
using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.ViewModels.Api
{
    public class LeadApiRequest
    {
        [Required]
        [StringLength(30)]
        public string LeadCode { get; set; } = string.Empty;

        [Required]
        [StringLength(150)]
        public string LeadName { get; set; } = string.Empty;

        [EmailAddress]
        public string? Email { get; set; }

        [Phone]
        public string? Phone { get; set; }

        [StringLength(100)]
        public string CompanyName { get; set; } = string.Empty;

        public string Source { get; set; } = "Other";

        public string Status { get; set; } = "New";

        public string Priority { get; set; } = "Medium";

        [Range(typeof(decimal), "0", "79228162514264337593543950335")]
        public decimal ExpectedValue { get; set; }

        // Admin and Manager can assign a Sales Executive.
        // Sales Executives cannot change ownership.
        public string? AssignedTo { get; set; }
    }

    public class LeadApiResponse
    {
        public int LeadId { get; set; }

        public string LeadCode { get; set; } = string.Empty;

        public string LeadName { get; set; } = string.Empty;

        public string? Email { get; set; }

        public string? Phone { get; set; }

        public string CompanyName { get; set; } = string.Empty;

        public string Source { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public string Priority { get; set; } = string.Empty;

        public decimal ExpectedValue { get; set; }

        public string? AssignedTo { get; set; }

        public string AssignedToName { get; set; } = "Unassigned";

        public DateTime CreatedDate { get; set; }
    }
}
