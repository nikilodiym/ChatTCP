using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace TcpClientApp
{
    public partial class MainWindow : Window
    {
        private TcpClient _client;
        private NetworkStream _stream;
        private bool _isConnected;

        public MainWindow()
        {
            InitializeComponent();
            SetConnectionState(false);
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isConnected)
            {
                Disconnect();
                return;
            }

            if (string.IsNullOrWhiteSpace(NameTextBox.Text))
            {
                MessageBox.Show("Please enter your name");
                return;
            }

            try
            {
                _client = new TcpClient();
                await _client.ConnectAsync(IpTextBox.Text, int.Parse(PortTextBox.Text));
                _stream = _client.GetStream();

                _isConnected = true;
                SetConnectionState(true);
                LogMessage("Connected to server");
                
                // Start receiving messages
                _ = ReceiveMessagesAsync();

                // Send client name
                await SendMessageAsync($"NAME|{NameTextBox.Text}");
            }
            catch (Exception ex)
            {
                LogMessage($"Connection error: {ex.Message}");
                Disconnect();
            }
        }

        private async Task ReceiveMessagesAsync()
        {
            var buffer = new byte[4096];
            try
            {
                using (var reader = new StreamReader(_stream, Encoding.Unicode))
                {
                    while (_isConnected)
                    {
                        var message = await reader.ReadLineAsync();
                        if (message == null) break;

                        Dispatcher.Invoke(() => LogMessage(message));
                    }
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => LogMessage($"Receive error: {ex.Message}"));
            }
            finally
            {
                Dispatcher.Invoke(Disconnect);
            }
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            await SendMessageAsync(MessageTextBox.Text);
            MessageTextBox.Clear();
            MessageTextBox.Focus();
        }

        private async void MessageTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter && _isConnected)
            {
                await SendMessageAsync(MessageTextBox.Text);
                MessageTextBox.Clear();
            }
        }

        private async Task SendMessageAsync(string message)
        {
            if (!_isConnected || string.IsNullOrWhiteSpace(message)) return;

            try
            {
                var data = Encoding.Unicode.GetBytes(message + Environment.NewLine);
                await _stream.WriteAsync(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                LogMessage($"Send error: {ex.Message}");
                Disconnect();
            }
        }

        private void Disconnect()
        {
            try
            {
                _isConnected = false;
                _stream?.Close();
                _client?.Close();
            }
            catch { /* Ignore */ }
            finally
            {
                SetConnectionState(false);
                LogMessage("Disconnected from server");
            }
        }

        private void SetConnectionState(bool connected)
        {
            ConnectButton.Content = connected ? "Disconnect" : "Connect";
            StatusText.Text = connected ? $"Connected as {NameTextBox.Text}" : "Disconnected";
            SendButton.IsEnabled = connected;
            NameTextBox.IsEnabled = !connected;
        }

        private void LogMessage(string message)
        {
            LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
            LogTextBox.ScrollToEnd();
        }

        protected override void OnClosed(EventArgs e)
        {
            Disconnect();
            base.OnClosed(e);
        }
    }
}