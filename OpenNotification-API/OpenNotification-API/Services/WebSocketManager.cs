using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using OpenNotification_API.Models;

namespace OpenNotification_API.Services;

public class NotificationWebSocketManager
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, WebSocket>> _guidConnections = new();

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
