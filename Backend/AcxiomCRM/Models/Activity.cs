
using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models;

public class Activity
{
    public int ActivityId { get; set; }

    [Required]
    public string ActivityType { get; set; } = "Task";

    [Required]
    [StringLength(150)]
    public string Subject { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    public DateTime ActivityDate { get; set; } = DateTime.UtcNow;

    public int? CustomerId { get; set; }

    public Customer? Customer { get; set; }

    public int? LeadId { get; set; }

    public Lead? Lead { get; set; }

    public string? AssignedTo { get; set; }

    public ApplicationUser? AssignedUser { get; set; }

    public string Status { get; set; } = "Planned";
}
