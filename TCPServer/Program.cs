using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace TCPServer;

class Program
{
    private static TcpListener _listener;
        private static readonly ConcurrentDictionary<string, TcpClient> _clients = new();
        private static bool _isRunning = true;

        static async Task Main(string[] args)
        {
            Console.Title = "TCP Server (2 clients)";
            Console.WriteLine("Starting TCP server...");

            _listener = new TcpListener(IPAddress.Any, 150);
            _listener.Start();
            Console.WriteLine("Server started on port 150. Waiting for connections...");

            // Start accepting clients
            var acceptTask = AcceptClientsAsync();

            // Start console commands handler
            var consoleTask = HandleConsoleCommandsAsync();

            await Task.WhenAll(acceptTask, consoleTask);
        }

        private static async Task AcceptClientsAsync()
        {
            while (_isRunning)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync();
                    var clientId = Guid.NewGuid().ToString();
                    _clients.TryAdd(clientId, client);
                    Console.WriteLine($"Client connected: {client.Client.RemoteEndPoint} [ID: {clientId}]");

                    // Start handling client in separate task
                    _ = HandleClientAsync(clientId, client);
                }
                catch (Exception ex)
                {
                    if (_isRunning)
                        Console.WriteLine($"Accept error: {ex.Message}");
                }
            }
        }

        private static async Task HandleClientAsync(string clientId, TcpClient client)
        {
            try
            {
                using (client)
                using (var stream = client.GetStream())
                using (var reader = new StreamReader(stream, Encoding.Unicode))
                {
                    while (_isRunning && client.Connected)
                    {
                        var message = await reader.ReadLineAsync();
                        if (message == null) break; // Client disconnected

                        Console.WriteLine($"Received from {clientId}: {message}");

                        if (message.Equals("exit", StringComparison.OrdinalIgnoreCase))
                        {
                            await SendToClient(client, "Goodbye!");
                            break;
                        }

                        // Broadcast to all clients
                        await BroadcastMessage($"{clientId}: {message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Client {clientId} error: {ex.Message}");
            }
            finally
            {
                _clients.TryRemove(clientId, out _);
                Console.WriteLine($"Client disconnected: {clientId}");
            }
        }

        private static async Task BroadcastMessage(string message)
        {
            var data = Encoding.Unicode.GetBytes(message + Environment.NewLine);
            
            foreach (var (id, client) in _clients)
            {
                try
                {
                    if (client.Connected)
                    {
                        await client.GetStream().WriteAsync(data, 0, data.Length);
                    }
                }
                catch
                {
                    Console.WriteLine($"Failed to send to client {id}");
                }
            }
        }

        private static async Task SendToClient(TcpClient client, string message)
        {
            var data = Encoding.Unicode.GetBytes(message + Environment.NewLine);
            await client.GetStream().WriteAsync(data, 0, data.Length);
        }

        private static async Task HandleConsoleCommandsAsync()
        {
            while (_isRunning)
            {
                var input = Console.ReadLine();
                if (input?.Equals("exit", StringComparison.OrdinalIgnoreCase) == true)
                {
                    _isRunning = false;
                    _listener.Stop();
                    Console.WriteLine("Server stopping...");
                    
                    // Disconnect all clients
                    foreach (var client in _clients.Values)
                    {
                        client.Close();
                    }
                    
                    break;
                }
                
                if (input?.Equals("list", StringComparison.OrdinalIgnoreCase) == true)
                {
                    Console.WriteLine($"Connected clients: {_clients.Count}");
                    foreach (var (id, _) in _clients)
                    {
                        Console.WriteLine($"- {id}");
                    }
                }
                
                if (input?.StartsWith("msg ") == true)
                {
                    var message = input.Substring(4);
                    await BroadcastMessage($"SERVER: {message}");
                }
            }
        }
}