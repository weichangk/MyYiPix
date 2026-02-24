using System.ComponentModel.DataAnnotations;
using YiPix.BuildingBlocks.Common.Domain;

namespace YiPix.Services.TaskProcessing.Domain.Entities;

public class ProcessingTask : AggregateRoot
{
    public Guid UserId { get; set; }
    
    [MaxLength(50)]
    public string TaskType { get; set; } = string.Empty; // Convert, Compress, Crop, AIEnhance, Batch
    
    [MaxLength(30)]
    public string Status { get; set; } = "Pending"; // Pending, Processing, Completed, Failed, Cancelled
    
    [MaxLength(500)]
    public string? InputFileUrl { get; set; }
    
    [MaxLength(500)]
    public string? OutputFileUrl { get; set; }
    
    public string? Parameters { get; set; } // JSON
    
    public int? Progress { get; set; } // 0-100
    
    public string? ErrorMessage { get; set; }
    
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    
    public long? InputFileSize { get; set; }
    public long? OutputFileSize { get; set; }
}
