
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AcxiomCRM.ViewModels;

public class FollowUpViewModel
{
    public int FollowUpId { get; set; }

    public int? CustomerId { get; set; }

    public int? LeadId { get; set; }

    [Required(ErrorMessage = "Follow-up date is required.")]
    public DateTime FollowUpDate { get; set; } = DateTime.Today.AddDays(1);

    [Required(ErrorMessage = "Subject is required.")]
    [StringLength(150)]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "Select a follow-up type.")]
    public string FollowUpType { get; set; } = "Call";

    public string Status { get; set; } = "Planned";

    public string? AssignedUserId { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public List<SelectListItem> Customers { get; set; } = new();

    public List<SelectListItem> Leads { get; set; } = new();

    public List<SelectListItem> SalesExecutives { get; set; } = new();
}
