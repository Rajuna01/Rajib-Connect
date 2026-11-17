
namespace AcxiomCRM.ViewModels;

public class AuditLogListViewModel
{
    public List<AuditLogViewModel> Logs { get; set; } = new();

    public string? SearchTerm { get; set; }

    public string? ActionFilter { get; set; }

    public string? EntityFilter { get; set; }

    public int TotalLogs { get; set; }
}
