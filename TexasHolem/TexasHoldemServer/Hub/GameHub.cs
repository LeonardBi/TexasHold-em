using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TexasHoldemServer.GameLogic;
using TexasHoldemServer.Data;

namespace TexasHoldemServer.Hubs
{
    public class ServerPlayer
    {
        public Player CorePlayer { get; set; }
        public string ConnectionId { get; set; }
        public bool IsReady { get; set; } = false;
        public bool IsSittingOut { get; set; } = false;
        public bool SitOutNextHand { get; set; } = false;
        public bool HasFolded { get; set; } = false;
        public int CurrentBet { get; set; } = 0;
        public bool HasActedThisRound { get; set; } = false;
    }

    public class GameHub : Hub
    {
        private static List<ServerPlayer> _players = new List<ServerPlayer>();
        private static Deck _deck = new Deck();
        private static List<Card> _communityCards = new List<Card>();

        private static int _currentTurnIndex = -1;
        private static int _pot = 0;
        private static int _currentMaxBet = 0;
        private static string _gameState = "Waiting";

        private readonly AppDbContext _dbContext;

        public GameHub(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public static bool IsUserOnline(string username)
        {
            return _players.Any(p => p.CorePlayer.Name == username);
        }

        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
        }

        private async Task SyncTableState()
        {
            var tableData = _players.Select((p, index) => new {
                Name = p.CorePlayer.Name,
                Chips = p.CorePlayer.Chips,
                SeatIndex = index,
                Status = p.IsSittingOut ? "休息中" : (p.SitOutNextHand ? "暂离预约" : (p.HasFolded ? "已弃牌" : (p.IsReady ? "已准备" : "等待中"))),
                CurrentBet = p.CurrentBet,
                IsSitOutNextHand = p.SitOutNextHand
            }).ToList();

            await Clients.All.SendAsync("UpdateTable", tableData, _pot, _currentMaxBet, _gameState);
        }

        public async Task JoinGame(string playerName)
        {
            // Prevent duplicate logins
            var existing = _players.FirstOrDefault(p => p.CorePlayer.Name == playerName);
            if (existing != null) _players.Remove(existing);

            // Enforce maximum table capacity
            if (_players.Count >= 6) { await Clients.Caller.SendAsync("SystemMessage", "Room is full!"); return; }

            // Verify account balance directly from the database, not from client input
            var dbUser = _dbContext.Users.FirstOrDefault(u => u.Username == playerName);
            int realChips = dbUser != null ? dbUser.Chips : 5000;

            if (realChips <= 0) { await Clients.Caller.SendAsync("KickedOut", "Insufficient chips!"); return; }
            // Register player securely and broadcast update
            _players.Add(new ServerPlayer { CorePlayer = new Player(playerName, realChips), ConnectionId = Context.ConnectionId });
            await Clients.All.SendAsync("SystemMessage", $"Player【{playerName}】is sitting on the table");
            await SyncTableState();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var player = _players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
            if (player != null)
            {
                TexasHoldemServer.Hubs.SessionManager.LogoutUser(player.CorePlayer.Name);
                var dbUser = _dbContext.Users.FirstOrDefault(u => u.Username == player.CorePlayer.Name);
                if (dbUser != null) { dbUser.Chips = player.CorePlayer.Chips; await _dbContext.SaveChangesAsync(); }

                _players.Remove(player);
                await Clients.All.SendAsync("SystemMessage", $"Player【{player.CorePlayer.Name}】left the table.");

                // Check if the game is in progress and handle the disconnection accordingly
                if (_gameState != "Waiting")
                {
                    // Get the list of active players who haven't folded yet
                    var activePlayers = _players.Where(p => !p.HasFolded).ToList();

                    if (activePlayers.Count <= 1)
                    {
                        // if only one player remains, declare them the winner by default
                        if (activePlayers.Count == 1)
                        {
                            await DeclareWinner(activePlayers[0], "Player disconnected");
                        }
                        // if no players remain, prepare for the next round without declaring a winner
                        else
                        {
                            await ResetForNextRound();
                        }
                    }
                    // if the disconnected player is the one whose turn it is, treat it as a fold and move to the next turn
                    else if (_currentTurnIndex >= 0 && _currentTurnIndex < _players.Count && _players[_currentTurnIndex] == player)
                    {
                        await PlayerAction("Fold 弃牌", 0);
                    }
                    else
                    {
                        await SyncTableState();
                    }
                }
                else
                {
                    await SyncTableState();
                }
            }
            await base.OnDisconnectedAsync(exception);
        }

        public async Task ToggleReady()
        {
            var player = _players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
            if (player == null) return;

            player.IsReady = !player.IsReady;

            await CheckAutoStart();
        }

        public async Task ToggleSitOut()
        {
            var player = _players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
            if (player == null) return;
            if (_gameState == "Waiting") { player.IsSittingOut = true; player.IsReady = false; }
            else { player.SitOutNextHand = !player.SitOutNextHand; }
            await SyncTableState();
        }

        private async Task CheckAutoStart()
        {
            var activePlayers = _players.Where(p => !p.IsSittingOut && p.IsReady && p.CorePlayer.Chips > 0).ToList();
            if (activePlayers.Count >= 2 && _gameState == "Waiting") await StartRound(); else await SyncTableState();
        }

        private async Task StartRound()
        {
            _gameState = "PreFlop"; _pot = 0; _currentMaxBet = 0;
            _deck.Reset(); _communityCards.Clear();
            await Clients.All.SendAsync("ClearCommunityCards");

            foreach (var p in _players)
            {
                p.HasFolded = p.IsSittingOut || !p.IsReady || p.CorePlayer.Chips == 0;
                p.CurrentBet = 0; p.HasActedThisRound = false;
                p.CorePlayer.HoleCards.Clear();

                if (!p.HasFolded)
                {
                    p.CorePlayer.HoleCards.Add(_deck.DealCard()); p.CorePlayer.HoleCards.Add(_deck.DealCard());
                    await Clients.Client(p.ConnectionId).SendAsync("ReceiveHoleCards", p.CorePlayer.HoleCards[0].ImageFileName, p.CorePlayer.HoleCards[1].ImageFileName);
                }
            }
            await SyncTableState(); _currentTurnIndex = -1; await NextTurn();
        }

        public async Task PlayerAction(string actionType, int targetBetAmount)
        {
            if (_gameState == "Waiting" || _currentTurnIndex < 0) return;
            var player = _players[_currentTurnIndex];
            if (player.ConnectionId != Context.ConnectionId) return;

            player.HasActedThisRound = true;

            if (_gameState == "RiverSwap")
            {
                if (actionType == "KeepHand")
                {
                    await Clients.All.SendAsync("SystemMessage", $"【{player.CorePlayer.Name}】Keep Card!");
                }
                else if (actionType.StartsWith("SwapCard_"))
                {
                    int cardIndex = int.Parse(actionType.Split('_')[1]);
                    if (cardIndex == 0 || cardIndex == 1)
                    {
                        Card newCard = _deck.DealCard();
                        player.CorePlayer.HoleCards[cardIndex] = newCard;
                        await Clients.Client(player.ConnectionId).SendAsync("ReceiveHoleCards", player.CorePlayer.HoleCards[0].ImageFileName, player.CorePlayer.HoleCards[1].ImageFileName);
                        await Clients.All.SendAsync("SystemMessage", $"【{player.CorePlayer.Name}】Switch Card!");
                    }
                }
                await SyncTableState(); await NextTurn(); return;
            }

            if (actionType.Contains("Fold")) { player.HasFolded = true; await Clients.All.SendAsync("SystemMessage", $"【{player.CorePlayer.Name}】弃牌了。"); }
            else if (actionType.Contains("Call"))
            {
                int amountToCall = Math.Min(_currentMaxBet - player.CurrentBet, player.CorePlayer.Chips);
                player.CorePlayer.Chips -= amountToCall;
                player.CurrentBet += amountToCall;
                _pot += amountToCall;
                if (amountToCall == 0)
                {
                    await Clients.All.SendAsync("SystemMessage", $"【{player.CorePlayer.Name}】选择了看牌。");
                }
                else
                {
                    await Clients.All.SendAsync("SystemMessage", $"【{player.CorePlayer.Name}】跟注 ${amountToCall}。");
                }
            }
            else if (actionType.Contains("Raise") || actionType.Contains("All-In"))
            {
                int amountToPay = Math.Min(targetBetAmount - player.CurrentBet, player.CorePlayer.Chips);
                player.CorePlayer.Chips -= amountToPay; player.CurrentBet += amountToPay; _pot += amountToPay;
                _currentMaxBet = player.CurrentBet;
                foreach (var p in _players.Where(p => p != player && !p.HasFolded)) p.HasActedThisRound = false;
                await Clients.All.SendAsync("SystemMessage", actionType.Contains("All-In") ? $"【{player.CorePlayer.Name}】All-In 梭哈了！" : $"【{player.CorePlayer.Name}】加注到 ${_currentMaxBet}！");
            }

            await SyncTableState(); await NextTurn();
        }

        private async Task NextTurn()
        {
            var activePlayers = _players.Where(p => !p.HasFolded).ToList();
            if (activePlayers.Count == 1) { await DeclareWinner(activePlayers[0], "其他玩家全部弃牌"); return; }

            bool roundIsComplete = activePlayers.All(p => p.CorePlayer.Chips == 0 || (p.HasActedThisRound && p.CurrentBet == _currentMaxBet));
            if (roundIsComplete) { await AdvancePhase(); return; }

            int loopGuard = 0;
            do
            {
                _currentTurnIndex = (_currentTurnIndex + 1) % _players.Count;
                if (++loopGuard > _players.Count * 2) { await AdvancePhase(); return; }
            }
            while (_players[_currentTurnIndex].HasFolded || _players[_currentTurnIndex].CorePlayer.Chips == 0 || (_players[_currentTurnIndex].HasActedThisRound && _players[_currentTurnIndex].CurrentBet == _currentMaxBet));

            await Clients.All.SendAsync("UpdateTurn", _players[_currentTurnIndex].CorePlayer.Name, _currentMaxBet - _players[_currentTurnIndex].CurrentBet);
        }

        private async Task AdvancePhase()
        {
            await Task.Delay(1000);
            // Reset round-specific betting variables
            foreach (var p in _players) { p.CurrentBet = 0; p.HasActedThisRound = false; }
            _currentMaxBet = 0; await SyncTableState();

            // Finite State Machine transitions
            if (_gameState == "River")
            {
                _gameState = "RiverSwap";
                await Clients.All.SendAsync("SystemMessage", "Final River Swap!");
                await SyncTableState(); _currentTurnIndex = -1; await NextTurn(); return;
            }
            if (_gameState == "RiverSwap") { await EvaluateShowdown(); return; }

            // Deal community cards based on the new state
            if (_gameState == "PreFlop") { _gameState = "Flop"; for (int i = 0; i < 3; i++) { _communityCards.Add(_deck.DealCard()); } }
            else if (_gameState == "Flop") { _gameState = "Turn"; _communityCards.Add(_deck.DealCard()); }
            else if (_gameState == "Turn") { _gameState = "River"; _communityCards.Add(_deck.DealCard()); }

            var allCardImages = _communityCards.Select(c => c.ImageFileName).ToList();
            await Clients.All.SendAsync("ReceiveCommunityCards", allCardImages);

            var activePlayers = _players.Where(p => !p.HasFolded).ToList();
            if (activePlayers.Count(p => p.CorePlayer.Chips > 0) <= 1) { await AdvancePhase(); }
            else { _currentTurnIndex = -1; await NextTurn(); }
        }

        private async Task EvaluateShowdown()
        {
            var activePlayers = _players.Where(p => !p.HasFolded).ToList();
            if (activePlayers.Count == 0) return;

            ServerPlayer winner = activePlayers[0]; int maxScore = -1;
            foreach (var p in activePlayers)
            {
                int score = CalculateHandScore(p.CorePlayer.HoleCards.Concat(_communityCards).ToList());
                if (score > maxScore) { maxScore = score; winner = p; }
            }

            winner.CorePlayer.Chips += _pot;
            await Clients.All.SendAsync("ShowWinner", $"【{winner.CorePlayer.Name}】赢得了 ${_pot}！", "摊牌比大小获胜");
            await ResetForNextRound();
        }

        private int CalculateHandScore(List<Card> cards)
        {
            var ranks = cards.Select(c => {
                string rankStr = c.ImageFileName.ToLower().Split('_')[0];
                if (rankStr == "ace") return 14; if (rankStr == "king") return 13;
                if (rankStr == "queen") return 12; if (rankStr == "jack") return 11;
                int.TryParse(rankStr, out int r); return r;
            }).OrderByDescending(r => r).ToList();
            var groups = ranks.GroupBy(r => r).OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key).ToList();
            if (groups[0].Count() == 4) return 8000000 + groups[0].Key;
            if (groups[0].Count() == 3 && groups.Count > 1 && groups[1].Count() >= 2) return 7000000 + groups[0].Key;
            if (groups[0].Count() == 3) return 4000000 + groups[0].Key;
            if (groups[0].Count() == 2 && groups.Count > 1 && groups[1].Count() == 2) return 3000000 + groups[0].Key * 100 + groups[1].Key;
            if (groups[0].Count() == 2) return 2000000 + groups[0].Key * 10000 + ranks.First(r => r != groups[0].Key);
            return 1000000 + ranks[0] * 10000 + ranks[1] * 100 + ranks[2];
        }

        private async Task DeclareWinner(ServerPlayer winner, string reason)
        {
            winner.CorePlayer.Chips += _pot;
            await Clients.All.SendAsync("ShowWinner", $"【{winner.CorePlayer.Name}】赢得了 ${_pot}！", reason);
            await ResetForNextRound();
        }

        private async Task ResetForNextRound()
        {
            _gameState = "Waiting";
            foreach (var p in _players) { var dbUser = _dbContext.Users.FirstOrDefault(u => u.Username == p.CorePlayer.Name); if (dbUser != null) { dbUser.Chips = p.CorePlayer.Chips; } }
            await _dbContext.SaveChangesAsync();

            await Task.Delay(3500);

            var brokePlayers = _players.Where(p => p.CorePlayer.Chips <= 0).ToList();
            foreach (var p in brokePlayers) { await Clients.Client(p.ConnectionId).SendAsync("KickedOut", "破产遣返：你的筹码已经归零，请离开房间充值后再战！"); _players.Remove(p); }

            foreach (var p in _players) { if (p.SitOutNextHand) { p.IsSittingOut = true; p.IsReady = false; p.SitOutNextHand = false; } }

            await Clients.All.SendAsync("ClearCommunityCards"); await CheckAutoStart();
            if (_gameState == "Waiting") await Clients.All.SendAsync("SystemMessage", "请各位点击【准备】开始新的一局。");
        }

        public async Task SendMessage(string user, string message) => await Clients.All.SendAsync("ReceiveMessage", user, message);
    }
}