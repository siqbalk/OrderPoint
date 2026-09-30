namespace Notifications.Contracts;

/// <summary>
/// Notifications publishes no events and no synchronous contracts — it only
/// listens. Its public surface is its permission names.
/// </summary>
public static class NotificationsPermissions
{
    public const string NotificationsRead = "notifications:notification:read";
}
