using System;
using System.Collections.Generic;
using System.Linq;
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

        private string selectedGroup = null;
        private List<string> groups = new List<string>();
        private Dictionary<string, List<string>> groupMessages =
            new Dictionary<string, List<string>>();

        public MainWindow()
        {
            InitializeComponent();
            UpdateUsernamePlaceholder();
            UpdateMessagePlaceholder();

            ConnectButton.IsEnabled = false;
            SendButton.IsEnabled = false;
        }

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

        private async void MessageTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && SendButton.IsEnabled)
            {
                e.Handled = true;
                await SendMessage();
            }
        }

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
                SendButton.IsEnabled = true;

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
                        string[] rawGroups = message
                            .Substring(7)
                            .Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        var groupList = rawGroups
                            .Select(g => g.Trim())
                            .Where(g => !string.IsNullOrWhiteSpace(g))
                            .ToArray();

                        Dispatcher.Invoke(() =>
                        {
                            groups.Clear();
                            GroupsPanel.Children.Clear();
                            foreach (var g in groupList)
                            {
                                AddGroup(g);
                            }
                        });
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

        private async Task SendMessage()
        {
            if (selectedGroup == null)
            {
                MessageBox.Show(
                    "Please select a group before sending a message.",
                    "Group Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }

            if (string.IsNullOrWhiteSpace(MessageTextBox.Text) || stream == null)
                return;

            string message = $"[{selectedGroup}] {username}: {MessageTextBox.Text.Trim()}";

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

        private void AppendChatMessage(string message)
        {
            Dispatcher.Invoke(() =>
            {
                string group = ExtractGroupName(message);
                if (group == null)
                    return;

                if (!groupMessages.ContainsKey(group))
                    groupMessages[group] = new List<string>();

                groupMessages[group].Add(message);

                if (group == selectedGroup)
                {
                    ChatListBox.Items.Add(message);
                    ChatListBox.ScrollIntoView(ChatListBox.Items[ChatListBox.Items.Count - 1]);
                }
            });
        }

        private string ExtractGroupName(string message)
        {
            if (message.StartsWith("[") && message.Contains("]"))
            {
                int endIndex = message.IndexOf(']');
                if (endIndex > 1)
                    return message.Substring(1, endIndex - 1);
            }
            return null;
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

        private void AddGroup(string groupName)
        {
            if (groups.Contains(groupName))
                return;

            groups.Add(groupName);
            string initials = GetGroupInitials(groupName);

            Button btn = new Button
            {
                Content = initials,
                Style = (Style)FindResource("GroupCircleButtonStyle"),
                Tag = groupName,
                Margin = new Thickness(5, 0, 5, 0),
                ToolTip = groupName,
            };

            btn.Click += GroupButton_Click;
            GroupsPanel.Children.Add(btn);
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

            return new string(parts.Take(2).Select(p => p[0]).ToArray()).ToUpper();
        }

        private void GroupButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string groupName)
            {
                SelectGroup(groupName);
            }
        }

        private void SelectGroup(string groupName)
        {
            selectedGroup = groupName;

            foreach (Button btn in GroupsPanel.Children)
            {
                btn.Background = (System.Windows.Media.Brush)(
                    new System.Windows.Media.BrushConverter().ConvertFrom(
                        (string)btn.Tag == groupName ? "#3A5AD9" : "#4E7FFF"
                    )
                );
            }

            ChatListBox.Items.Clear();

            if (groupMessages.TryGetValue(groupName, out List<string> messages))
            {
                foreach (string msg in messages)
                {
                    ChatListBox.Items.Add(msg);
                }

                if (messages.Count > 0)
                    ChatListBox.ScrollIntoView(ChatListBox.Items[ChatListBox.Items.Count - 1]);
            }

            SendButton.IsEnabled =
                !string.IsNullOrWhiteSpace(MessageTextBox.Text)
                && client != null
                && client.Connected;
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            await SendMessage();
        }
    }
}
