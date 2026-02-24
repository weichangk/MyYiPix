namespace YiPix.BuildingBlocks.Contracts.Payment;

public enum PaymentStatus
{
    Pending,
    Completed,
    Failed,
    Refunded
}

public record PaymentDto(
    Guid Id,
    Guid UserId,
    decimal Amount,
    string Currency,
    PaymentStatus Status,
    string PayPalOrderId,
    DateTime CreatedAt
);

public record CreatePaymentRequest(Guid UserId, string PlanId, string ReturnUrl, string CancelUrl);
public record PayPalWebhookPayload(string EventType, string ResourceId, object Resource);
public record CapturePaymentRequest(string PayPalOrderId);
