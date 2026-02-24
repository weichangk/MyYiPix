using System.ComponentModel.DataAnnotations;
using YiPix.BuildingBlocks.Common.Domain;

namespace YiPix.Services.FileStorage.Domain.Entities;

public class StoredFile : BaseEntity
{
    public Guid? UserId { get; set; }
    
    [MaxLength(256)]
    public string FileName { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;
    
    public long FileSize { get; set; }
    
    [MaxLength(500)]
    public string StoragePath { get; set; } = string.Empty;
    
    [MaxLength(50)]
    public string StorageProvider { get; set; } = "Local"; // Local, S3, AzureBlob, MinIO
    
    [MaxLength(100)]
    public string? FileHash { get; set; }
    
    public bool IsPublic { get; set; }
    
    [MaxLength(50)]
    public string? Category { get; set; } // Avatar, TaskInput, TaskOutput, Release
}
