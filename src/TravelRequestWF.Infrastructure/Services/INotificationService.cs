namespace TravelRequestWF.Infrastructure.Services;

public interface INotificationService
{
    Task NotifyRequestSubmittedAsync(NotificationPayload payload);
    Task NotifyRequestStatusChangedAsync(NotificationPayload payload);
}
