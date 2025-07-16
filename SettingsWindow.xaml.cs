using System;
using System.Windows;

namespace InternalChatAppClientWPF
{
    public partial class SettingsWindow : Window
    {
        public string IpAddress { get; private set; }
        public int Port { get; private set; }

        public SettingsWindow(string currentIp, int currentPort)
        {
            InitializeComponent();

            IpTextBox.Text = currentIp;
            PortTextBox.Text = currentPort.ToString();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Validate IP and port
            if (string.IsNullOrWhiteSpace(IpTextBox.Text))
            {
                MessageBox.Show("Please enter a valid IP address.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(PortTextBox.Text, out int port) || port < 1 || port > 65535)
            {
                MessageBox.Show("Please enter a valid port number (1-65535).", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IpAddress = IpTextBox.Text.Trim();
            Port = port;

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

    }
}