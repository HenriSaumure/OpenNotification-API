namespace OpenNotification_API.Models;

public record Notification(
    Guid Guid,
    string Title,
    string? Description = null,
    string? PictureLink = null,
    bool IsAlert = false
);

public record NotificationRequest(
    Guid Guid,
    string Title,
    string? Description = null,
    string? PictureLink = null,
    bool IsAlert = false
)
{
    public Notification ToNotification() => new(
        Guid,
        Title,
        Description,
        PictureLink,
        IsAlert
    );
}
