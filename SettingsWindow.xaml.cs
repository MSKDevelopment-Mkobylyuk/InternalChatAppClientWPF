using System;
using System.Windows;

namespace InternalChatAppClientWPF
{
    public partial class SettingsWindow : Window
    {
        // Properties to hold the IP address and port entered by the user
        public string IpAddress { get; private set; }
        public int Port { get; private set; }

        // Constructor - initializes the window and sets initial IP and Port values
        public SettingsWindow(string currentIp, int currentPort)
        {
            InitializeComponent();

            // Set the textboxes to show the current IP and port
            IpTextBox.Text = currentIp;
            PortTextBox.Text = currentPort.ToString();
        }

        // Called when the Save button is clicked
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Validate that the IP address textbox is not empty or whitespace
            if (string.IsNullOrWhiteSpace(IpTextBox.Text))
            {
                MessageBox.Show("Please enter a valid IP address.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return; // Stop if validation fails
            }

            // Validate the port number - it must be an integer between 1 and 65535
            if (!int.TryParse(PortTextBox.Text, out int port) || port < 1 || port > 65535)
            {
                MessageBox.Show("Please enter a valid port number (1-65535).", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return; // Stop if validation fails
            }

            // Save the validated IP and port into the properties
            IpAddress = IpTextBox.Text.Trim();
            Port = port;

            // Close the dialog and signal that it was successful
            DialogResult = true;
            Close();
        }

        // Called when the Cancel button is clicked - closes the window without saving
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false; // Indicate the operation was cancelled
            Close();
        }
    }
}
