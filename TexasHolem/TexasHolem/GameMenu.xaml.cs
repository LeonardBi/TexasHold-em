using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace TexasHoldemClient
{
    public partial class GameMenu : Window
    {
        private bool isNavigating = false;
        private bool _isSafeToClose = false;

        public GameMenu()
        {
            InitializeComponent();
            UpdateUIState(); // initialize welcome text and auth button based on login state
            ApplySettings(); // initialize button texts based on current language settings
            AudioManager.PlayBGM("BGM.mp3");
        }

        private void UpdateUIState()
        {
            if (GameSession.IsLoggedIn)
            {
                WelcomeText.Text = GameSettings.Translate($"欢迎, {GameSession.CurrentUsername}!", $"Welcome, {GameSession.CurrentUsername}!");
                AuthBtn.Content = GameSettings.Translate("登出", "Logout");
            }
            else
            {
                WelcomeText.Text = GameSettings.Translate("欢迎, 游客!", "Welcome, Guest!");
                AuthBtn.Content = GameSettings.Translate("登录", "Login");
            }
        }

        public void ApplySettings()
        {
            // update button texts based on current language settings
            OnlineBtn.Content = GameSettings.Translate("在线联机", "Online Game");
            OfflineBtn.Content = GameSettings.Translate("单机训练", "Offline Game");
            SettingsMenuBtn.Content = GameSettings.Translate("游戏设置", "Settings");
            ExitGameBtn.Content = GameSettings.Translate("退出游戏", "Exit");

            // update welcome text and auth button in case language changed while logged in
            UpdateUIState();
        }

        // when the menu becomes visible again, re-apply settings to refresh texts
        private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (this.Visibility == Visibility.Visible)
            {
                ApplySettings();
            }
        }

        private void OnlineGameBtn_Click(object sender, RoutedEventArgs e)
        {
            if (GameSession.IsLoggedIn)
            {
                OnlineGameWindow onlineWindow = new OnlineGameWindow(GameSession.CurrentUsername);
                onlineWindow.Show();
                this.Hide();
            }
            else
            {
                MessageBox.Show(GameSettings.Translate("请先登录才能进行在线游戏！", "Please login first to play online!"),
                                GameSettings.Translate("需要登录", "Login Required"), MessageBoxButton.OK, MessageBoxImage.Warning);

                // not logged in, navigate to login page
                isNavigating = true;
                MainWindow loginWin = new MainWindow();
                loginWin.Show();
                this.Close();
            }
        }

        private async void AuthBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var client = new System.Net.Http.HttpClient())
                {
                    await client.PostAsync($"https://localhost:7140/api/auth/logout?username={GameSession.CurrentUsername}", null);
                }
            }
            catch { }

            if (GameSession.IsLoggedIn)
            {
                // if currently logged in, perform logout action
                GameSession.CurrentUsername = string.Empty; // clear login state
                UpdateUIState(); // refresh welcome text and button
                MessageBox.Show(GameSettings.Translate("您已成功退出登录。", "You have successfully logged out."),
                                GameSettings.Translate("登出", "Logout"));
            }
            else
            {
                // if currently not logged in, navigate to login page
                isNavigating = true;
                MainWindow loginWin = new MainWindow();
                loginWin.Show();
                this.Close();
            }
        }

        private void OfflineGameBtn_Click(object sender, RoutedEventArgs e)
        {
            string playerName = string.IsNullOrWhiteSpace(GameSession.CurrentUsername) ? "Player" : GameSession.CurrentUsername;

            OfflineGameWindow offlineWindow = new OfflineGameWindow(playerName);
            offlineWindow.Show();
            this.Hide();
        }

        private void SettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            SettingsWindow sw = new SettingsWindow();
            sw.Owner = this;
            if (sw.ShowDialog() == true)
            {
                ApplySettings();
            }
        }

        private void ExitBtn_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        protected override async void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (isNavigating)
            {
                base.OnClosing(e);
                return;
            }
            // if navigating to another page, close without asking
            if (!_isSafeToClose && !string.IsNullOrEmpty(GameSession.CurrentUsername))
            {
                e.Cancel = true;

                try
                {
                    using (var client = new System.Net.Http.HttpClient())
                    {
                        var handler = new System.Net.Http.HttpClientHandler();
                        handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => { return true; };

                        using (var secureClient = new System.Net.Http.HttpClient(handler))
                        {
                            string url = $"https://localhost:7140/api/auth/logout?username={GameSession.CurrentUsername}";
                            var response = await secureClient.PostAsync(url, null);

                            if (!response.IsSuccessStatusCode)
                            {
                                MessageBox.Show($"Error: {response.StatusCode}", "Request Message");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Request Failed：\n" + ex.Message, "Request Message");
                }
                finally
                {
                    _isSafeToClose = true;

                    Application.Current.Shutdown();
                }
            }
            else
            {
                MessageBoxResult result = MessageBox.Show("Are you sure you want to exit the game?",
                                                      "Exit Confirmation",
                                                      MessageBoxButton.YesNo,
                                                      MessageBoxImage.Question);

                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                }
                else
                {
                    Application.Current.Shutdown();
                    System.Diagnostics.Process.GetCurrentProcess().Kill();
                }
                base.OnClosing(e);
            }
        }
    }
}