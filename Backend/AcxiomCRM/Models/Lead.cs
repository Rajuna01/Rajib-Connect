
using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models;

public class Lead
{
    public int LeadId { get; set; }

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

    public decimal ExpectedValue { get; set; }

    public string? AssignedTo { get; set; }

    public ApplicationUser? AssignedUser { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
