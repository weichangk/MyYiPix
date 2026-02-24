namespace YiPix.BuildingBlocks.Contracts.Events;

public record IntegrationEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}

// Auth Events
public record UserCreatedEvent(Guid UserId, string Email, string? DisplayName) : IntegrationEvent;
public record UserLoggedInEvent(Guid UserId, string IpAddress, string DeviceType) : IntegrationEvent;

// Subscription Events
public record SubscriptionActivatedEvent(Guid UserId, Guid SubscriptionId, string PlanType) : IntegrationEvent;
public record SubscriptionCancelledEvent(Guid UserId, Guid SubscriptionId) : IntegrationEvent;
public record SubscriptionExpiredEvent(Guid UserId, Guid SubscriptionId) : IntegrationEvent;

// Payment Events
public record PaymentCompletedEvent(Guid UserId, Guid PaymentId, decimal Amount, string Currency) : IntegrationEvent;
public record PaymentFailedEvent(Guid UserId, Guid PaymentId, string Reason) : IntegrationEvent;

// Download Events
public record DownloadStartedEvent(Guid UserId, string Version, string Platform) : IntegrationEvent;
public record DownloadCompletedEvent(Guid UserId, string Version, string Platform) : IntegrationEvent;

// Task Events
public record TaskCreatedEvent(Guid TaskId, Guid UserId, string TaskType) : IntegrationEvent;
public record TaskCompletedEvent(Guid TaskId, Guid UserId, string TaskType) : IntegrationEvent;
public record TaskFailedEvent(Guid TaskId, Guid UserId, string Reason) : IntegrationEvent;
