using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using OpenNotification_API.Models;

namespace OpenNotification_API.Services;

public class NotificationWebSocketManager
{
    // Dictionary: GUID -> List of WebSocket connections for that GUID
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, WebSocket>> _guidConnections = new();

    public async Task AddConnectionAsync(string guid, string connectionId, WebSocket webSocket)
    {
        var connections = _guidConnections.GetOrAdd(guid, _ => new ConcurrentDictionary<string, WebSocket>());
        connections.TryAdd(connectionId, webSocket);
        Console.WriteLine($"WebSocket connection added for GUID {guid}: {connectionId}");
    }

    public async Task RemoveConnectionAsync(string guid, string connectionId)
    {
        if (_guidConnections.TryGetValue(guid, out var connections))
        {
            connections.TryRemove(connectionId, out _);
            
            // Remove GUID entry if no more connections
            if (connections.IsEmpty)
            {
                _guidConnections.TryRemove(guid, out _);
            }
        }
        Console.WriteLine($"WebSocket connection removed for GUID {guid}: {connectionId}");
    }

    public async Task SendNotificationToGuidAsync(Notification notification)
    {
        var guidString = notification.Guid.ToString();
        
        if (!_guidConnections.TryGetValue(guidString, out var connections))
        {
            Console.WriteLine($"No clients connected for GUID: {guidString}");
            return;
        }

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
                // Mark closed connections for removal
                connectionsToRemove.Add(connection.Key);
            }
        }

        // Remove closed connections
        foreach (var connectionId in connectionsToRemove)
        {
            connections.TryRemove(connectionId, out _);
        }

        await Task.WhenAll(tasks);
        Console.WriteLine($"Notification sent to {tasks.Count} clients for GUID: {guidString}");
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

    public int GetTotalConnectionCount() => _guidConnections.Values.Sum(connections => connections.Count);
    
    public int GetConnectionCountForGuid(string guid) => 
        _guidConnections.TryGetValue(guid, out var connections) ? connections.Count : 0;

    public Dictionary<string, int> GetAllGuidConnections() =>
        _guidConnections.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Count);
}
