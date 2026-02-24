using Microsoft.EntityFrameworkCore;

namespace YiPix.Services.Subscription.Infrastructure.Data;

public interface ISubscriptionRepository
{
    Task<Domain.Entities.Subscription?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Domain.Entities.Subscription?> GetActiveByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<Domain.Entities.Subscription?> GetByPayPalIdAsync(string paypalSubscriptionId, CancellationToken ct = default);
    Task<List<Domain.Entities.Subscription>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<Domain.Entities.Subscription> CreateAsync(Domain.Entities.Subscription subscription, CancellationToken ct = default);
    Task UpdateAsync(Domain.Entities.Subscription subscription, CancellationToken ct = default);
    Task AddHistoryAsync(Domain.Entities.SubscriptionHistory history, CancellationToken ct = default);
}

public class SubscriptionRepository : ISubscriptionRepository
{
    private readonly SubscriptionDbContext _context;

    public SubscriptionRepository(SubscriptionDbContext context) => _context = context;

    public async Task<Domain.Entities.Subscription?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Subscriptions.FindAsync([id], ct);

    public async Task<Domain.Entities.Subscription?> GetActiveByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await _context.Subscriptions
            .Where(s => s.UserId == userId && s.Status == "Active")
            .OrderByDescending(s => s.StartDate)
            .FirstOrDefaultAsync(ct);

    public async Task<Domain.Entities.Subscription?> GetByPayPalIdAsync(string paypalSubscriptionId, CancellationToken ct = default)
        => await _context.Subscriptions
            .FirstOrDefaultAsync(s => s.PayPalSubscriptionId == paypalSubscriptionId, ct);

    public async Task<List<Domain.Entities.Subscription>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await _context.Subscriptions
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.StartDate)
            .ToListAsync(ct);

    public async Task<Domain.Entities.Subscription> CreateAsync(Domain.Entities.Subscription subscription, CancellationToken ct = default)
    {
        _context.Subscriptions.Add(subscription);
        await _context.SaveChangesAsync(ct);
        return subscription;
    }

    public async Task UpdateAsync(Domain.Entities.Subscription subscription, CancellationToken ct = default)
    {
        subscription.UpdatedAt = DateTime.UtcNow;
        _context.Subscriptions.Update(subscription);
        await _context.SaveChangesAsync(ct);
    }

    public async Task AddHistoryAsync(Domain.Entities.SubscriptionHistory history, CancellationToken ct = default)
    {
        _context.SubscriptionHistories.Add(history);
        await _context.SaveChangesAsync(ct);
    }
}
