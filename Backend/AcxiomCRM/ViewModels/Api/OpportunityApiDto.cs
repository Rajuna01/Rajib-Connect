
using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.ViewModels.Api
{
    public class OpportunityApiRequest
    {
        [Required]
        [StringLength(150)]
        public string OpportunityName { get; set; } = string.Empty;

        public int? CustomerId { get; set; }

        public int? LeadId { get; set; }

        [Range(typeof(decimal), "0.01", "999999999")]
        public decimal Amount { get; set; }

        [Range(0, 100)]
        public int Probability { get; set; } = 50;

        [Required]
        [StringLength(30)]
        public string Stage { get; set; } = "Qualification";

        [Required]
        public DateTime ExpectedCloseDate { get; set; } =
            DateTime.Today.AddDays(30);

        [StringLength(100)]
        public string Source { get; set; } = "Other";

        [StringLength(1000)]
        public string? Notes { get; set; }

        // Admin and Manager can assign an active Sales Executive.
        // Sales Executives are automatically assigned to themselves.
        public string? AssignedTo { get; set; }
    }

    public class OpportunityApiResponse
    {
        public int OpportunityId { get; set; }

        public string OpportunityName { get; set; } = string.Empty;

        public int? CustomerId { get; set; }

        public string CustomerName { get; set; } = "Not linked";

        public int? LeadId { get; set; }

        public string LeadName { get; set; } = "Not linked";

        public decimal Amount { get; set; }

        public int Probability { get; set; }

        public decimal WeightedValue { get; set; }

        public string Stage { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public DateTime ExpectedCloseDate { get; set; }

        public string Source { get; set; } = string.Empty;

        public string? Notes { get; set; }

        public string? AssignedTo { get; set; }

        public string AssignedToName { get; set; } = "Unassigned";

        public string CreatedBy { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; }

        public DateTime? ModifiedDate { get; set; }
    }
}
