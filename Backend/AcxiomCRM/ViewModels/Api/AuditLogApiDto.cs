namespace AcxiomCRM.ViewModels.Api
{
    public class AuditLogApiResponse
    {
        public int AuditLogId { get; set; }

        public string? UserId { get; set; }

        public string UserName { get; set; } = "Unknown";

        public string Action { get; set; } = string.Empty;

        public string EntityName { get; set; } = string.Empty;

        public int? RecordId { get; set; }

        public string? OldValue { get; set; }

        public string? NewValue { get; set; }

        public DateTime CreatedDate { get; set; }

        public string? IpAddress { get; set; }

        public string Result { get; set; } = string.Empty;
    }
}