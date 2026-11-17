
using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models;

public class AuditLog
{
    public int AuditLogId { get; set; }

    public string? UserId { get; set; }

    [StringLength(100)]
    public string Action { get; set; } = string.Empty;

    [StringLength(100)]
    public string EntityName { get; set; } = string.Empty;

    public int? RecordId { get; set; }

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public string? IpAddress { get; set; }

    [StringLength(50)]
    public string Result { get; set; } = "Success";
}
