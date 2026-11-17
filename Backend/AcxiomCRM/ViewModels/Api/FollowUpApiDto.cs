
using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.ViewModels.Api
{
    public class FollowUpApiRequest
    {
        public int? CustomerId { get; set; }

        public int? LeadId { get; set; }

        [Required]
        public DateTime FollowUpDate { get; set; }

        [Required]
        [StringLength(150)]
        public string Subject { get; set; } = string.Empty;

        [Required]
        public string FollowUpType { get; set; } = "Call";

        public string Status { get; set; } = "Planned";

        [StringLength(1000)]
        public string? Notes { get; set; }

        // Used by Admin and Manager only.
        public string? AssignedUserId { get; set; }
    }

    public class FollowUpApiResponse
    {
        public int FollowUpId { get; set; }

        public int? CustomerId { get; set; }

        public string CustomerName { get; set; } = "Not linked";

        public int? LeadId { get; set; }

        public string LeadName { get; set; } = "Not linked";

        public DateTime FollowUpDate { get; set; }

        public string Subject { get; set; } = string.Empty;

        public string FollowUpType { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public string? AssignedUserId { get; set; }

        public string AssignedUserName { get; set; } = "Unassigned";

        public string? Notes { get; set; }
    }
}
