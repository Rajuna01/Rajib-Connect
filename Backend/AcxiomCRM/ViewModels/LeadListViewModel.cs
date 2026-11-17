
namespace AcxiomCRM.ViewModels;

public class LeadListViewModel
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

    public string AssignedToName { get; set; } = "Unassigned";

    public DateTime CreatedDate { get; set; }
}
