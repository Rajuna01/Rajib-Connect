
namespace AcxiomCRM.ViewModels;

public class OpportunityListViewModel
{
    public int OpportunityId { get; set; }

    public string OpportunityName { get; set; } = string.Empty;

    public string CustomerName { get; set; } = "Not linked";

    public string LeadName { get; set; } = "Not linked";

    public string AssignedToName { get; set; } = "Unassigned";

    public decimal Amount { get; set; }

    public int Probability { get; set; }

    public decimal WeightedValue { get; set; }

    public string Stage { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime ExpectedCloseDate { get; set; }

    public string Source { get; set; } = string.Empty;
}
