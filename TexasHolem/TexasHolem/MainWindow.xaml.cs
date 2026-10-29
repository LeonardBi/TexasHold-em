using System;
using System.Windows;
using RestSharp;

namespace TexasHoldemClient
{
    public partial class MainWindow : Window
    {
        private const string ServerUrl = "https://localhost:7140";

        private bool isNavigating = false;

        public MainWindow()
        {
            InitializeComponent();
        }

        // login
        private async void LoginBtn_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Logging in...";
            StatusText.Foreground = System.Windows.Media.Brushes.Gray;
            LoginBtn.IsEnabled = false;

            try
            {
                var client = new RestClient(ServerUrl);
                var request = new RestRequest("/api/Auth/login", Method.Post);

                request.AddQueryParameter("username", UsernameBox.Text);
                request.AddQueryParameter("password", PasswordBox.Password);

                var response = await client.ExecuteAsync(request);

                if (response.IsSuccessful)
                {
                    StatusText.Text = "Login Successful!";
                    StatusText.Foreground = System.Windows.Media.Brushes.Green;
                    MessageBox.Show($"Welcome Back, {UsernameBox.Text}!", "Success");
                    GameSession.CurrentUsername = UsernameBox.Text;
                    isNavigating = true;
                    GameMenu menuWin = new GameMenu();
                    menuWin.Show();
                    this.Close();
                }
                else
                {
                    StatusText.Text = $"Login Failed: {response.Content}";
                    StatusText.Foreground = System.Windows.Media.Brushes.Red;
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = "Error connecting to server";
                MessageBox.Show(ex.Message);
            }
            finally
            {
                LoginBtn.IsEnabled = true;
            }
        }

        // Register
        private void SignUpBtn_Click(object sender, RoutedEventArgs e)
        {
            isNavigating = true;
            RegisterWindow regWin = new RegisterWindow();
            regWin.Show();
            this.Close();
        }

        // Forgot Password
        private void ForgotPassword_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            isNavigating = true;
            ForgotPasswordWindow forgotWin = new ForgotPasswordWindow();
            forgotWin.Show();
            this.Close();
        }

        // Back to Menu
        private void BackToMenu_Click(object sender, RoutedEventArgs e)
        {
            isNavigating = true;
            GameMenu menuWin = new GameMenu();
            menuWin.Show();
            this.Close();
        }

        private void ExitBtn_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // Override the closing event to prevent accidental exits
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // if navigating to another window, allow closing without confirmation
            if (isNavigating)
            {
                base.OnClosing(e);
                return;
            }

            // if not navigating, show confirmation dialog
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