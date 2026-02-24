using YiPix.BuildingBlocks.Common.Exceptions;
using YiPix.BuildingBlocks.Contracts.Events;
using YiPix.BuildingBlocks.Contracts.Subscription;
using YiPix.BuildingBlocks.EventBus.Abstractions;
using YiPix.Services.Subscription.Domain.Entities;
using YiPix.Services.Subscription.Infrastructure.Data;

namespace YiPix.Services.Subscription.Application;

public interface ISubscriptionAppService
{
    Task<SubscriptionDto> GetActiveSubscriptionAsync(Guid userId, CancellationToken ct = default);
    Task<List<SubscriptionDto>> GetUserSubscriptionsAsync(Guid userId, CancellationToken ct = default);
    Task<SubscriptionDto> CreateSubscriptionAsync(CreateSubscriptionRequest request, CancellationToken ct = default);
    Task<SubscriptionDto> ActivateByPayPalIdAsync(string paypalSubscriptionId, CancellationToken ct = default);
    Task CancelSubscriptionAsync(Guid subscriptionId, string? reason = null, CancellationToken ct = default);
    Task<SubscriptionStatusResponse> CheckStatusAsync(Guid userId, CancellationToken ct = default);
}

public class SubscriptionAppService : ISubscriptionAppService
{
    private readonly ISubscriptionRepository _repository;
    private readonly IEventBus _eventBus;

    public SubscriptionAppService(ISubscriptionRepository repository, IEventBus eventBus)
    {
        _repository = repository;
        _eventBus = eventBus;
    }

    public async Task<SubscriptionDto> GetActiveSubscriptionAsync(Guid userId, CancellationToken ct = default)
    {
        var sub = await _repository.GetActiveByUserIdAsync(userId, ct)
            ?? throw new NotFoundException("Subscription", userId);
        return MapToDto(sub);
    }

    public async Task<List<SubscriptionDto>> GetUserSubscriptionsAsync(Guid userId, CancellationToken ct = default)
    {
        var subs = await _repository.GetByUserIdAsync(userId, ct);
        return subs.Select(MapToDto).ToList();
    }

    public async Task<SubscriptionDto> CreateSubscriptionAsync(CreateSubscriptionRequest request, CancellationToken ct = default)
    {
        var existing = await _repository.GetActiveByUserIdAsync(request.UserId, ct);
        if (existing != null)
            throw new ConflictException("User already has an active subscription.");

        var subscription = new Domain.Entities.Subscription
        {
            UserId = request.UserId,
            Plan = request.Plan.ToString(),
            Status = "Active",
            StartDate = DateTime.UtcNow,
            EndDate = CalculateEndDate(request.Plan),
            PayPalSubscriptionId = request.PayPalSubscriptionId
        };

        await _repository.CreateAsync(subscription, ct);

        await _eventBus.PublishAsync(
            new SubscriptionActivatedEvent(request.UserId, subscription.Id, subscription.Plan), ct);

        return MapToDto(subscription);
    }

    public async Task<SubscriptionDto> ActivateByPayPalIdAsync(string paypalSubscriptionId, CancellationToken ct = default)
    {
        var sub = await _repository.GetByPayPalIdAsync(paypalSubscriptionId, ct)
            ?? throw new NotFoundException("Subscription", Guid.Empty);

        var oldStatus = sub.Status;
        sub.Status = "Active";
        await _repository.UpdateAsync(sub, ct);

        await _repository.AddHistoryAsync(new SubscriptionHistory
        {
            SubscriptionId = sub.Id,
            UserId = sub.UserId,
            FromStatus = oldStatus,
            ToStatus = "Active",
            Reason = "PayPal activation"
        }, ct);

        await _eventBus.PublishAsync(
            new SubscriptionActivatedEvent(sub.UserId, sub.Id, sub.Plan), ct);

        return MapToDto(sub);
    }

    public async Task CancelSubscriptionAsync(Guid subscriptionId, string? reason = null, CancellationToken ct = default)
    {
        var sub = await _repository.GetByIdAsync(subscriptionId, ct)
            ?? throw new NotFoundException("Subscription", subscriptionId);

        var oldStatus = sub.Status;
        sub.Status = "Cancelled";
        sub.CancelledAt = DateTime.UtcNow;
        sub.CancellationReason = reason;
        await _repository.UpdateAsync(sub, ct);

        await _repository.AddHistoryAsync(new SubscriptionHistory
        {
            SubscriptionId = sub.Id,
            UserId = sub.UserId,
            FromStatus = oldStatus,
            ToStatus = "Cancelled",
            Reason = reason
        }, ct);

        await _eventBus.PublishAsync(
            new SubscriptionCancelledEvent(sub.UserId, sub.Id), ct);
    }

    public async Task<SubscriptionStatusResponse> CheckStatusAsync(Guid userId, CancellationToken ct = default)
    {
        var sub = await _repository.GetActiveByUserIdAsync(userId, ct);
        if (sub == null)
            return new SubscriptionStatusResponse(false, SubscriptionPlan.Free, null);

        Enum.TryParse<SubscriptionPlan>(sub.Plan, out var plan);
        return new SubscriptionStatusResponse(sub.IsActive, plan, sub.EndDate);
    }

    private static DateTime? CalculateEndDate(SubscriptionPlan plan) => plan switch
    {
        SubscriptionPlan.Monthly => DateTime.UtcNow.AddMonths(1),
        SubscriptionPlan.Yearly => DateTime.UtcNow.AddYears(1),
        SubscriptionPlan.Lifetime => null,
        _ => null
    };

    private static SubscriptionDto MapToDto(Domain.Entities.Subscription s)
    {
        Enum.TryParse<SubscriptionPlan>(s.Plan, out var plan);
        Enum.TryParse<SubscriptionStatus>(s.Status, out var status);
        return new SubscriptionDto(s.Id, s.UserId, plan, status, s.StartDate, s.EndDate, s.PayPalSubscriptionId);
    }
}
