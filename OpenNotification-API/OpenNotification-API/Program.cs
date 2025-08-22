using System.Net.WebSockets;
using OpenNotification_API.Models;
using OpenNotification_API.Services;

var builder = WebApplication.CreateBuilder(args);

// Add only essential services
builder.Services.AddSingleton<NotificationWebSocketManager>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseWebSockets();

// WebSocket endpoint: /ws/{guid}
app.Map("/ws/{guid}", async (string guid, HttpContext context, NotificationWebSocketManager wsManager) =>
{
    if (!Guid.TryParse(guid, out var parsedGuid))
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("Invalid GUID format");
        return;
    }

    if (context.WebSockets.IsWebSocketRequest)
    {
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        var connectionId = Guid.NewGuid().ToString();
        
        wsManager.AddConnection(guid, connectionId, webSocket);
        
        try
        {
            var buffer = new byte[1024 * 4];
            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                }
            }
        }
        finally
        {
            wsManager.RemoveConnection(guid, connectionId);
        }
    }
    else
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("WebSocket connection required");
    }
});

// Notification API endpoint
app.MapPost("/notification", async (NotificationRequest request, NotificationWebSocketManager wsManager) =>
{
    if (string.IsNullOrWhiteSpace(request.Title))
        return Results.BadRequest("Title is required");

    if (request.Guid == Guid.Empty)
        return Results.BadRequest("GUID is required");

    var notification = request.ToNotification();
    await wsManager.SendNotificationToGuidAsync(notification);
    return Results.Ok(notification);
});

app.Run();
