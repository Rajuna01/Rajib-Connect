
using AcxiomCRM.Models;

namespace AcxiomCRM.ViewModels;

public class ActivityListViewModel
{
    public List<Activity> Activities { get; set; } = new();

    public string? SearchTerm { get; set; }

    public string? StatusFilter { get; set; }

    public string? TypeFilter { get; set; }

    public int TotalActivities { get; set; }

    public int PlannedCount { get; set; }

    public int CompletedCount { get; set; }

    public int CancelledCount { get; set; }
}
