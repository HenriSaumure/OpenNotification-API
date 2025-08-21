using System.Net.WebSockets;
using OpenNotification_API.Models;
using OpenNotification_API.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddOpenApi();
builder.Services.AddSingleton<NotificationWebSocketManager>();

// Configure Kestrel to listen on HTTP and HTTPS
builder.WebHost.ConfigureKestrel(options =>
{
    // HTTPS (default)
    options.ListenLocalhost(7129, listenOptions =>
    {
        listenOptions.UseHttps(); // Dev certificate
    });

    // HTTP for local testing
    options.ListenLocalhost(5193); // No HTTPS, plain HTTP
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
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
        
        await wsManager.AddConnectionAsync(guid, connectionId, webSocket);
        
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
            await wsManager.RemoveConnectionAsync(guid, connectionId);
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
})
.WithName("SendNotification");

// Health check endpoints
app.MapGet("/status", (NotificationWebSocketManager wsManager) =>
{
    return Results.Ok(new
    {
        TotalConnectedClients = wsManager.GetTotalConnectionCount(),
        GuidConnections = wsManager.GetAllGuidConnections()
    });
})
.WithName("GetStatus");

app.MapGet("/status/{guid}", (string guid, NotificationWebSocketManager wsManager) =>
{
    if (!Guid.TryParse(guid, out var parsedGuid))
        return Results.BadRequest("Invalid GUID format");

    return Results.Ok(new
    {
        Guid = guid,
        ConnectedClients = wsManager.GetConnectionCountForGuid(guid)
    });
})
.WithName("GetGuidStatus");

app.Run();
