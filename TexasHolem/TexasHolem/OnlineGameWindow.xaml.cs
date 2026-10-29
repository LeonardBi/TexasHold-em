using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TexasHoldemClient
{
    public class PlayerInfo
    {
        public string Name { get; set; }
        public int Chips { get; set; }
        public int SeatIndex { get; set; }
        public string Status { get; set; }
        public int CurrentBet { get; set; }
        public bool IsSitOutNextHand { get; set; }
    }

    public partial class OnlineGameWindow : Window
    {
        private HubConnection _connection = null!;
        private string _myPlayerName;
        private bool _isExiting = false;
        private bool _isPlaying = false;

        private int _currentMaxBet = 0;
        private int _myCurrentBet = 0;

        private string _currentGameState = "Waiting";
        private int _selectedSwapIndex = -1;

        public OnlineGameWindow(string playerName)
        {
            InitializeComponent();
            _myPlayerName = playerName;
            Name0.Text = _myPlayerName;
            ConnectToServer();
            ApplySettings(); 
        }

        // Match the Chinese status from server to English for UI display
        private string TranslateStatus(string serverStatus)
        {
            if (!GameSettings.IsEnglish || string.IsNullOrEmpty(serverStatus)) return serverStatus;

            return serverStatus.Replace("等待中", "Waiting")
                               .Replace("已准备", "Ready")
                               .Replace("休息中", "Sitting Out")
                               .Replace("暂离预约", "Sit Out Next")
                               .Replace("已弃牌", "Folded")
                               .Replace("看牌", "Check")
                               .Replace("行动完毕", "Acted");
        }

        private async void ShowWarning(string message)
        {
            Dispatcher.Invoke(() => { WarningText.Text = message; WarningToast.Visibility = Visibility.Visible; });
            await Task.Delay(3000);
            Dispatcher.Invoke(() => WarningToast.Visibility = Visibility.Collapsed);
        }

        private void AddChatMessage(string message)
        {
            Dispatcher.Invoke(() => {
                ChatListBox.Items.Add(message);
                if (ChatListBox.Items.Count > 0) ChatListBox.ScrollIntoView(ChatListBox.Items[ChatListBox.Items.Count - 1]);
            });
        }

        private async void ConnectToServer()
        {
            // Build the persistent connection to the server's GameHub
            _connection = new HubConnectionBuilder().WithUrl("https://localhost:7140/gamehub").Build();

            // Register event listener for incoming broadcast messages
            _connection.On<string, string>("ReceiveMessage", (user, message) => AddChatMessage($"{user}: {message}"));

            // match Chinese system messages from server and translate to English if needed
            _connection.On<string>("SystemMessage", (sysMsg) =>
            {
                string translated = sysMsg;
                if (GameSettings.IsEnglish)
                {
                    translated = sysMsg.Replace("房间已满，无法加入", "Room is full, cannot join")
                                       .Replace("坐上了牌桌", "took a seat at the table")
                                       .Replace("离开了牌桌", "left the table")
                                       .Replace("选择了保留底牌", "kept their hand")
                                       .Replace("更换了一张底牌", "swapped a hole card")
                                       .Replace("弃牌了", "folded")
                                       .Replace("选择了看牌", "checked")
                                       .Replace("跟注 $", "called $")
                                       .Replace("All-In 梭哈了", "went All-In")
                                       .Replace("加注到 $", "raised to $")
                                       .Replace("终极换牌阶段 (River Swap)", "Final Swap Phase (River Swap)")
                                       .Replace("请各位点击【准备】开始新的一局", "Please click [Ready] to start a new round")
                                       .Replace("！", "!")
                                       .Replace("。", ".");
                }
                AddChatMessage($"[System] {translated}");
            });

            _connection.On<string, string>("ReceiveHoleCards", (card1Img, card2Img) =>
            {
                _isPlaying = true;
                _selectedSwapIndex = -1;
                Dispatcher.Invoke(() => {
                    DisplayCardsOnPanel(new string[] { card1Img, card2Img }, Cards0);
                    PreGamePanel.Visibility = Visibility.Collapsed;
                    SitOutBtn.Visibility = Visibility.Visible;
                    AudioManager.PlaySound("ShowCard.mp3");
                });
            });

            _connection.On<System.Collections.Generic.List<PlayerInfo>, int, int, string>("UpdateTable", (players, pot, maxBet, state) =>
            {
                Dispatcher.Invoke(() => {
                    PotText.Text = $"Pot: ${pot}";
                    _currentGameState = state;

                    GameStateText.Text = state == "Waiting" ?
                        GameSettings.Translate("等待玩家中...", "Waiting for Players...") :
                        GameSettings.Translate($"阶段: {state}", $"Stage: {state}");

                    _currentMaxBet = maxBet;

                    var me = players.FirstOrDefault(p => p.Name == _myPlayerName);
                    if (me != null)
                    {
                        _myCurrentBet = me.CurrentBet;
                        SitOutBtn.Content = me.IsSitOutNextHand ?
                            GameSettings.Translate("✔️ 准备 (取消休息)", "✔️ Ready (Cancel Sit Out)") :
                            GameSettings.Translate("⏸️ 下局休息", "⏸️ Sit Out Next Hand");
                        SitOutBtn.Background = me.IsSitOutNextHand ? System.Windows.Media.Brushes.ForestGreen : System.Windows.Media.Brushes.Gray;

                        if (state == "Waiting")
                        {
                            PreGamePanel.Visibility = Visibility.Visible;

                            if (me.Status.Contains("已准备") || me.Status.Contains("Ready"))
                            {
                                ReadyBtn.Content = GameSettings.Translate("取消准备", "Cancel Ready");
                            }
                            else
                            {
                                ReadyBtn.Content = GameSettings.Translate("准备", "Ready");
                            }
                        }
                        else
                        {
                            PreGamePanel.Visibility = Visibility.Collapsed;
                        }
                    }
                    UpdateOtherPlayersUI(players);
                });
            });

            // Outdated kick reason messages from server are now matched and translated to English if needed
            _connection.On<string>("KickedOut", async (reason) =>
            {
                await Dispatcher.Invoke(async () => {
                    _isExiting = true;
                    if (_connection != null) await _connection.StopAsync();

                    this.Visibility = Visibility.Collapsed;

                    bool isBankrupt = reason.Contains("insufficient") || reason.Contains("筹码") ||
                                      reason.Contains("破产") || reason.Contains("chips") || reason.Contains("0");

                    // Bankruptcy kick reason is now matched and translated to English for the message box
                    if (isBankrupt)
                    {
                        string title = GameSettings.Translate("破产补助", "Bankruptcy Grant");
                        string msg = GameSettings.Translate(
                            "您已破产被移出对局！\n不要灰心，系统为您发放了 $1000 的破产补助金，是否立即领取？",
                            "You are bankrupt and kicked out!\nDon't worry, the system offers a $1000 bankruptcy grant. Claim now?");

                        if (MessageBox.Show(msg, title, MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                        {
                            await RequestBankruptcyGrant(); 
                        }
                    }
                    else
                    {
                        MessageBox.Show(GameSettings.Translate(reason, "You have been kicked out."), GameSettings.Translate("提示", "Kicked Out"), MessageBoxButton.OK, MessageBoxImage.Error);
                    }

                    var menu = Application.Current.Windows.OfType<GameMenu>().FirstOrDefault() ?? new GameMenu();
                    menu.Show();
                    this.Close();
                });
            });

            _connection.On("ClearCommunityCards", () =>
            {
                Dispatcher.Invoke(() => { CommunityCardsPanel.Children.Clear(); Cards0.Children.Clear(); _isPlaying = false; });
            });

            _connection.On<System.Collections.Generic.List<string>>("ReceiveCommunityCards", (cardImages) =>
            {
                Dispatcher.Invoke(() => {
                    DisplayCardsOnPanel(cardImages.ToArray(), CommunityCardsPanel);
                    AudioManager.PlaySound("ShowCard.mp3");
                });
            });

            // Gamehub matches Chinese win reason messages from server and translates to English for display
            _connection.On<string, string>("ShowWinner", async (winText, reason) =>
            {
                Dispatcher.Invoke(() => {
                    string translatedMsg = winText;
                    string translatedReason = reason;

                    if (GameSettings.IsEnglish)
                    {
                        translatedMsg = winText.Replace("赢得了", "won").Replace("！", "!");
                        translatedReason = reason.Replace("其他玩家全部弃牌", "Everyone else folded")
                                                 .Replace("摊牌比大小获胜", "Best hand at showdown");
                    }

                    WinnerText.Text = translatedMsg;
                    WinnerReason.Text = translatedReason;

                    CenterBroadcastToast.Visibility = Visibility.Visible;
                    ActionPanel.Visibility = Visibility.Collapsed; SwapPanel.Visibility = Visibility.Collapsed;

                    if (winText.Contains(_myPlayerName)) AudioManager.PlaySound("Win.mp3");
                    else AudioManager.PlaySound("Lose.mp3");
                });
                await Task.Delay(3000);
                Dispatcher.Invoke(() => CenterBroadcastToast.Visibility = Visibility.Collapsed);
            });

            _connection.On<string, int>("UpdateTurn", (activePlayerName, amountToCall) =>
            {
                Dispatcher.Invoke(() =>
                {
                    // Reset all seat borders to default
                    Seat0.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#55FFFFFF"));
                    Seat1.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#55FFFFFF"));
                    Seat2.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#55FFFFFF"));
                    Seat3.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#55FFFFFF"));
                    Seat4.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#55FFFFFF"));
                    Seat5.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#55FFFFFF"));

                    if (activePlayerName == Name0.Text && Seat0.Visibility == Visibility.Visible) Seat0.BorderBrush = Brushes.Gold;
                    else if (activePlayerName == Name1.Text && Seat1.Visibility == Visibility.Visible) Seat1.BorderBrush = Brushes.Gold;
                    else if (activePlayerName == Name2.Text && Seat2.Visibility == Visibility.Visible) Seat2.BorderBrush = Brushes.Gold;
                    else if (activePlayerName == Name3.Text && Seat3.Visibility == Visibility.Visible) Seat3.BorderBrush = Brushes.Gold;
                    else if (activePlayerName == Name4.Text && Seat4.Visibility == Visibility.Visible) Seat4.BorderBrush = Brushes.Gold;
                    else if (activePlayerName == Name5.Text && Seat5.Visibility == Visibility.Visible) Seat5.BorderBrush = Brushes.Gold;

                    if (activePlayerName == _myPlayerName)
                    {
                        AudioManager.PlaySound("PlayersTurn.mp3");
                        if (_currentGameState == "RiverSwap")
                        {
                            AddChatMessage(GameSettings.Translate("[系统] 请点击底牌选择你要更换的牌,或选择不换!", "[System] Click a hole card to swap, or keep hand!"));
                            ActionPanel.Visibility = Visibility.Collapsed;
                            SwapPanel.Visibility = Visibility.Visible;
                            _selectedSwapIndex = -1;
                        }
                        else
                        {
                            CallBtn.Content = amountToCall > 0 ? $"Call (${amountToCall})" : GameSettings.Translate("Check 看牌", "Check");
                            AddChatMessage(GameSettings.Translate("[系统] 现在轮到你行动了!", "[System] It's your turn to act!"));
                            SwapPanel.Visibility = Visibility.Collapsed;
                            ActionPanel.Visibility = Visibility.Visible;
                        }
                    }
                    else
                    {
                        ActionPanel.Visibility = Visibility.Collapsed;
                        SwapPanel.Visibility = Visibility.Collapsed;
                    }
                });
            });

            try { await _connection.StartAsync(); await _connection.InvokeAsync("JoinGame", _myPlayerName); }
            catch (Exception ex) { MessageBox.Show(GameSettings.Translate($"无法连接到服务器: {ex.Message}", $"Failed to connect to server: {ex.Message}")); }
        }

        // Bankruptcy grant request is now sent to a dedicated API endpoint and handles responses with user-friendly messages
        private async Task RequestBankruptcyGrant()
        {
            try
            {
                var handler = new System.Net.Http.HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
                };

                using (System.Net.Http.HttpClient client = new System.Net.Http.HttpClient(handler))
                {
                    client.Timeout = TimeSpan.FromSeconds(5);
                    var payload = new { Username = _myPlayerName, Chips = 1000 };
                    string jsonString = System.Text.Json.JsonSerializer.Serialize(payload);
                    var content = new System.Net.Http.StringContent(jsonString, System.Text.Encoding.UTF8, "application/json");

                    var response = await client.PostAsync("https://localhost:7140/api/auth/syncChips", content);

                    if (response.IsSuccessStatusCode)
                    {
                        MessageBox.Show(
                            GameSettings.Translate("💰 领取成功！您已获得 $1000 筹码，可以重新加入对局了。", "💰 Claim successful! You received $1000 chips."),
                            GameSettings.Translate("提示", "Success"),
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    else
                    {
                        string errorDetail = await response.Content.ReadAsStringAsync();
                        MessageBox.Show($"发放失败！状态码: {response.StatusCode}\n详情: {errorDetail}", "后端报错");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"网络异常，补助发放失败: {ex.Message}", "网络错误");
            }
        }

        // Update UI for all players based on the latest game state
        private void UpdateOtherPlayersUI(System.Collections.Generic.List<PlayerInfo> players)
        {
            var me = players.FirstOrDefault(p => p.Name == _myPlayerName);
            if (me == null) return;

            Chips0.Text = $"${me.Chips}";
            Status0.Text = TranslateStatus(me.Status);
            if (me.CurrentBet > 0) Status0.Text += $" | Bet: ${me.CurrentBet}";

            Seat1.Visibility = Visibility.Collapsed; Seat2.Visibility = Visibility.Collapsed; Seat3.Visibility = Visibility.Collapsed; Seat4.Visibility = Visibility.Collapsed; Seat5.Visibility = Visibility.Collapsed;
            foreach (var player in players)
            {
                if (player.Name == _myPlayerName) continue;
                int uiPos = (player.SeatIndex - me.SeatIndex + 6) % 6;

                string displayStatus = TranslateStatus(player.Status);
                if (player.CurrentBet > 0) displayStatus += $" | Bet: ${player.CurrentBet}";

                // update each player's seat UI based on their relative position to me, and show their name, chips, and status
                switch (uiPos)
                {
                    case 1: Seat1.Visibility = Visibility.Visible; Name1.Text = player.Name; Chips1.Text = $"${player.Chips}"; Status1.Text = displayStatus; break;
                    case 2: Seat2.Visibility = Visibility.Visible; Name2.Text = player.Name; Chips2.Text = $"${player.Chips}"; Status2.Text = displayStatus; break;
                    case 3: Seat3.Visibility = Visibility.Visible; Name3.Text = player.Name; Chips3.Text = $"${player.Chips}"; Status3.Text = displayStatus; break;
                    case 4: Seat4.Visibility = Visibility.Visible; Name4.Text = player.Name; Chips4.Text = $"${player.Chips}"; Status4.Text = displayStatus; break;
                    case 5: Seat5.Visibility = Visibility.Visible; Name5.Text = player.Name; Chips5.Text = $"${player.Chips}"; Status5.Text = displayStatus; break;
                }
            }
        }

        // Display cards on the specified panel, and if it's the player's hole cards panel, add click events for swapping during River Swap phase
        private void DisplayCardsOnPanel(string[] imageNames, StackPanel panel)
        {
            panel.Children.Clear();
            foreach (string imageName in imageNames)
            {
                try
                {
                    string uriString = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", imageName);
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit(); bitmap.UriSource = new Uri(uriString, UriKind.Absolute); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.EndInit();

                    Border cardBorder = new Border { BorderThickness = new Thickness(3), BorderBrush = System.Windows.Media.Brushes.Transparent, Margin = new Thickness(2), CornerRadius = new CornerRadius(4) };
                    Image cardImage = new Image { Source = bitmap, Stretch = System.Windows.Media.Stretch.Uniform, Width = 60, Height = 85 };
                    cardBorder.Child = cardImage;

                    if (panel == Cards0)
                    {
                        int cardIndex = panel.Children.Count;
                        cardBorder.MouseLeftButtonDown += (s, e) => {
                            if (_currentGameState == "RiverSwap") SelectCardToSwap(cardIndex);
                        };
                    }
                    panel.Children.Add(cardBorder);
                }
                catch { }
            }
        }

        private void SelectCardToSwap(int index)
        {
            _selectedSwapIndex = index;
            foreach (Border b in Cards0.Children.OfType<Border>()) b.BorderBrush = System.Windows.Media.Brushes.Transparent;
            if (index >= 0 && index < Cards0.Children.Count)
            {
                ((Border)Cards0.Children[index]).BorderBrush = System.Windows.Media.Brushes.Yellow;
            }
        }

        private void MessageTextBox_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) SendBtn_Click(null, null); }
        private async void SendBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(MessageTextBox.Text)) return;
            try { await _connection.InvokeAsync("SendMessage", _myPlayerName, MessageTextBox.Text); MessageTextBox.Clear(); } catch { }
        }

        private async void ReadyBtn_Click(object sender, RoutedEventArgs e) { try { await _connection.InvokeAsync("ToggleReady"); } catch { } }
        private async void SitOutBtn_Click(object sender, RoutedEventArgs e) { try { await _connection.InvokeAsync("ToggleSitOut"); } catch { } }

        private async void FoldBtn_Click(object sender, RoutedEventArgs e)
        {
            try { ActionPanel.Visibility = Visibility.Collapsed; AudioManager.PlaySound("Fold.mp3"); await _connection.InvokeAsync("PlayerAction", "Fold 弃牌", 0); } catch { }
        }

        private async void CallBtn_Click(object sender, RoutedEventArgs e)
        {
            try { ActionPanel.Visibility = Visibility.Collapsed; if (_currentMaxBet - _myCurrentBet > 0) AudioManager.PlaySound("CallRaise.mp3"); else AudioManager.PlaySound("Check.mp3"); await _connection.InvokeAsync("PlayerAction", "Call 跟注", 0); } catch { }
        }

        private async void AllInBtn_Click(object sender, RoutedEventArgs e)
        {
            try { if (int.TryParse(Chips0.Text.Replace("$", ""), out int myChips) && myChips > 0) { ActionPanel.Visibility = Visibility.Collapsed; AudioManager.PlaySound("Allin.mp3"); await _connection.InvokeAsync("PlayerAction", "All-In",  _myCurrentBet + myChips); } } catch { }
        }

        private async void RaiseBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (int.TryParse(RaiseAmountInput.Text.Trim(), out int targetBet) && targetBet > 0)
                {
                    if (int.TryParse(Chips0.Text.Replace("$", ""), out int myChips))
                    {
                        if (targetBet <= _currentMaxBet) { ShowWarning(GameSettings.Translate($"加注额必须大于当前的最高下注 (${_currentMaxBet})！", $"Raise must be greater than current max bet (${_currentMaxBet})!")); return; }
                        if (targetBet - _myCurrentBet > myChips) { ShowWarning(GameSettings.Translate($"资金不足以加注到 ${targetBet}，请直接使用 All-In！", $"Not enough chips to raise to ${targetBet}, please All-In!")); return; }
                        ActionPanel.Visibility = Visibility.Collapsed;
                        AudioManager.PlaySound("CallRaise.mp3");
                        await _connection.InvokeAsync("PlayerAction", "Raise 加注", targetBet);
                    }
                }
                else { ShowWarning(GameSettings.Translate("请输入合法的加注数字！", "Please enter a valid raise amount!")); }
            }
            catch { }
        }

        private async void DoSwapBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedSwapIndex == -1) { ShowWarning(GameSettings.Translate("请先用鼠标点击选中一张要换掉的底牌！", "Please select a hole card to swap!")); return; }
            try { SwapPanel.Visibility = Visibility.Collapsed; await _connection.InvokeAsync("PlayerAction", $"SwapCard_{_selectedSwapIndex}", 0); } catch { }
        }

        private async void PassSwapBtn_Click(object sender, RoutedEventArgs e)
        {
            try { SwapPanel.Visibility = Visibility.Collapsed; await _connection.InvokeAsync("PlayerAction", "KeepHand", 0); } catch { }
        }

        private async void BackBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_isExiting) return;
            string msg = _isPlaying ? GameSettings.Translate("游戏正在进行中，离开将扣除已下注筹码！确定离开？", "Game in progress. Leaving will forfeit your bet. Leave?") : GameSettings.Translate("确定返回主菜单？", "Return to Main Menu?");
            if (MessageBox.Show(msg, GameSettings.Translate("离开确认", "Exit Confirmation"), MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No) return;

            _isExiting = true;
            if (_connection != null) { await _connection.StopAsync(); }
            var menu = Application.Current.Windows.OfType<GameMenu>().FirstOrDefault() ?? new GameMenu(); menu.Show(); this.Close();
        }

        protected override async void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_isExiting) return;
            string msg = _isPlaying ? GameSettings.Translate("游戏正在进行中，关闭将扣除已下注筹码！确定关闭？", "Game in progress. Closing will forfeit your bet. Close?") : GameSettings.Translate("确定直接关闭？", "Are you sure you want to close?");
            if (MessageBox.Show(msg, GameSettings.Translate("关闭确认", "Close Confirmation"), MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No) { e.Cancel = true; }
            else { _isExiting = true; if (_connection != null) { await _connection.StopAsync(); } var menu = Application.Current.Windows.OfType<GameMenu>().FirstOrDefault() ?? new GameMenu(); menu.Show(); }
        }

        private void SettingsBtn_Click(object sender, RoutedEventArgs e) { SettingsWindow sw = new SettingsWindow(); sw.Owner = this; if (sw.ShowDialog() == true) { ApplySettings(); } }

        private void ApplySettings()
        {
            PokerTableArea.Background = GameSettings.TableBrush;
            BackBtn.Content = GameSettings.Translate("主菜单", "Main Menu");
            FoldBtn.Content = GameSettings.Translate("弃牌", "Fold");
            CallBtn.Content = GameSettings.Translate("看牌/跟注", "Check/Call");
            RaiseBtn.Content = GameSettings.Translate("加注到", "Raise To");
            AllInBtn.Content = GameSettings.Translate("梭哈", "All-In");
            ReadyBtn.Content = GameSettings.Translate("✔️ 准备 (取消休息)", "✔️ Ready (Cancel Sit Out)");
            DoSwapBtn.Content = GameSettings.Translate("Swap 换选中牌", "Swap Selected");
            PassSwapBtn.Content = GameSettings.Translate("Keep 不换", "Keep Hand");

            string currentSitOutText = SitOutBtn.Content?.ToString() ?? "";
            if (currentSitOutText.Contains("取消") || currentSitOutText.Contains("Cancel")) { SitOutBtn.Content = GameSettings.Translate("✅ 取消暂离", "✅ Cancel Sit Out"); }
            else { SitOutBtn.Content = GameSettings.Translate("⏸️ 下局休息", "⏸️ Sit Out Next Hand"); }
        }
    }
}