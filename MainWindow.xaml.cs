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
        TcpClient client; // TCP client used to connect to the chat server
        NetworkStream stream; // Stream for sending/receiving data
        string username; // Stores the current user's name

        private string selectedGroup = null; // Currently selected chat group
        private List<string> groups = new List<string>(); // List of available groups
        private Dictionary<string, List<string>> groupMessages =
            new Dictionary<string, List<string>>(); // Messages grouped by group name

        private string serverIp
        {
            get => Properties.Settings.Default.ServerIp;
            set
            {
                Properties.Settings.Default.ServerIp = value;
                Properties.Settings.Default.Save();
            }
        }

        private int serverPort
        {
            get => Properties.Settings.Default.ServerPort;
            set
            {
                Properties.Settings.Default.ServerPort = value;
                Properties.Settings.Default.Save();
            }
        }


        public MainWindow()
        {
            InitializeComponent();
            UpdateUsernamePlaceholder();
            UpdateMessagePlaceholder();

            // Disable buttons initially
            ConnectButton.IsEnabled = false;
            SendButton.IsEnabled = false;
        }

        // Updates the Connect button and placeholder visibility when username text changes
        private void UsernameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateUsernamePlaceholder();
            ConnectButton.IsEnabled = !string.IsNullOrWhiteSpace(UsernameTextBox.Text);
        }

        // Shows/hides the placeholder inside the username textbox
        private void UpdateUsernamePlaceholder()
        {
            UsernamePlaceholder.Visibility = string.IsNullOrEmpty(UsernameTextBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        // Updates the Send button and message placeholder when message input changes
        private void MessageTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateMessagePlaceholder();
            SendButton.IsEnabled =
                !string.IsNullOrWhiteSpace(MessageTextBox.Text)
                && client != null
                && client.Connected
                && selectedGroup != null;
        }

        // Shows/hides the placeholder inside the message textbox
        private void UpdateMessagePlaceholder()
        {
            MessagePlaceholder.Visibility = string.IsNullOrEmpty(MessageTextBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        // Sends message on Enter key press if input is valid
        private async void MessageTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && SendButton.IsEnabled)
            {
                e.Handled = true;
                await SendMessage();
            }
        }

        // Connects to the chat server on button click
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
                string ip = Properties.Settings.Default.ServerIp;
                int port = Properties.Settings.Default.ServerPort;

                client = new TcpClient();
                await client.ConnectAsync(ip, port); // Connect using settings
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

        // Listens for messages from the server
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
                        // Update group list if the server sends group info
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

        // Sends a chat message to the server
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
            AppendChatMessage(message); // Show message locally

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

        // Displays a message in the chat list for the current group
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

        // Extracts group name from message format: "[GroupName] User: Message"
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

        // Disconnects from the server and resets UI
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

        // Adds a group button to the UI
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

        // Generates initials from group name (e.g., "Dev Team" => "DT")
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

        // Handles group button click event
        private void GroupButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string groupName)
            {
                SelectGroup(groupName);
            }
        }

        // Sets the current active group and displays messages for that group
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

        // Handles Send button click
        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            await SendMessage();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow(serverIp, serverPort)
            {
                Owner = this
            };

            bool? result = settingsWindow.ShowDialog();

            if (result == true)
            {
                serverIp = settingsWindow.IpAddress;
                serverPort = settingsWindow.Port;

                MessageBox.Show($"Settings updated:\nIP Address: {serverIp}\nPort: {serverPort}", "Settings Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}