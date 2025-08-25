using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using OpenNotification_API.Models;

namespace OpenNotification_API.Services;

public class NotificationWebSocketManager
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, WebSocket>> _guidConnections = new();
    private readonly ConcurrentDictionary<string, WebSocket> _countConnections = new();
    private long _notificationCount;
    private readonly string _counterFilePath = "notification_count.txt";
    private readonly object _fileLock = new();

    public NotificationWebSocketManager()
    {
        LoadCounterFromFile();
    }

    private void LoadCounterFromFile()
    {
        try
        {
            if (File.Exists(_counterFilePath))
            {
                var content = File.ReadAllText(_counterFilePath);
                if (long.TryParse(content.Trim(), out var count))
                {
                    _notificationCount = count;
                }
            }
            else
            {
                // Generate file with initial count of 0
                SaveCounterToFile();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading counter from file: {ex.Message}");
            _notificationCount = 0;
            SaveCounterToFile();
        }
    }

    private void SaveCounterToFile()
    {
        try
        {
            lock (_fileLock)
            {
                File.WriteAllText(_counterFilePath, _notificationCount.ToString());
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving counter to file: {ex.Message}");
        }
    }

    public void AddConnection(string guid, string connectionId, WebSocket webSocket)
    {
        var connections = _guidConnections.GetOrAdd(guid, _ => new ConcurrentDictionary<string, WebSocket>());
        connections.TryAdd(connectionId, webSocket);
    }

    public void RemoveConnection(string guid, string connectionId)
    {
        if (_guidConnections.TryGetValue(guid, out var connections))
        {
            connections.TryRemove(connectionId, out _);
            
            if (connections.IsEmpty)
            {
                _guidConnections.TryRemove(guid, out _);
            }
        }
    }

    public long GetNotificationCount() => _notificationCount;

    public async Task HandleCountWebSocketAsync(WebSocket webSocket)
    {
        var connectionId = Guid.NewGuid().ToString();
        _countConnections.TryAdd(connectionId, webSocket);

        try
        {
            // Send initial count immediately upon connection
            await SendCountToWebSocketAsync(webSocket, _notificationCount);

            var buffer = new byte[1024 * 4];
            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                    break;
                }
            }
        }
        finally
        {
            _countConnections.TryRemove(connectionId, out _);
        }
    }

    private async Task SendCountToWebSocketAsync(WebSocket webSocket, long count)
    {
        try
        {
            var message = JsonSerializer.Serialize(new { count });
            var buffer = Encoding.UTF8.GetBytes(message);
            await webSocket.SendAsync(
                new ArraySegment<byte>(buffer),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending count to WebSocket: {ex.Message}");
        }
    }

    private async Task BroadcastCountToAllCountConnectionsAsync()
    {
        var connectionsToRemove = new List<string>();
        var tasks = new List<Task>();

        foreach (var connection in _countConnections.ToList())
        {
            if (connection.Value.State == WebSocketState.Open)
            {
                tasks.Add(SendCountToWebSocketAsync(connection.Value, _notificationCount));
            }
            else
            {
                connectionsToRemove.Add(connection.Key);
            }
        }

        foreach (var connectionId in connectionsToRemove)
        {
            _countConnections.TryRemove(connectionId, out _);
        }

        if (tasks.Any())
        {
            await Task.WhenAll(tasks);
        }
    }

    public async Task SendNotificationToGuidAsync(Notification notification)
    {
        var guidString = notification.Guid.ToString();
        
        if (!_guidConnections.TryGetValue(guidString, out var connections))
            return;

        var message = JsonSerializer.Serialize(notification);
        var buffer = Encoding.UTF8.GetBytes(message);

        var tasks = new List<Task>();
        var connectionsToRemove = new List<string>();

        foreach (var connection in connections.ToList())
        {
            if (connection.Value.State == WebSocketState.Open)
            {
                tasks.Add(SendMessageAsync(connection.Value, buffer));
            }
            else
            {
                connectionsToRemove.Add(connection.Key);
            }
        }

        foreach (var connectionId in connectionsToRemove)
        {
            connections.TryRemove(connectionId, out _);
        }

        await Task.WhenAll(tasks);
        
        // Increment counter after successful send and save to file
        Interlocked.Increment(ref _notificationCount);
        SaveCounterToFile();
        
        // Broadcast updated count to all count WebSocket connections
        await BroadcastCountToAllCountConnectionsAsync();
    }

    private async Task SendMessageAsync(WebSocket webSocket, byte[] buffer)
    {
        try
        {
            await webSocket.SendAsync(
                new ArraySegment<byte>(buffer),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending message: {ex.Message}");
        }
    }
}
