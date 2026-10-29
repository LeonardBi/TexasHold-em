using RestSharp;
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
    public partial class RegisterWindow : Window
    {
        private const string ServerUrl = "https://localhost:7140";

        private bool isNavigating = false;

        public RegisterWindow()
        {
            InitializeComponent();
        }

        private void Q1Dropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CustomQ1Box == null) return;

            var selectedItem = Q1Dropdown.SelectedItem as ComboBoxItem;
            if (selectedItem != null && selectedItem.Content.ToString() == "--- Custom Question ---")
            {
                CustomQ1Box.Visibility = Visibility.Visible;
            }
            else
            {
                CustomQ1Box.Visibility = Visibility.Collapsed;
            }
        }

        private void Q2Dropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CustomQ2Box == null) return;

            var selectedItem = Q2Dropdown.SelectedItem as ComboBoxItem;
            if (selectedItem != null && selectedItem.Content.ToString() == "--- Custom Question ---")
            {
                CustomQ2Box.Visibility = Visibility.Visible;
            }
            else
            {
                CustomQ2Box.Visibility = Visibility.Collapsed;
            }
        }

        // Sign up button click event handler
        private async void SubmitRegBtn_Click(object sender, RoutedEventArgs e)
        {
            string username = RegUsernameBox.Text;
            string pwd = RegPasswordBox.Password;
            string confirmPwd = ConfirmPasswordBox.Password;

            string finalQ1 = GetQuestionContent(Q1Dropdown, CustomQ1Box);
            string a1 = A1Box.Text;

            string finalQ2 = GetQuestionContent(Q2Dropdown, CustomQ2Box);
            string a2 = A2Box.Text;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(pwd) ||
                string.IsNullOrWhiteSpace(a1) || string.IsNullOrWhiteSpace(a2))
            {
                RegStatusText.Text = "Please fill in all fields (including all answers).";
                return;
            }

            if (pwd != confirmPwd)
            {
                RegStatusText.Text = "Passwords do not match!";
                return;
            }

            RegStatusText.Text = "Registering...";
            SubmitRegBtn.IsEnabled = false;

            try
            {
                var client = new RestClient(ServerUrl);
                var request = new RestRequest("/api/Auth/register", Method.Post);

                request.AddQueryParameter("username", username);
                request.AddQueryParameter("password", pwd);
                request.AddQueryParameter("q1", finalQ1);
                request.AddQueryParameter("a1", a1);
                request.AddQueryParameter("q2", finalQ2);
                request.AddQueryParameter("a2", a2);

                var response = await client.ExecuteAsync(request);

                if (response.IsSuccessful)
                {
                    MessageBox.Show("Registration Successful! Please login.", "Success");

                    isNavigating = true; // registration successful, prepare to navigate back to login page

                    // Jump back to login page after successful registration
                    MainWindow loginWin = new MainWindow();
                    loginWin.Show();
                    this.Close();
                }
                else
                {
                    // Error message from server
                    RegStatusText.Text = $"Failed: {response.Content}";
                }
            }
            catch (Exception ex)
            {
                RegStatusText.Text = "Connection error to server.";
                MessageBox.Show(ex.Message);
            }
            finally
            {
                SubmitRegBtn.IsEnabled = true;
            }
        }

        private string GetQuestionContent(ComboBox dropdown, TextBox customBox)
        {
            var selected = dropdown.SelectedItem as ComboBoxItem;
            if (selected != null && selected.Content.ToString() == "--- Custom Question ---")
            {
                return customBox.Text; // if custom question is selected, return the content of the custom question text box
            }
            return selected?.Content.ToString() ?? ""; // otherwise, return the selected question from the dropdown
        }

        // Back to login window
        private void BackToLogin_Click(object sender, RoutedEventArgs e)
        {
            isNavigating = true;

            MainWindow loginWin = new MainWindow();
            loginWin.Show();
            this.Close();
        }

        // Close event handler to ensure the application exits completely when the window is closed
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (isNavigating)
            {
                base.OnClosing(e);
                return;
            }

            // if not navigating to another window, exit the entire application
            Application.Current.Shutdown();
            System.Diagnostics.Process.GetCurrentProcess().Kill();

            base.OnClosing(e);
        }
    }
}