using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
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
    public partial class ForgotPasswordWindow : Window
    {
        private const string ServerUrl = "https://localhost:7140";
        private string verifiedUsername = "";

        private bool isNavigating = false;

        public ForgotPasswordWindow()
        {
            InitializeComponent();
        }

        // get username
        private async void SearchUserBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(UsernameBox.Text)) return;

            Step1Status.Text = "Searching...";
            var client = new RestClient(ServerUrl);
            var request = new RestRequest("/api/Auth/get-questions", Method.Get);
            request.AddQueryParameter("username", UsernameBox.Text);

            var response = await client.ExecuteAsync(request);

            if (response.IsSuccessful)
            {
                var doc = JsonDocument.Parse(response.Content ?? "{}");
                string fetchedQ1 = doc.RootElement.GetProperty("q1").GetString();
                string fetchedQ2 = doc.RootElement.GetProperty("q2").GetString();

                Q1Text.Text = $"Question 1: {fetchedQ1}";
                Q2Text.Text = $"Question 2: {fetchedQ2}";

                QuestionsPanel.Visibility = Visibility.Visible;
                Step1Status.Text = "";
                verifiedUsername = UsernameBox.Text; // store for later use 
            }
            else
            {
                Step1Status.Text = "Username not found.";
                QuestionsPanel.Visibility = Visibility.Collapsed;
            }
        }

        // verify answers
        private async void VerifyBtn_Click(object sender, RoutedEventArgs e)
        {
            var client = new RestClient(ServerUrl);
            var request = new RestRequest("/api/Auth/verify-answers", Method.Post);
            request.AddQueryParameter("username", verifiedUsername);
            request.AddQueryParameter("a1", A1Box.Text);
            request.AddQueryParameter("a2", A2Box.Text);

            var response = await client.ExecuteAsync(request);

            if (response.IsSuccessful)
            {
                // verified successfully, move to step 2
                Step1Panel.Visibility = Visibility.Collapsed;
                Step2Panel.Visibility = Visibility.Visible;
            }
            else
            {
                Step1Status.Text = "Incorrect answers. Please try again.";
            }
        }

        // submit new password  
        private async void ResetPwdBtn_Click(object sender, RoutedEventArgs e)
        {
            if (NewPwdBox.Password != ConfirmPwdBox.Password)
            {
                Step2Status.Text = "Passwords do not match!";
                return;
            }
            if (string.IsNullOrWhiteSpace(NewPwdBox.Password))
            {
                Step2Status.Text = "Password cannot be empty!";
                return;
            }

            var client = new RestClient(ServerUrl);
            var request = new RestRequest("/api/Auth/reset-password", Method.Post);
            request.AddQueryParameter("username", verifiedUsername);
            request.AddQueryParameter("newPassword", NewPwdBox.Password);

            var response = await client.ExecuteAsync(request);

            if (response.IsSuccessful)
            {
                MessageBox.Show("Password reset successful! Please login with your new password.", "Success");
                BackToLogin_Click(null, null);
            }
            else
            {
                Step2Status.Text = "Error resetting password.";
            }
        }

        private void BackToLogin_Click(object sender, RoutedEventArgs e)
        {
            isNavigating = true;

            MainWindow loginWin = new MainWindow();
            loginWin.Show();
            this.Close();
        }

        // Override the closing event to ensure the application exits completely when the window is closed
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (isNavigating)
            {
                base.OnClosing(e);
                return;
            }

            Application.Current.Shutdown();
            System.Diagnostics.Process.GetCurrentProcess().Kill();

            base.OnClosing(e);
        }
    }
}