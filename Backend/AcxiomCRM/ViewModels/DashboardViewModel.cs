
namespace AcxiomCRM.ViewModels;

public class DashboardViewModel
{
    // Dashboard KPI values
    public int TotalCustomers { get; set; }

    public int TotalLeads { get; set; }

    public int OpenOpportunities { get; set; }

    public decimal PipelineValue { get; set; }

    public decimal WeightedPipeline { get; set; }

    // Lead chart data
    public List<string> LeadLabels { get; set; } = new();

    public List<int> LeadValues { get; set; } = new();

    // Opportunity chart data
    public List<string> OpportunityLabels { get; set; } = new();

    public List<int> OpportunityValues { get; set; } = new();

    // Recent leads
    public List<RecentLeadViewModel> RecentLeads { get; set; } = new();
}

public class RecentLeadViewModel
{
    public string LeadCode { get; set; } = string.Empty;

    public string LeadName { get; set; } = string.Empty;

    public string CompanyName { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedDate { get; set; }
}
