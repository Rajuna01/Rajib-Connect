
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AcxiomCRM.ViewModels;

public class LeadViewModel
{
    public int LeadId { get; set; }

    [Required(ErrorMessage = "Lead code is required.")]
    [StringLength(30)]
    public string LeadCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Lead name is required.")]
    [StringLength(150)]
    public string LeadName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string? Email { get; set; }

    [Phone(ErrorMessage = "Enter a valid phone number.")]
    public string? Phone { get; set; }

    [StringLength(100)]
    public string CompanyName { get; set; } = string.Empty;

    [Required]
    public string Source { get; set; } = "Other";

    [Required]
    public string Status { get; set; } = "New";

    [Required]
    public string Priority { get; set; } = "Medium";

    [Range(typeof(decimal), "0", "999999999999",
        ErrorMessage = "Expected value must be non-negative.")]
    public decimal ExpectedValue { get; set; }

    public string? AssignedTo { get; set; }

    public List<SelectListItem> SalesExecutives { get; set; } = new();
}
