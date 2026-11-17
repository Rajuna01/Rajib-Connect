
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AcxiomCRM.ViewModels;

public class OpportunityViewModel
{
    public int OpportunityId { get; set; }

    [Required(ErrorMessage = "Opportunity name is required.")]
    [StringLength(150)]
    public string OpportunityName { get; set; } = string.Empty;

    [Display(Name = "Customer")]
    public int? CustomerId { get; set; }

    [Display(Name = "Lead")]
    public int? LeadId { get; set; }

    [Required]
    [Range(typeof(decimal), "0.01", "999999999")]
    public decimal Amount { get; set; }

    [Required]
    [Range(0, 100)]
    public int Probability { get; set; } = 50;

    [Required]
    public string Stage { get; set; } = "Qualification";

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Expected Close Date")]
    public DateTime ExpectedCloseDate { get; set; } =
        DateTime.Today.AddDays(30);

    [Required]
    public string Status { get; set; } = "Open";

    public string Source { get; set; } = "Other";

    [StringLength(1000)]
    public string? Notes { get; set; }

    [Display(Name = "Assigned Sales Executive")]
    public string? AssignedTo { get; set; }

    public List<SelectListItem> Customers { get; set; } = new();
    public List<SelectListItem> Leads { get; set; } = new();
    public List<SelectListItem> SalesExecutives { get; set; } = new();
}
