
using AcxiomCRM.Models;

namespace AcxiomCRM.ViewModels;

public class FollowUpListViewModel
{
    public List<FollowUp> FollowUps { get; set; } = new();

    public string? SearchTerm { get; set; }

    public string? StatusFilter { get; set; }

    public int TotalFollowUps { get; set; }

    public int PlannedCount { get; set; }

    public int CompletedCount { get; set; }

    public int MissedCount { get; set; }

    public int OverdueCount { get; set; }
}
