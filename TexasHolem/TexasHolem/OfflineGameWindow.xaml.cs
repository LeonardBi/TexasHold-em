using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TexasHoldemClient
{
    public partial class OfflineGameWindow : Window
    {
        private GameEngine _gameEngine = null!;
        private Player _humanPlayer = null!;
        private Player _computer1 = null!;
        private Player _computer2 = null!;
        private Player _computer3 = null!;
        private Player _computer4 = null!;
        private Player _computer5 = null!;

        // game state variables
        private enum GameStage { PreFlop, Flop, Turn, River, RiverSwap, Showdown }
        private GameStage _currentStage = GameStage.Showdown;

        private List<Player> _allPlayers = new List<Player>();
        private int[] _playerBets = new int[6];
        private int _currentBet = 0;

        private int _smallBlindAmount = 50;
        private int _bigBlindAmount = 100;

        private int _dealerIndex = -1;
        private int _currentPlayerIndex = 0;
        private int _playersActedThisRound = 0;
        private int _selectedSwapIndex = -1;
        private bool _isExiting = false;

        private CancellationTokenSource _cts = new CancellationTokenSource();

        public OfflineGameWindow(string playerName = "Player")
        {
            InitializeComponent();
            InitializeGame(playerName);
            ApplySettings();
        }

        private void InitializeGame(string pName)
        {
            //initialize players and game engine
            _gameEngine = new GameEngine();
            _humanPlayer = new Player(pName, 50000);
            _computer1 = new Player("Computer 1", 10000);
            _computer2 = new Player("Computer 2", 10000);
            _computer3 = new Player("Computer 3", 10000) { IsSeatEmpty = true, IsSittingOut = true };
            _computer4 = new Player("Computer 4", 10000) { IsSeatEmpty = true, IsSittingOut = true };
            _computer5 = new Player("Computer 5", 10000) { IsSeatEmpty = true, IsSittingOut = true };

            _allPlayers.Add(_humanPlayer);
            _allPlayers.Add(_computer4);
            _allPlayers.Add(_computer1);
            _allPlayers.Add(_computer2);
            _allPlayers.Add(_computer3);
            _allPlayers.Add(_computer5);

            UpdateBotUI();
            UpdateUIStats();
        }

        // utility methods
        private void AddLog(string msg)
        {
            GameLogBox.Items.Add(msg);
            if (GameLogBox.Items.Count > 0) GameLogBox.ScrollIntoView(GameLogBox.Items[GameLogBox.Items.Count - 1]);
        }

        // shows a temporary toast message above a player's area to indicate their action
        private async void ShowActionToast(Border toastBorder, TextBlock toastText, string message, string playerName)
        {
            toastText.Text = message;
            toastBorder.Visibility = Visibility.Visible;
            AddLog(GameSettings.Translate($"[{playerName}] 选择了: {message}", $"[{playerName}] chose: {message}"));
            try { await Task.Delay(1500, _cts.Token); } catch { }
            toastBorder.Visibility = Visibility.Collapsed;
        }

        // finds the next player index who has an active seat and is not sitting out, starting from the given index
        private int GetNextActivePlayerIndex(int startIndex)
        {
            for (int i = 1; i <= 6; i++)
            {
                int index = (startIndex + i) % 6;
                if (!_allPlayers[index].IsSeatEmpty && !_allPlayers[index].IsSittingOut) return index;
            }
            return startIndex;
        }

        // finds the next player index who can actually play starting from the given index
        private int GetNextPlayingPlayerIndex(int startIndex)
        {
            for (int i = 1; i <= 6; i++)
            {
                int index = (startIndex + i) % 6;
                if (!_allPlayers[index].IsSeatEmpty && !_allPlayers[index].IsSittingOut && _allPlayers[index].HoleCards.Count > 0 && _allPlayers[index].Chips > 0) return index;
            }
            return startIndex;
        }

        // Starts a new round by resetting all necessary game state, dealing new cards, and initiating the first betting round
        private void StartNewRound()
        {
            CenterBroadcastToast.Visibility = Visibility.Collapsed;
            ResetHighlight();
            ActionPanel.Visibility = Visibility.Visible;
            SwapPanel.Visibility = Visibility.Collapsed;
            GameLogBox.Items.Clear();
            AddLog(GameSettings.Translate("====== 🎲 新的对局开始 ======", "====== 🎲 New Round Starts ======"));

            foreach (var p in _allPlayers)
            {
                p.ResetForNewRound();
                if (!p.IsSeatEmpty && p.Chips < _bigBlindAmount) { p.IsSittingOut = true; p.HoleCards.Clear(); }
            }

            if (_allPlayers.Count(p => !p.IsSeatEmpty && !p.IsSittingOut) <= 1)
            {
                CenterBroadcastText.Text = GameSettings.Translate("游戏结束！剩余玩家不足。", "Game Over! Not enough players.");
                CenterBroadcastToast.Visibility = Visibility.Visible;
                return;
            }

            _gameEngine.Deck.Reset(); _gameEngine.CommunityCards.Clear(); _gameEngine.Pot = 0;

            foreach (var p in _allPlayers)
            {
                if (!p.IsSeatEmpty && !p.IsSittingOut)
                {
                    p.HoleCards.Add(_gameEngine.Deck.DealCard()!);
                    p.HoleCards.Add(_gameEngine.Deck.DealCard()!);
                }
            }

            for (int i = 0; i < 6; i++) _playerBets[i] = 0;
            _dealerIndex = _dealerIndex == -1 ? 0 : GetNextActivePlayerIndex(_dealerIndex);
            int sbIndex = GetNextActivePlayerIndex(_dealerIndex);
            int bbIndex = GetNextActivePlayerIndex(sbIndex);
            Player dealerPlayer = _allPlayers[_dealerIndex];

            DealerBtn0.Visibility = dealerPlayer == _humanPlayer ? Visibility.Visible : Visibility.Collapsed;
            DealerBtn1.Visibility = dealerPlayer == _computer1 ? Visibility.Visible : Visibility.Collapsed;
            DealerBtn2.Visibility = dealerPlayer == _computer2 ? Visibility.Visible : Visibility.Collapsed;
            DealerBtn3.Visibility = dealerPlayer == _computer3 ? Visibility.Visible : Visibility.Collapsed;
            DealerBtn4.Visibility = dealerPlayer == _computer4 ? Visibility.Visible : Visibility.Collapsed;
            DealerBtn5.Visibility = dealerPlayer == _computer5 ? Visibility.Visible : Visibility.Collapsed;

            int sbPay = Math.Min(_allPlayers[sbIndex].Chips, _smallBlindAmount);
            _allPlayers[sbIndex].Chips -= sbPay; _playerBets[sbIndex] = sbPay; _allPlayers[sbIndex].CurrentHandInvestment += sbPay; _gameEngine.Pot += sbPay;
            int bbPay = Math.Min(_allPlayers[bbIndex].Chips, _bigBlindAmount);
            _allPlayers[bbIndex].Chips -= bbPay; _playerBets[bbIndex] = bbPay; _allPlayers[bbIndex].CurrentHandInvestment += bbPay; _gameEngine.Pot += bbPay;

            _currentBet = _bigBlindAmount; _currentPlayerIndex = GetNextPlayingPlayerIndex(bbIndex);

            if (!_humanPlayer.IsSeatEmpty && !_humanPlayer.IsSittingOut) DisplayCardsOnPanel(_humanPlayer.HoleCards, PlayerHoleCardsPanel, true);
            if (!_computer1.IsSeatEmpty && !_computer1.IsSittingOut) DisplayCardsOnPanel(_computer1.HoleCards, Comp1HoleCardsPanel, false);
            if (!_computer2.IsSeatEmpty && !_computer2.IsSittingOut) DisplayCardsOnPanel(_computer2.HoleCards, Comp2HoleCardsPanel, false);
            if (!_computer3.IsSeatEmpty && !_computer3.IsSittingOut) DisplayCardsOnPanel(_computer3.HoleCards, Comp3HoleCardsPanel, false);
            if (!_computer4.IsSeatEmpty && !_computer4.IsSittingOut) DisplayCardsOnPanel(_computer4.HoleCards, Comp4HoleCardsPanel, false);
            if (!_computer5.IsSeatEmpty && !_computer5.IsSittingOut) DisplayCardsOnPanel(_computer5.HoleCards, Comp5HoleCardsPanel, false);

            CommunityCardsPanel.Children.Clear(); _currentStage = GameStage.PreFlop;

            // play new round sound
            AudioManager.PlaySound("ShowCard.mp3");

            StartBettingRound();
        }

        private async void StartBettingRound()
        {
            _playersActedThisRound = 0;
            if (_currentStage != GameStage.PreFlop) _currentPlayerIndex = GetNextPlayingPlayerIndex(_dealerIndex);
            int playersCanAct = _allPlayers.Count(p => p.HoleCards.Count > 0 && !p.IsSeatEmpty && !p.IsSittingOut && p.Chips > 0);
            if (playersCanAct <= 1 && IsBettingRoundComplete())
            {
                try { await Task.Delay(800, _cts.Token); } catch { return; }
                AdvanceGameStage(); return;
            }
            ProcessCurrentTurn();
        }

        private bool IsBettingRoundComplete()
        {
            int playersCanAct = _allPlayers.Count(p => p.HoleCards.Count > 0 && !p.IsSeatEmpty && !p.IsSittingOut && p.Chips > 0);
            if (_playersActedThisRound < playersCanAct) return false;
            for (int i = 0; i < 6; i++)
            {
                var p = _allPlayers[i];
                if (!p.IsSeatEmpty && !p.IsSittingOut && p.HoleCards.Count > 0 && p.Chips > 0)
                {
                    if (_playerBets[i] < _currentBet) return false;
                }
            }
            return true;
        }

        private void ProcessCurrentTurn()
        {
            if (_allPlayers.Count(p => p.HoleCards.Count > 0 && !p.IsSeatEmpty && !p.IsSittingOut) == 1) { ExecuteEarlyWin(); return; }
            ResetHighlight(); SetActionButtonsEnabled(false);
            Player currentPlayer = _allPlayers[_currentPlayerIndex];
            if (currentPlayer.IsSeatEmpty || currentPlayer.IsSittingOut || currentPlayer.HoleCards.Count == 0 || currentPlayer.Chips == 0) { AdvanceToNextPlayer(); return; }

            // highlight current player
            if (currentPlayer == _humanPlayer) HumanBorder.BorderBrush = Brushes.Gold;
            else if (currentPlayer == _computer1) Comp1Border.BorderBrush = Brushes.Gold;
            else if (currentPlayer == _computer2) Comp2Border.BorderBrush = Brushes.Gold;
            else if (currentPlayer == _computer3) Comp3Border.BorderBrush = Brushes.Gold;
            else if (currentPlayer == _computer4) Comp4Border.BorderBrush = Brushes.Gold;
            else if (currentPlayer == _computer5) Comp5Border.BorderBrush = Brushes.Gold;

            if (currentPlayer == _humanPlayer)
            {
                SetActionButtonsEnabled(true);
                UpdateUIStats();
                AudioManager.PlaySound("PlayersTurn.mp3");
            }
            else { ExecuteAITurnAsync(currentPlayer); }
        }

        // executes the AI logic for the given bot player, updates the game state accordingly, and advances to the next turn after a short delay
        private async void ExecuteAITurnAsync(Player bot)
        {
            try
            {
                await Task.Delay(1000, _cts.Token);
                Border botToast = bot == _computer1 ? Comp1ActionToast : bot == _computer2 ? Comp2ActionToast : bot == _computer3 ? Comp3ActionToast : bot == _computer4 ? Comp4ActionToast : Comp5ActionToast;
                TextBlock botText = bot == _computer1 ? Comp1ActionText : bot == _computer2 ? Comp2ActionText : bot == _computer3 ? Comp3ActionText : bot == _computer4 ? Comp4ActionText : Comp5ActionText;

                if (ComputerLogic.DecideCallOrFold(bot, _gameEngine.CommunityCards, _currentBet))
                {
                    int botToPay = _currentBet - _playerBets[_currentPlayerIndex];
                    if (botToPay >= bot.Chips)
                    {
                        botToPay = bot.Chips; ShowActionToast(botToast, botText, "ALL-IN!", bot.Name);
                        if (bot.Chips + _playerBets[_currentPlayerIndex] > _currentBet) { _currentBet = bot.Chips + _playerBets[_currentPlayerIndex]; _playersActedThisRound = 1; }
                        else _playersActedThisRound++;
                    }
                    else { ShowActionToast(botToast, botText, botToPay == 0 ? "Check" : "Call", bot.Name); _playersActedThisRound++; }
                    bot.Chips -= botToPay; _gameEngine.Pot += botToPay; _playerBets[_currentPlayerIndex] += botToPay; bot.CurrentHandInvestment += botToPay;
                }
                else
                {
                    bot.HoleCards.Clear();
                    if (bot == _computer1) Comp1HoleCardsPanel.Children.Clear();
                    else if (bot == _computer2) Comp2HoleCardsPanel.Children.Clear();
                    else if (bot == _computer3) Comp3HoleCardsPanel.Children.Clear();
                    else if (bot == _computer4) Comp4HoleCardsPanel.Children.Clear();
                    else if (bot == _computer5) Comp5HoleCardsPanel.Children.Clear();
                    ShowActionToast(botToast, botText, "Fold", bot.Name); _playersActedThisRound++;
                }
                UpdateUIStats(); await Task.Delay(1500, _cts.Token); AdvanceToNextPlayer();
            }
            catch { }
        }

        // enables or disables the action buttons for the human player
        private void AdvanceToNextPlayer()
        { 
            if (IsBettingRoundComplete()) AdvanceGameStage();
            else { _currentPlayerIndex = (_currentPlayerIndex + 1) % 6; ProcessCurrentTurn(); } 
        }

        // Game stage progression logic
        private void AdvanceGameStage()
        {
            if (_currentStage == GameStage.PreFlop)
            {
                _gameEngine.DealCommunityCards(3); _currentStage = GameStage.Flop;
                AddLog(GameSettings.Translate("翻牌 Flop", "Flop"));
                AudioManager.PlaySound("ShowCard.mp3");
            }
            else if (_currentStage == GameStage.Flop)
            {
                _gameEngine.DealCommunityCards(1); _currentStage = GameStage.Turn;
                AddLog(GameSettings.Translate("转牌 Turn", "Turn"));
                AudioManager.PlaySound("ShowCard.mp3");
            }
            else if (_currentStage == GameStage.Turn)
            {
                _gameEngine.DealCommunityCards(1); _currentStage = GameStage.River;
                AddLog(GameSettings.Translate("河牌 River", "River"));
                AudioManager.PlaySound("ShowCard.mp3");
            }
            else if (_currentStage == GameStage.River) { if (_allPlayers.Any(p => p.HoleCards.Count > 0 && !p.IsSeatEmpty && !p.IsSittingOut && p.Chips == 0)) ExecuteShowdown(); else StartRiverSwapPhase(); return; }

            for (int i = 0; i < 6; i++) _playerBets[i] = 0; _currentBet = 0;
            DisplayCardsOnPanel(_gameEngine.CommunityCards, CommunityCardsPanel, true);
            UpdateUIStats(); StartBettingRound();
        }

        // *Swap Card logic after river
        private void StartRiverSwapPhase()
        {
            _currentStage = GameStage.RiverSwap;
            foreach (var bot in _allPlayers.Where(p => p != _humanPlayer && !p.IsSeatEmpty && !p.IsSittingOut && p.HoleCards.Count > 0)) ComputerLogic.DecideRiverSwap(bot, _gameEngine.CommunityCards, _gameEngine.Deck);
            if (_humanPlayer.HoleCards.Count > 0 && !_humanPlayer.IsSittingOut)
            {
                ActionPanel.Visibility = Visibility.Collapsed; SwapPanel.Visibility = Visibility.Visible;
                CenterBroadcastText.Text = GameSettings.Translate("终极换牌阶段！\n点击你要换的底牌。", "Final Swap Phase!\nClick a hole card to swap.");
                CenterBroadcastToast.Visibility = Visibility.Visible; _selectedSwapIndex = -1;
            }
            else ExecuteShowdown();
        }

        // Highlights the selected card for swapping and stores the selected index
        private void SelectCardToSwap(int index)
        {
            _selectedSwapIndex = index;
            foreach (Border b in PlayerHoleCardsPanel.Children.OfType<Border>()) b.BorderBrush = Brushes.Transparent;
            if (index >= 0 && index < PlayerHoleCardsPanel.Children.Count)
            {
                ((Border)PlayerHoleCardsPanel.Children[index]).BorderBrush = Brushes.Yellow;
            }
        }

        // Event handlers for the swap buttons  
        private void DoSwapBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedSwapIndex == -1)
            {
                MessageBox.Show(GameSettings.Translate("请先选中一张要换的底牌！", "Please select a card to swap first!"));
                return;
            }
            Card dropCard = _humanPlayer.HoleCards[_selectedSwapIndex];
            Card keepCard = _humanPlayer.HoleCards[1 - _selectedSwapIndex];

            _gameEngine.Deck.InsertCardRandomly(dropCard);
            Card newCard = _gameEngine.Deck.DealCard()!;

            _humanPlayer.HoleCards.Clear();
            if (_selectedSwapIndex == 0)
            {
                _humanPlayer.HoleCards.Add(newCard);
                _humanPlayer.HoleCards.Add(keepCard);
            }
            else 
            {
                _humanPlayer.HoleCards.Add(keepCard);
                _humanPlayer.HoleCards.Add(newCard);
            }
            DisplayCardsOnPanel(_humanPlayer.HoleCards, PlayerHoleCardsPanel, true);
            CenterBroadcastToast.Visibility = Visibility.Collapsed; ExecuteShowdown();
        }

        private void PassSwapBtn_Click(object sender, RoutedEventArgs e)
        { 
            CenterBroadcastToast.Visibility = Visibility.Collapsed; ExecuteShowdown(); 
        }

        //Called when the human player clicks the Call button. It calculates the amount to call, updates the player's chips and the pot, shows a toast message, and advances to the next player.    
        private void CallBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStage == GameStage.Showdown) { StartNewRound(); return; }
            int toPay = _currentBet - _playerBets[0];
            if (toPay >= _humanPlayer.Chips) { AllInBtn_Click(null, null); return; }
            if (toPay > 0)
            {
                _humanPlayer.Chips -= toPay; _gameEngine.Pot += toPay; _playerBets[0] = _currentBet; _humanPlayer.CurrentHandInvestment += toPay;
                ShowActionToast(PlayerActionToast, PlayerActionText, "Call", _humanPlayer.Name);
                AudioManager.PlaySound("CallRaise.mp3");
            }
            else
            {
                ShowActionToast(PlayerActionToast, PlayerActionText, "Check", _humanPlayer.Name);
                AudioManager.PlaySound("Check.mp3");
            }
            SetActionButtonsEnabled(false); _playersActedThisRound++; AdvanceToNextPlayer();
        }

        private void RaiseBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(RaiseAmountInput.Text, out int raiseAmount)) return;
            if (raiseAmount < _currentBet + _bigBlindAmount) raiseAmount = _currentBet + _bigBlindAmount;
            int toPay = raiseAmount - _playerBets[0];
            if (_humanPlayer.Chips <= toPay) { AllInBtn_Click(null, null); return; }
            _humanPlayer.Chips -= toPay; _gameEngine.Pot += toPay; _playerBets[0] = raiseAmount; _currentBet = raiseAmount; _humanPlayer.CurrentHandInvestment += toPay;
            ShowActionToast(PlayerActionToast, PlayerActionText, $"Raise {raiseAmount}", _humanPlayer.Name);
            AudioManager.PlaySound("CallRaise.mp3");
            _playersActedThisRound = 1; SetActionButtonsEnabled(false); AdvanceToNextPlayer();
        }

        private void AllInBtn_Click(object sender, RoutedEventArgs e)
        {
            int toPay = _humanPlayer.Chips; int raiseAmount = _playerBets[0] + toPay;
            _humanPlayer.Chips -= toPay; _gameEngine.Pot += toPay; _playerBets[0] = raiseAmount; _humanPlayer.CurrentHandInvestment += toPay;
            if (raiseAmount > _currentBet) { _currentBet = raiseAmount; _playersActedThisRound = 1; } else _playersActedThisRound++;
            ShowActionToast(PlayerActionToast, PlayerActionText, "ALL-IN!", _humanPlayer.Name);
            AudioManager.PlaySound("Allin.mp3");
            SetActionButtonsEnabled(false); AdvanceToNextPlayer();
        }

        private void FoldBtn_Click(object sender, RoutedEventArgs e)
        {
            ShowActionToast(PlayerActionToast, PlayerActionText, "Fold", _humanPlayer.Name);
            _humanPlayer.HoleCards.Clear(); PlayerHoleCardsPanel.Children.Clear();
            AudioManager.PlaySound("Fold.mp3");
            SetActionButtonsEnabled(false); _playersActedThisRound++; AdvanceToNextPlayer();
        }

        private void RefundUnmatchedBets()
        {
            var active = _allPlayers.Where(p => p.HoleCards.Count > 0 && !p.IsSeatEmpty && !p.IsSittingOut).ToList();
            if (active.Count == 0) return;
            foreach (var p in active)
            {
                int maxOther = 0; var others = active.Where(o => o != p).ToList();
                if (others.Count > 0) maxOther = others.Max(o => o.CurrentHandInvestment);
                if (p.CurrentHandInvestment > maxOther) { int refund = p.CurrentHandInvestment - maxOther; p.Chips += refund; _gameEngine.Pot -= refund; p.CurrentHandInvestment = maxOther; }
            }
        }

        private void ExecuteEarlyWin()
        {
            RefundUnmatchedBets(); Player winner = _allPlayers.First(p => p.HoleCards.Count > 0 && !p.IsSeatEmpty && !p.IsSittingOut);
            winner.Chips += _gameEngine.Pot;
            CenterBroadcastText.Text = GameSettings.Translate($"🏆 {winner.Name} 获胜!\n赢取: ${_gameEngine.Pot}", $"🏆 {winner.Name} Wins!\nPot: ${_gameEngine.Pot}");
            CenterBroadcastToast.Visibility = Visibility.Visible;

            if (winner == _humanPlayer) AudioManager.PlaySound("Win.mp3");
            else AudioManager.PlaySound("Lose.mp3");

            _gameEngine.Pot = 0; _currentStage = GameStage.Showdown; ResetHighlight(); UpdateUIStats();
            ActionPanel.Visibility = Visibility.Visible; SwapPanel.Visibility = Visibility.Collapsed;
        }

        private void ExecuteShowdown()
        {
            RefundUnmatchedBets();
            if (!_computer1.IsSeatEmpty && !_computer1.IsSittingOut && _computer1.HoleCards.Count > 0) DisplayCardsOnPanel(_computer1.HoleCards, Comp1HoleCardsPanel, true);
            if (!_computer2.IsSeatEmpty && !_computer2.IsSittingOut && _computer2.HoleCards.Count > 0) DisplayCardsOnPanel(_computer2.HoleCards, Comp2HoleCardsPanel, true);
            if (!_computer3.IsSeatEmpty && !_computer3.IsSittingOut && _computer3.HoleCards.Count > 0) DisplayCardsOnPanel(_computer3.HoleCards, Comp3HoleCardsPanel, true);
            if (!_computer4.IsSeatEmpty && !_computer4.IsSittingOut && _computer4.HoleCards.Count > 0) DisplayCardsOnPanel(_computer4.HoleCards, Comp4HoleCardsPanel, true);
            if (!_computer5.IsSeatEmpty && !_computer5.IsSittingOut && _computer5.HoleCards.Count > 0) DisplayCardsOnPanel(_computer5.HoleCards, Comp5HoleCardsPanel, true);

            foreach (var p in _allPlayers.Where(p => p.HoleCards.Count > 0 && !p.IsSeatEmpty && !p.IsSittingOut)) p.BestHandResult = HandEvaluator.GetBestHand(p.HoleCards, _gameEngine.CommunityCards);
            Player winner = _allPlayers.Where(p => p.HoleCards.Count > 0 && !p.IsSeatEmpty && !p.IsSittingOut).OrderByDescending(p => p.BestHandResult).First();
            int wonAmount = _gameEngine.Pot; winner.Chips += wonAmount; _gameEngine.Pot = 0;
            ResetHighlight();
            if (winner == _humanPlayer) HumanBorder.BorderBrush = Brushes.LimeGreen;
            else if (winner == _computer1) Comp1Border.BorderBrush = Brushes.LimeGreen;
            else if (winner == _computer2) Comp2Border.BorderBrush = Brushes.LimeGreen;
            else if (winner == _computer3) Comp3Border.BorderBrush = Brushes.LimeGreen;
            else if (winner == _computer4) Comp4Border.BorderBrush = Brushes.LimeGreen;
            else if (winner == _computer5) Comp5Border.BorderBrush = Brushes.LimeGreen;

            CenterBroadcastText.Text = GameSettings.Translate($"🏆 {winner.Name} 获胜!\n牌型: {winner.BestHandResult.Type}\n赢取: ${wonAmount}", $"🏆 {winner.Name} Wins!\nHand: {winner.BestHandResult.Type}\nPot: ${wonAmount}");
            CenterBroadcastToast.Visibility = Visibility.Visible;

            if (winner == _humanPlayer) AudioManager.PlaySound("Win.mp3");
            else AudioManager.PlaySound("Lose.mp3");

            _currentStage = GameStage.Showdown; UpdateUIStats();
            ActionPanel.Visibility = Visibility.Visible; SwapPanel.Visibility = Visibility.Collapsed;
        }

        private void UpdateBotUI()
        {
            Comp1Border.Visibility = _computer1.IsSeatEmpty ? Visibility.Collapsed : Visibility.Visible; AddComp1Btn.Visibility = _computer1.IsSeatEmpty ? Visibility.Visible : Visibility.Collapsed;
            Comp2Border.Visibility = _computer2.IsSeatEmpty ? Visibility.Collapsed : Visibility.Visible; AddComp2Btn.Visibility = _computer2.IsSeatEmpty ? Visibility.Visible : Visibility.Collapsed;
            Comp3Border.Visibility = _computer3.IsSeatEmpty ? Visibility.Collapsed : Visibility.Visible; AddComp3Btn.Visibility = _computer3.IsSeatEmpty ? Visibility.Visible : Visibility.Collapsed;
            Comp4Border.Visibility = _computer4.IsSeatEmpty ? Visibility.Collapsed : Visibility.Visible; AddComp4Btn.Visibility = _computer4.IsSeatEmpty ? Visibility.Visible : Visibility.Collapsed;
            Comp5Border.Visibility = _computer5.IsSeatEmpty ? Visibility.Collapsed : Visibility.Visible; AddComp5Btn.Visibility = _computer5.IsSeatEmpty ? Visibility.Visible : Visibility.Collapsed;
        }

        private Player GetPlayerByTag(string tag) { return tag == "1" ? _computer1 : tag == "2" ? _computer2 : tag == "3" ? _computer3 : tag == "4" ? _computer4 : _computer5; }

        private void RemoveBot_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStage != GameStage.Showdown) { MessageBox.Show(GameSettings.Translate("只能在对局结束时移除玩家！", "Can only remove players at Showdown!")); return; }
            if (_allPlayers.Count(p => p != _humanPlayer && !p.IsSeatEmpty) <= 1) { MessageBox.Show(GameSettings.Translate("至少保留一名电脑玩家！", "Must keep at least one bot!")); return; }
            Player target = GetPlayerByTag(((Button)sender).Tag.ToString()); target.IsSeatEmpty = true; target.IsSittingOut = true; target.Chips = 0; UpdateBotUI();
        }

        private void AddBot_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStage != GameStage.Showdown) { MessageBox.Show(GameSettings.Translate("只能在对局结束时添加玩家！", "Can only add players at Showdown!")); return; }
            Player target = GetPlayerByTag(((Button)sender).Tag.ToString()); target.IsSeatEmpty = false; target.IsSittingOut = false; target.Chips = 10000; UpdateBotUI();
        }

        private void ResetHighlight() { HumanBorder.BorderBrush = Brushes.Transparent; Comp1Border.BorderBrush = Brushes.Transparent; Comp2Border.BorderBrush = Brushes.Transparent; Comp3Border.BorderBrush = Brushes.Transparent; Comp4Border.BorderBrush = Brushes.Transparent; Comp5Border.BorderBrush = Brushes.Transparent; }

        private void SetActionButtonsEnabled(bool isEnabled)
        {
            if (_currentStage == GameStage.Showdown) { ActionBtn.IsEnabled = true; FoldBtn.IsEnabled = false; RaiseBtn.IsEnabled = false; AllInBtn.IsEnabled = false; return; }
            ActionBtn.IsEnabled = isEnabled; FoldBtn.IsEnabled = isEnabled; RaiseBtn.IsEnabled = isEnabled; AllInBtn.IsEnabled = isEnabled;
        }

        private void UpdateUIStats()
        {
            PotText.Text = $"Pot: ${_gameEngine.Pot}";
            PlayerChipsText.Text = _humanPlayer.IsSittingOut ? "OUT" : (_humanPlayer.Chips == 0 && _humanPlayer.HoleCards.Count > 0 ? "ALL-IN" : $"${_humanPlayer.Chips}");
            Comp1ChipsText.Text = _computer1.IsSeatEmpty ? "" : (_computer1.IsSittingOut ? "OUT" : (_computer1.Chips == 0 && _computer1.HoleCards.Count > 0 ? "ALL-IN" : $"${_computer1.Chips}"));
            Comp2ChipsText.Text = _computer2.IsSeatEmpty ? "" : (_computer2.IsSittingOut ? "OUT" : (_computer2.Chips == 0 && _computer2.HoleCards.Count > 0 ? "ALL-IN" : $"${_computer2.Chips}"));
            Comp3ChipsText.Text = _computer3.IsSeatEmpty ? "" : (_computer3.IsSittingOut ? "OUT" : (_computer3.Chips == 0 && _computer3.HoleCards.Count > 0 ? "ALL-IN" : $"${_computer3.Chips}"));
            Comp4ChipsText.Text = _computer4.IsSeatEmpty ? "" : (_computer4.IsSittingOut ? "OUT" : (_computer4.Chips == 0 && _computer4.HoleCards.Count > 0 ? "ALL-IN" : $"${_computer4.Chips}"));
            Comp5ChipsText.Text = _computer5.IsSeatEmpty ? "" : (_computer5.IsSittingOut ? "OUT" : (_computer5.Chips == 0 && _computer5.HoleCards.Count > 0 ? "ALL-IN" : $"${_computer5.Chips}"));

            if (_currentStage == GameStage.Showdown) ActionBtn.Content = GameSettings.Translate("下一局", "Next Round");
            else if (_currentBet == _playerBets[0]) ActionBtn.Content = GameSettings.Translate("看牌 (Check)", "Check");
            else { int toCall = _currentBet - _playerBets[0]; ActionBtn.Content = toCall >= _humanPlayer.Chips ? "All-In" : $"Call (${toCall})"; }
            SetActionButtonsEnabled((_allPlayers[_currentPlayerIndex] == _humanPlayer) || _currentStage == GameStage.Showdown);
        }

        private void DisplayCardsOnPanel(List<Card> cards, StackPanel panel, bool showFaceUp)
        {
            panel.Children.Clear();
            foreach (Card card in cards)
            {
                string imageName = showFaceUp ? card.ImageFileName : "back.png";
                try
                {
                    BitmapImage bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.UriSource = new Uri(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", imageName), UriKind.Absolute); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.EndInit();
                    Border cardBorder = new Border { BorderThickness = new Thickness(3), BorderBrush = Brushes.Transparent, Margin = new Thickness(2), CornerRadius = new CornerRadius(4) };
                    Image cardImage = new Image { Source = bitmap, Stretch = Stretch.Uniform, Width = (panel == CommunityCardsPanel) ? 65 : 60, Height = (panel == CommunityCardsPanel) ? 95 : 85 };
                    cardBorder.Child = cardImage;
                    if (panel == PlayerHoleCardsPanel) { int cardIndex = panel.Children.Count; cardBorder.MouseLeftButtonDown += (s, e) => { if (_currentStage == GameStage.RiverSwap) SelectCardToSwap(cardIndex); }; }
                    panel.Children.Add(cardBorder);
                }
                catch { }
            }
        }

        private async Task SaveOfflineChipsToServer()
        {
            if (_humanPlayer.Name == "Player" || string.IsNullOrWhiteSpace(_humanPlayer.Name)) return;
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(2);
                    var content = new StringContent(JsonSerializer.Serialize(new { Username = _humanPlayer.Name, Chips = _humanPlayer.Chips }), Encoding.UTF8, "application/json");
                    await client.PostAsync("https://localhost:7140/api/auth/syncChips", content, _cts.Token);
                }
            }
            catch { }
        }

        private async void ExitBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_isExiting) return;
            if (MessageBox.Show(GameSettings.Translate("确定要返回主菜单吗？", "Return to Main Menu?"), GameSettings.Translate("离开确认", "Exit Confirmation"), MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No) return;
            _isExiting = true;
            _cts.Cancel();
            await SaveOfflineChipsToServer();
            var menu = Application.Current.Windows.OfType<GameMenu>().FirstOrDefault();
            if (menu != null) menu.Show();
            else new GameMenu().Show();
            this.Close();
        }

        protected override async void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_isExiting) return;
            e.Cancel = true;
            if (MessageBox.Show(GameSettings.Translate("确定要直接退出游戏吗？", "Are you sure you want to exit the game?"), GameSettings.Translate("退出确认", "Exit Confirmation"), MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                _isExiting = true;
                _cts.Cancel();
                await SaveOfflineChipsToServer();
                Application.Current.Shutdown();
            }
        }

        private void SettingsBtn_Click(object sender, RoutedEventArgs e) { SettingsWindow sw = new SettingsWindow(); sw.Owner = this; if (sw.ShowDialog() == true) ApplySettings(); }

        private void ApplySettings()
        {
            PokerTableArea.Background = GameSettings.TableBrush;
            BackBtn.Content = GameSettings.Translate("主菜单", "Main Menu");
            FoldBtn.Content = GameSettings.Translate("弃牌", "Fold");
            RaiseBtn.Content = GameSettings.Translate("加注", "Raise To");
            AllInBtn.Content = GameSettings.Translate("梭哈", "All-In");
            ActionBtn.Content = GameSettings.Translate("看牌/跟注", "Check/Call");
            DoSwapBtn.Content = GameSettings.Translate("换选中牌", "Swap Selected");
            PassSwapBtn.Content = GameSettings.Translate("不换", "Keep Hand");
            UpdateUIStats();
        }
    }
}