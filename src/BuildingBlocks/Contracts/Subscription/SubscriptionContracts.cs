namespace YiPix.BuildingBlocks.Contracts.Subscription;

public enum SubscriptionPlan
{
    Free,
    Monthly,
    Yearly,
    Lifetime
}

public enum SubscriptionStatus
{
    Active,
    Cancelled,
    Expired,
    PastDue,
    Suspended
}

public record SubscriptionDto(
    Guid Id,
    Guid UserId,
    SubscriptionPlan Plan,
    SubscriptionStatus Status,
    DateTime StartDate,
    DateTime? EndDate,
    string? PayPalSubscriptionId
);

public record CreateSubscriptionRequest(Guid UserId, SubscriptionPlan Plan, string PayPalSubscriptionId);
public record SubscriptionStatusResponse(bool IsActive, SubscriptionPlan Plan, DateTime? ExpiresAt);
