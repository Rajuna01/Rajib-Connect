
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AcxiomCRM.ViewModels;

public class ActivityViewModel
{
    public int ActivityId { get; set; }

    [Required(ErrorMessage = "Select an activity type.")]
    public string ActivityType { get; set; } = "Call";

    [Required(ErrorMessage = "Activity subject is required.")]
    [StringLength(150)]
    public string Subject { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Activity date is required.")]
    public DateTime ActivityDate { get; set; } = DateTime.Today;

    public int? CustomerId { get; set; }

    public int? LeadId { get; set; }

    public string? AssignedTo { get; set; }

    public string Status { get; set; } = "Planned";

    public List<SelectListItem> Customers { get; set; } = new();

    public List<SelectListItem> Leads { get; set; } = new();

    public List<SelectListItem> SalesExecutives { get; set; } = new();
}
