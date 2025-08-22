namespace OpenNotification_API.Models;

public record Notification(
    Guid Guid,
    string Title,
    string? Description = null,
    string? PictureLink = null,
    string? Icon = null,
    string? ActionLink = null,
    bool IsAlert = false
);


public record NotificationRequest(
    Guid Guid,
    string Title,
    string? Description = null,
    string? PictureLink = null,
    string? Icon = null,
    string? ActionLink = null,
    bool IsAlert = false
)
{
    public Notification ToNotification() => new(
        Guid,
        Title,
        Description,
        PictureLink,
        Icon,
        ActionLink,
        IsAlert
    );
}
