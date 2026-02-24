using Microsoft.EntityFrameworkCore;
using YiPix.Services.Payment.Domain.Entities;

namespace YiPix.Services.Payment.Infrastructure.Data;

public interface IPaymentRepository
{
    Task<Domain.Entities.Payment?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Domain.Entities.Payment?> GetByPayPalOrderIdAsync(string paypalOrderId, CancellationToken ct = default);
    Task<List<Domain.Entities.Payment>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<Domain.Entities.Payment> CreateAsync(Domain.Entities.Payment payment, CancellationToken ct = default);
    Task UpdateAsync(Domain.Entities.Payment payment, CancellationToken ct = default);
    Task<WebhookLog> CreateWebhookLogAsync(WebhookLog log, CancellationToken ct = default);
    Task<bool> WebhookAlreadyProcessedAsync(string eventType, string resourceId, CancellationToken ct = default);
}

public class PaymentRepository : IPaymentRepository
{
    private readonly PaymentDbContext _context;

    public PaymentRepository(PaymentDbContext context) => _context = context;

    public async Task<Domain.Entities.Payment?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Payments.FindAsync([id], ct);

    public async Task<Domain.Entities.Payment?> GetByPayPalOrderIdAsync(string paypalOrderId, CancellationToken ct = default)
        => await _context.Payments.FirstOrDefaultAsync(p => p.PayPalOrderId == paypalOrderId, ct);

    public async Task<List<Domain.Entities.Payment>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await _context.Payments.Where(p => p.UserId == userId).OrderByDescending(p => p.CreatedAt).ToListAsync(ct);

    public async Task<Domain.Entities.Payment> CreateAsync(Domain.Entities.Payment payment, CancellationToken ct = default)
    {
        _context.Payments.Add(payment);
        await _context.SaveChangesAsync(ct);
        return payment;
    }

    public async Task UpdateAsync(Domain.Entities.Payment payment, CancellationToken ct = default)
    {
        payment.UpdatedAt = DateTime.UtcNow;
        _context.Payments.Update(payment);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<WebhookLog> CreateWebhookLogAsync(WebhookLog log, CancellationToken ct = default)
    {
        _context.WebhookLogs.Add(log);
        await _context.SaveChangesAsync(ct);
        return log;
    }

    public async Task<bool> WebhookAlreadyProcessedAsync(string eventType, string resourceId, CancellationToken ct = default)
        => await _context.WebhookLogs.AnyAsync(w => w.EventType == eventType && w.ResourceId == resourceId && w.Processed, ct);
}
