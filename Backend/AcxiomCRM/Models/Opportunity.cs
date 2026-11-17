
using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models;

public class Opportunity
{
    public int OpportunityId { get; set; }

    [Required(ErrorMessage = "Opportunity name is required.")]
    [StringLength(150)]
    public string OpportunityName { get; set; } = string.Empty;

    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public int? LeadId { get; set; }
    public Lead? Lead { get; set; }

    [Range(typeof(decimal), "0.01", "999999999",
        ErrorMessage = "Amount must be greater than zero.")]
    public decimal Amount { get; set; }

    [Range(0, 100,
        ErrorMessage = "Probability must be between 0 and 100.")]
    public int Probability { get; set; } = 50;

    [Required]
    [StringLength(30)]
    public string Stage { get; set; } = "Qualification";

    [Required]
    public DateTime ExpectedCloseDate { get; set; } =
        DateTime.Today.AddDays(30);

    [Required]
    [StringLength(20)]
    public string Status { get; set; } = "Open";

    [StringLength(100)]
    public string Source { get; set; } = "Other";

    [StringLength(1000)]
    public string? Notes { get; set; }

    public string? AssignedTo { get; set; }
    public ApplicationUser? AssignedUser { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public DateTime? ModifiedDate { get; set; }

    [Required]
    public string CreatedBy { get; set; } = string.Empty;

    public decimal WeightedValue =>
        Amount * Probability / 100m;
}
