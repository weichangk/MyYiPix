using Microsoft.EntityFrameworkCore;
using YiPix.Services.FileStorage.Domain.Entities;

namespace YiPix.Services.FileStorage.Infrastructure.Data;

public interface IFileRepository
{
    Task<StoredFile?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<StoredFile>> GetByUserIdAsync(Guid userId, string? category = null, CancellationToken ct = default);
    Task<StoredFile> CreateAsync(StoredFile file, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public class FileRepository : IFileRepository
{
    private readonly FileDbContext _context;

    public FileRepository(FileDbContext context) => _context = context;

    public async Task<StoredFile?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Files.FindAsync([id], ct);

    public async Task<List<StoredFile>> GetByUserIdAsync(Guid userId, string? category = null, CancellationToken ct = default)
    {
        var query = _context.Files.Where(f => f.UserId == userId);
        if (!string.IsNullOrEmpty(category)) query = query.Where(f => f.Category == category);
        return await query.OrderByDescending(f => f.CreatedAt).ToListAsync(ct);
    }

    public async Task<StoredFile> CreateAsync(StoredFile file, CancellationToken ct = default)
    {
        _context.Files.Add(file);
        await _context.SaveChangesAsync(ct);
        return file;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var file = await _context.Files.FindAsync([id], ct);
        if (file != null)
        {
            _context.Files.Remove(file);
            await _context.SaveChangesAsync(ct);
        }
    }
}
