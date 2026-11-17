
using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models;

public class FollowUp
{
    public int FollowUpId { get; set; }

    public int? CustomerId { get; set; }

    public Customer? Customer { get; set; }

    public int? LeadId { get; set; }

    public Lead? Lead { get; set; }

    [Required]
    public DateTime FollowUpDate { get; set; }

    [Required]
    [StringLength(150)]
    public string Subject { get; set; } = string.Empty;

    public string FollowUpType { get; set; } = "Call";

    public string Status { get; set; } = "Planned";

    public string? AssignedUserId { get; set; }

    public ApplicationUser? AssignedUser { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }
}
