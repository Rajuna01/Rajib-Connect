
namespace AcxiomCRM.ViewModels;

public class AuditLogViewModel
{
    public int AuditLogId { get; set; }

    public string UserId { get; set; } = "System";

    public string Action { get; set; } = string.Empty;

    public string EntityName { get; set; } = string.Empty;

    public int? RecordId { get; set; }

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public DateTime CreatedDate { get; set; }

    public string? IpAddress { get; set; }

    public string Result { get; set; } = "Success";
}
