using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.ViewModels.Api
{
    public class ActivityApiRequest
    {
        [Required]
        public string ActivityType { get; set; } = "Task";

        [Required]
        [StringLength(150)]
        public string Subject { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        public DateTime ActivityDate { get; set; } = DateTime.UtcNow;

        public int? CustomerId { get; set; }

        public int? LeadId { get; set; }

        public string? AssignedTo { get; set; }

        public string Status { get; set; } = "Planned";
    }

    public class ActivityApiResponse
    {
        public int ActivityId { get; set; }

        public string ActivityType { get; set; } = string.Empty;

        public string Subject { get; set; } = string.Empty;

        public string? Description { get; set; }

        public DateTime ActivityDate { get; set; }

        public int? CustomerId { get; set; }

        public string CustomerName { get; set; } = "Not linked";

        public int? LeadId { get; set; }

        public string LeadName { get; set; } = "Not linked";

        public string? AssignedTo { get; set; }

        public string AssignedUserName { get; set; } = "Unassigned";

        public string Status { get; set; } = string.Empty;
    }
}