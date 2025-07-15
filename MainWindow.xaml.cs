using System;
using System.Linq; // For Where()
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace InternalChatAppClientWPF
{
    public partial class MainWindow : Window
    {
        TcpClient client;
        NetworkStream stream;
        string username;

        // Group handling
        private string selectedGroup = null;
        private System.Collections.Generic.List<string> groups =
            new System.Collections.Generic.List<string>();

        public MainWindow()
        {
            InitializeComponent();
            UpdateUsernamePlaceholder();
            UpdateMessagePlaceholder();

            ConnectButton.IsEnabled = false;
            SendButton.IsEnabled = false;
        }

        // Handle username input placeholder visibility
        private void UsernameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateUsernamePlaceholder();
            ConnectButton.IsEnabled = !string.IsNullOrWhiteSpace(UsernameTextBox.Text);
        }

        private void UpdateUsernamePlaceholder()
        {
            UsernamePlaceholder.Visibility = string.IsNullOrEmpty(UsernameTextBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        // Handle message input placeholder visibility and Send button enabled state
        private void MessageTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateMessagePlaceholder();
            SendButton.IsEnabled =
                !string.IsNullOrWhiteSpace(MessageTextBox.Text)
                && client != null
                && client.Connected
                && selectedGroup != null;
        }

        private void UpdateMessagePlaceholder()
        {
            MessagePlaceholder.Visibility = string.IsNullOrEmpty(MessageTextBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        // Send message on Enter key press
        private async void MessageTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && SendButton.IsEnabled)
            {
                e.Handled = true;
                await SendMessage();
            }
        }

        // Connect button click event
        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            username = UsernameTextBox.Text.Trim();
            if (string.IsNullOrEmpty(username))
            {
                MessageBox.Show(
                    "Please enter your name before connecting.",
                    "Info",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
                return;
            }

            try
            {
                client = new TcpClient();
                await client.ConnectAsync("127.0.0.1", 5000);
                stream = client.GetStream();
                AppendChatMessage("[System]: Connected to server.");
                ConnectButton.IsEnabled = false;
                UsernameTextBox.IsEnabled = false;
                SendButton.IsEnabled = false; // will enable after group selected

                // Start receiving messages
                ReceiveMessages();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to connect: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        // Send button click event
        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            await SendMessage();
        }

        private async Task SendMessage()
        {
            if (
                string.IsNullOrWhiteSpace(MessageTextBox.Text)
                || stream == null
                || selectedGroup == null
            )
                return;

            string message = $"[{selectedGroup}] {username}: {MessageTextBox.Text.Trim()}";

            // Show your own message immediately
            AppendChatMessage(message);

            byte[] data = Encoding.UTF8.GetBytes(message);
            try
            {
                await stream.WriteAsync(data, 0, data.Length);
                MessageTextBox.Clear();
            }
            catch (Exception ex)
            {
                AppendChatMessage($"[System]: Error sending message: {ex.Message}");
            }
        }

        // Async loop to receive messages from server
        private async void ReceiveMessages()
        {
            byte[] buffer = new byte[1024];
            try
            {
                while (true)
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                    {
                        AppendChatMessage("[System]: Disconnected from server.");
                        break;
                    }
                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    if (message.StartsWith("GROUPS:"))
                    {
                        // Split by ',' and filter out empty entries manually
                        string[] rawGroups = message.Substring(7).Split(new char[] { ',' });
                        var groupList = rawGroups
                            .Where(g => !string.IsNullOrWhiteSpace(g))
                            .Select(g => g.Trim())
                            .ToArray();

                        foreach (var g in groupList)
                            Dispatcher.Invoke(() => AddGroup(g));
                    }
                    else
                    {
                        AppendChatMessage(message);
                    }
                }
            }
            catch (Exception ex)
            {
                AppendChatMessage($"[System]: Connection lost: {ex.Message}");
            }
            finally
            {
                Disconnect();
            }
        }

        private void AppendChatMessage(string message)
        {
            Dispatcher.Invoke(() =>
            {
                ChatListBox.Items.Add(message);
                ChatListBox.ScrollIntoView(ChatListBox.Items[ChatListBox.Items.Count - 1]);
            });
        }

        private void Disconnect()
        {
            Dispatcher.Invoke(() =>
            {
                ConnectButton.IsEnabled = true;
                UsernameTextBox.IsEnabled = true;
                SendButton.IsEnabled = false;
            });

            if (stream != null)
                stream.Close();
            if (client != null)
                client.Close();
        }

        // GROUP UI handling

        private void AddGroup(string groupName)
        {
            if (groups.Contains(groupName))
                return;
            groups.Add(groupName);

            string initials = GetGroupInitials(groupName);

            Button btn = new Button();
            btn.Content = initials;
            btn.Style = (Style)FindResource("GroupCircleButtonStyle");
            btn.Tag = groupName;
            btn.Margin = new Thickness(5, 0, 5, 0);
            btn.Click += GroupButton_Click;

            GroupsPanel.Children.Add(btn);

            if (selectedGroup == null)
            {
                SelectGroup(groupName);
            }
        }

        private string GetGroupInitials(string groupName)
        {
            string[] parts = groupName.Split(
                new char[] { ' ' },
                StringSplitOptions.RemoveEmptyEntries
            );
            if (parts.Length == 1)
            {
                return parts[0].Length <= 2
                    ? parts[0].ToUpper()
                    : parts[0].Substring(0, 2).ToUpper();
            }

            string initials = "";
            foreach (string part in parts)
            {
                if (initials.Length < 2)
                    initials += part[0];
                else
                    break;
            }
            return initials.ToUpper();
        }

        private void GroupButton_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            if (btn != null && btn.Tag is string groupName)
            {
                SelectGroup(groupName);
            }
        }

        private void SelectGroup(string groupName)
        {
            selectedGroup = groupName;

            foreach (Button btn in GroupsPanel.Children)
            {
                if ((string)btn.Tag == groupName)
                    btn.Background = (System.Windows.Media.Brush)(
                        new System.Windows.Media.BrushConverter().ConvertFrom("#3A5AD9")
                    ); // darker blue
                else
                    btn.Background = (System.Windows.Media.Brush)(
                        new System.Windows.Media.BrushConverter().ConvertFrom("#4E7FFF")
                    ); // default blue
            }

            // Clear chat messages when switching groups
            Dispatcher.Invoke(() => ChatListBox.Items.Clear());

            // Enable send button only if message box has text
            SendButton.IsEnabled = !string.IsNullOrWhiteSpace(MessageTextBox.Text);
        }
    }
}
