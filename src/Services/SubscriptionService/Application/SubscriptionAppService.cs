using Microsoft.Extensions.Logging;
using YiPix.BuildingBlocks.Common.Exceptions;
using YiPix.BuildingBlocks.Contracts.Events;
using YiPix.BuildingBlocks.Contracts.Subscription;
using YiPix.BuildingBlocks.EventBus.Abstractions;
using YiPix.BuildingBlocks.PayPal;
using YiPix.Services.Subscription.Domain.Entities;
using YiPix.Services.Subscription.Infrastructure.Data;

namespace YiPix.Services.Subscription.Application;

/// <summary>
/// 订阅服务接口 - 订阅生命周期管理
/// </summary>
public interface ISubscriptionAppService
{
    Task<SubscriptionDto> GetActiveSubscriptionAsync(Guid userId, CancellationToken ct = default);
    Task<List<SubscriptionDto>> GetUserSubscriptionsAsync(Guid userId, CancellationToken ct = default);
    Task<SubscriptionDto> CreateSubscriptionAsync(CreateSubscriptionRequest request, CancellationToken ct = default);
    Task<SubscriptionDto> ActivateByPayPalIdAsync(string paypalSubscriptionId, CancellationToken ct = default);
    Task CancelSubscriptionAsync(Guid subscriptionId, string? reason = null, CancellationToken ct = default);
    Task<SubscriptionStatusResponse> CheckStatusAsync(Guid userId, CancellationToken ct = default);
    Task ActivateOrRenewByPaymentAsync(Guid userId, string planId, string paymentType, CancellationToken ct = default);
    Task UpdateSubscriptionStatusAsync(Guid userId, string paypalSubscriptionId, string newStatus, string reason, CancellationToken ct = default);
}

/// <summary>
/// 订阅服务实现：管理订阅创建、激活、取消，并通过事件总线发布状态变更事件
/// 支持 PayPal 订阅取消联动
/// </summary>
public class SubscriptionAppService : ISubscriptionAppService
{
    private readonly ISubscriptionRepository _repository;
    private readonly IEventBus _eventBus;
    private readonly IPayPalClient _paypalClient;
    private readonly ILogger<SubscriptionAppService> _logger;

    public SubscriptionAppService(
        ISubscriptionRepository repository,
        IEventBus eventBus,
        IPayPalClient paypalClient,
        ILogger<SubscriptionAppService> logger)
    {
        _repository = repository;
        _eventBus = eventBus;
        _paypalClient = paypalClient;
        _logger = logger;
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

        _logger.LogInformation("Subscription {SubscriptionId} created for user {UserId}, plan: {Plan}",
            subscription.Id, request.UserId, request.Plan);

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

        _logger.LogInformation("Subscription {SubscriptionId} activated via PayPal {PayPalSubscriptionId}",
            sub.Id, paypalSubscriptionId);

        return MapToDto(sub);
    }

    /// <summary>
    /// 取消订阅：同时取消本地记录和 PayPal 侧订阅
    /// </summary>
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

        // 同步取消 PayPal 侧订阅（如果有 PayPal 订阅 ID）
        if (!string.IsNullOrEmpty(sub.PayPalSubscriptionId))
        {
            try
            {
                await _paypalClient.CancelSubscriptionAsync(
                    sub.PayPalSubscriptionId,
                    reason ?? "User requested cancellation",
                    ct);

                _logger.LogInformation(
                    "PayPal subscription {PayPalSubscriptionId} cancelled for subscription {SubscriptionId}",
                    sub.PayPalSubscriptionId, sub.Id);
            }
            catch (Exception ex)
            {
                // PayPal 取消失败不应阻塞本地取消，仅记录错误
                _logger.LogError(ex,
                    "Failed to cancel PayPal subscription {PayPalSubscriptionId}, local subscription {SubscriptionId} already cancelled",
                    sub.PayPalSubscriptionId, sub.Id);
            }
        }

        await _eventBus.PublishAsync(
            new SubscriptionCancelledEvent(sub.UserId, sub.Id), ct);

        _logger.LogInformation("Subscription {SubscriptionId} cancelled for user {UserId}, reason: {Reason}",
            sub.Id, sub.UserId, reason);
    }

    public async Task<SubscriptionStatusResponse> CheckStatusAsync(Guid userId, CancellationToken ct = default)
    {
        var sub = await _repository.GetActiveByUserIdAsync(userId, ct);
        if (sub == null)
            return new SubscriptionStatusResponse(false, SubscriptionPlan.Free, null);

        Enum.TryParse<SubscriptionPlan>(sub.Plan, out var plan);
        return new SubscriptionStatusResponse(sub.IsActive, plan, sub.EndDate);
    }

    /// <summary>
    /// 支付完成后激活或续期订阅（由 PaymentCompletedEvent 触发）
    /// - 如果用户没有活跃订阅 → 创建新订阅
    /// - 如果用户已有活跃订阅 → 续期（延长 EndDate）
    /// </summary>
    public async Task ActivateOrRenewByPaymentAsync(Guid userId, string planId, string paymentType, CancellationToken ct = default)
    {
        if (!Enum.TryParse<SubscriptionPlan>(planId, out var plan))
        {
            _logger.LogWarning("Unknown plan '{PlanId}' in payment for user {UserId}", planId, userId);
            return;
        }

        var existing = await _repository.GetActiveByUserIdAsync(userId, ct);

        if (existing != null)
        {
            // 续期：延长到期时间
            var oldEndDate = existing.EndDate;
            var baseDate = existing.EndDate != null && existing.EndDate > DateTime.UtcNow
                ? existing.EndDate.Value
                : DateTime.UtcNow;

            existing.EndDate = plan switch
            {
                SubscriptionPlan.Monthly => baseDate.AddMonths(1),
                SubscriptionPlan.Yearly => baseDate.AddYears(1),
                SubscriptionPlan.Lifetime => null,
                _ => existing.EndDate
            };

            existing.Plan = planId; // 更新计划（可能升级）
            await _repository.UpdateAsync(existing, ct);

            await _repository.AddHistoryAsync(new SubscriptionHistory
            {
                SubscriptionId = existing.Id,
                UserId = userId,
                FromStatus = "Active",
                ToStatus = "Active",
                Reason = $"Renewed via payment, end date: {oldEndDate} → {existing.EndDate}"
            }, ct);

            _logger.LogInformation(
                "Subscription {SubscriptionId} renewed for user {UserId}, plan: {Plan}, new end date: {EndDate}",
                existing.Id, userId, planId, existing.EndDate);
        }
        else
        {
            // 新建订阅
            var subscription = new Domain.Entities.Subscription
            {
                UserId = userId,
                Plan = planId,
                Status = "Active",
                StartDate = DateTime.UtcNow,
                EndDate = CalculateEndDate(plan)
            };

            await _repository.CreateAsync(subscription, ct);

            await _eventBus.PublishAsync(
                new SubscriptionActivatedEvent(userId, subscription.Id, planId), ct);

            _logger.LogInformation(
                "New subscription {SubscriptionId} activated for user {UserId}, plan: {Plan}",
                subscription.Id, userId, planId);
        }
    }

    /// <summary>
    /// 通过 Webhook 更新订阅状态（如暂停、过期等）
    /// </summary>
    public async Task UpdateSubscriptionStatusAsync(Guid userId, string paypalSubscriptionId, string newStatus, string reason, CancellationToken ct = default)
    {
        var sub = await _repository.GetByPayPalIdAsync(paypalSubscriptionId, ct);
        if (sub == null)
        {
            _logger.LogWarning("No subscription found for PayPal ID {PayPalSubscriptionId}", paypalSubscriptionId);
            return;
        }

        var oldStatus = sub.Status;
        sub.Status = newStatus;

        if (newStatus == "Cancelled")
        {
            sub.CancelledAt = DateTime.UtcNow;
            sub.CancellationReason = reason;
        }

        await _repository.UpdateAsync(sub, ct);

        await _repository.AddHistoryAsync(new SubscriptionHistory
        {
            SubscriptionId = sub.Id,
            UserId = sub.UserId,
            FromStatus = oldStatus,
            ToStatus = newStatus,
            Reason = reason
        }, ct);

        _logger.LogInformation(
            "Subscription {SubscriptionId} status updated: {OldStatus} → {NewStatus}, reason: {Reason}",
            sub.Id, oldStatus, newStatus, reason);
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
