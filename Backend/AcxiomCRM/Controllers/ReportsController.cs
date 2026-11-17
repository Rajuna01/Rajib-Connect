
using System.Globalization;
using System.Text;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers
{
    [Authorize]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReportsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            string? period,
            DateTime? startDate,
            DateTime? endDate)
        {
            var range = ResolveDateRange(period, startDate, endDate);

            if (!range.IsValid)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "The custom end date must be on or after the start date.");

                range = ResolveDateRange("All", null, null);
            }

            var userId = _userManager.GetUserId(User);
            bool isSalesExecutive = User.IsInRole("SalesExecutive");

            var customersQuery = _context.Customers.AsNoTracking();
            var leadsQuery = _context.Leads.AsNoTracking();
            var opportunitiesQuery = _context.Opportunities.AsNoTracking();
            var followUpsQuery = _context.FollowUps.AsNoTracking();
            var activitiesQuery = _context.Activities.AsNoTracking();

            // Sales Executives can see only records assigned to them.
            if (isSalesExecutive)
            {
                customersQuery = customersQuery.Where(x => x.OwnerId == userId);
                leadsQuery = leadsQuery.Where(x => x.AssignedTo == userId);
                opportunitiesQuery = opportunitiesQuery.Where(x => x.AssignedTo == userId);
                followUpsQuery = followUpsQuery.Where(x => x.AssignedUserId == userId);
                activitiesQuery = activitiesQuery.Where(x => x.AssignedTo == userId);
            }

            // Apply the selected date range to each module.
            if (range.Start.HasValue)
            {
                var start = range.Start.Value;

                customersQuery = customersQuery.Where(x => x.CreatedDate >= start);
                leadsQuery = leadsQuery.Where(x => x.CreatedDate >= start);
                opportunitiesQuery = opportunitiesQuery.Where(x => x.CreatedDate >= start);
                followUpsQuery = followUpsQuery.Where(x => x.FollowUpDate >= start);
                activitiesQuery = activitiesQuery.Where(x => x.ActivityDate >= start);
            }

            if (range.EndExclusive.HasValue)
            {
                var end = range.EndExclusive.Value;

                customersQuery = customersQuery.Where(x => x.CreatedDate < end);
                leadsQuery = leadsQuery.Where(x => x.CreatedDate < end);
                opportunitiesQuery = opportunitiesQuery.Where(x => x.CreatedDate < end);
                followUpsQuery = followUpsQuery.Where(x => x.FollowUpDate < end);
                activitiesQuery = activitiesQuery.Where(x => x.ActivityDate < end);
            }

            // Load the filtered records for the report calculations.
            var customers = await customersQuery.ToListAsync();
            var leads = await leadsQuery.ToListAsync();
            var opportunities = await opportunitiesQuery.ToListAsync();
            var followUps = await followUpsQuery.ToListAsync();
            var activities = await activitiesQuery.ToListAsync();

            var model = new ReportsViewModel
            {
                Period = range.Period,
                StartDate = range.Start,
                EndDate = range.EndExclusive?.AddDays(-1),

                // Customers
                TotalCustomers = customers.Count,
                ActiveCustomers = customers.Count(x =>
                    x.Status.Equals("Active", StringComparison.OrdinalIgnoreCase)),
                InactiveCustomers = customers.Count(x =>
                    x.Status.Equals("Inactive", StringComparison.OrdinalIgnoreCase)),

                // Leads
                TotalLeads = leads.Count,
                NewLeads = leads.Count(x => IsStatus(x.Status, "New")),
                ContactedLeads = leads.Count(x => IsStatus(x.Status, "Contacted")),
                QualifiedLeads = leads.Count(x => IsStatus(x.Status, "Qualified")),
                UnqualifiedLeads = leads.Count(x => IsStatus(x.Status, "Unqualified")),
                ConvertedLeads = leads.Count(x => IsStatus(x.Status, "Converted")),
                LostLeads = leads.Count(x => IsStatus(x.Status, "Lost")),
                TotalLeadValue = leads.Sum(x => x.ExpectedValue),

                // Opportunities
                TotalOpportunities = opportunities.Count,
                OpenOpportunities = opportunities.Count(x =>
                    !IsStatus(x.Stage, "Won") && !IsStatus(x.Stage, "Lost")),
                WonOpportunities = opportunities.Count(x => IsStatus(x.Stage, "Won")),
                LostOpportunities = opportunities.Count(x => IsStatus(x.Stage, "Lost")),
                TotalPipelineValue = opportunities
                    .Where(x => !IsStatus(x.Stage, "Won") && !IsStatus(x.Stage, "Lost"))
                    .Sum(x => x.Amount),
                WeightedPipelineValue = opportunities
                    .Where(x => !IsStatus(x.Stage, "Won") && !IsStatus(x.Stage, "Lost"))
                    .Sum(x => x.Amount * x.Probability / 100m),
                WonRevenue = opportunities
                    .Where(x => IsStatus(x.Stage, "Won"))
                    .Sum(x => x.Amount),

                // Follow-ups
                TotalFollowUps = followUps.Count,
                PlannedFollowUps = followUps.Count(x => IsStatus(x.Status, "Planned")),
                CompletedFollowUps = followUps.Count(x => IsStatus(x.Status, "Completed")),
                MissedFollowUps = followUps.Count(x => IsStatus(x.Status, "Missed")),
                CancelledFollowUps = followUps.Count(x => IsStatus(x.Status, "Cancelled")),

                // Activities
                TotalActivities = activities.Count,
                PlannedActivities = activities.Count(x => IsStatus(x.Status, "Planned")),
                CompletedActivities = activities.Count(x => IsStatus(x.Status, "Completed")),
                CancelledActivities = activities.Count(x => IsStatus(x.Status, "Cancelled"))
            };

            model.LeadConversionRate = model.TotalLeads == 0
                ? 0
                : Math.Round(
                    (double)model.ConvertedLeads / model.TotalLeads * 100,
                    2);

            model.MonthlyTrends = BuildMonthlyTrends(
                leads,
                opportunities,
                range.Start,
                range.EndExclusive);

            model.SalesExecutivePerformance = await BuildSalesExecutivePerformance(
                leads,
                opportunities,
                isSalesExecutive,
                userId);

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> ExportCsv(
            string? period,
            DateTime? startDate,
            DateTime? endDate)
        {
            var range = ResolveDateRange(period, startDate, endDate);

            if (!range.IsValid)
            {
                return BadRequest("Invalid custom date range.");
            }

            var userId = _userManager.GetUserId(User);
            bool isSalesExecutive = User.IsInRole("SalesExecutive");

            var customersQuery = _context.Customers.AsNoTracking();
            var leadsQuery = _context.Leads.AsNoTracking();
            var opportunitiesQuery = _context.Opportunities.AsNoTracking();
            var followUpsQuery = _context.FollowUps.AsNoTracking();
            var activitiesQuery = _context.Activities.AsNoTracking();

            if (isSalesExecutive)
            {
                customersQuery = customersQuery.Where(x => x.OwnerId == userId);
                leadsQuery = leadsQuery.Where(x => x.AssignedTo == userId);
                opportunitiesQuery = opportunitiesQuery.Where(x => x.AssignedTo == userId);
                followUpsQuery = followUpsQuery.Where(x => x.AssignedUserId == userId);
                activitiesQuery = activitiesQuery.Where(x => x.AssignedTo == userId);
            }

            if (range.Start.HasValue)
            {
                var start = range.Start.Value;
                customersQuery = customersQuery.Where(x => x.CreatedDate >= start);
                leadsQuery = leadsQuery.Where(x => x.CreatedDate >= start);
                opportunitiesQuery = opportunitiesQuery.Where(x => x.CreatedDate >= start);
                followUpsQuery = followUpsQuery.Where(x => x.FollowUpDate >= start);
                activitiesQuery = activitiesQuery.Where(x => x.ActivityDate >= start);
            }

            if (range.EndExclusive.HasValue)
            {
                var end = range.EndExclusive.Value;
                customersQuery = customersQuery.Where(x => x.CreatedDate < end);
                leadsQuery = leadsQuery.Where(x => x.CreatedDate < end);
                opportunitiesQuery = opportunitiesQuery.Where(x => x.CreatedDate < end);
                followUpsQuery = followUpsQuery.Where(x => x.FollowUpDate < end);
                activitiesQuery = activitiesQuery.Where(x => x.ActivityDate < end);
            }

            var customers = await customersQuery.ToListAsync();
            var leads = await leadsQuery.ToListAsync();
            var opportunities = await opportunitiesQuery.ToListAsync();
            var followUps = await followUpsQuery.ToListAsync();
            var activities = await activitiesQuery.ToListAsync();

            var monthly = BuildMonthlyTrends(
                leads,
                opportunities,
                range.Start,
                range.EndExclusive);

            var employees = await BuildSalesExecutivePerformance(
                leads,
                opportunities,
                isSalesExecutive,
                userId);

            var csv = new StringBuilder();

            csv.AppendLine("Rajib Connect Reports and Analytics");
            csv.AppendLine($"Period,{Csv(range.Period)}");
            csv.AppendLine($"Generated On,{Csv(DateTime.Now.ToString("dd-MMM-yyyy HH:mm", CultureInfo.InvariantCulture))}");
            csv.AppendLine();

            csv.AppendLine("CUSTOMER REPORT");
            csv.AppendLine("Metric,Value");
            csv.AppendLine($"Total Customers,{customers.Count}");
            csv.AppendLine($"Active Customers,{customers.Count(x => IsStatus(x.Status, "Active"))}");
            csv.AppendLine($"Inactive Customers,{customers.Count(x => IsStatus(x.Status, "Inactive"))}");
            csv.AppendLine();

            csv.AppendLine("LEAD REPORT");
            csv.AppendLine("Metric,Value");
            csv.AppendLine($"Total Leads,{leads.Count}");
            csv.AppendLine($"New Leads,{leads.Count(x => IsStatus(x.Status, "New"))}");
            csv.AppendLine($"Contacted Leads,{leads.Count(x => IsStatus(x.Status, "Contacted"))}");
            csv.AppendLine($"Qualified Leads,{leads.Count(x => IsStatus(x.Status, "Qualified"))}");
            csv.AppendLine($"Unqualified Leads,{leads.Count(x => IsStatus(x.Status, "Unqualified"))}");
            csv.AppendLine($"Converted Leads,{leads.Count(x => IsStatus(x.Status, "Converted"))}");
            csv.AppendLine($"Lost Leads,{leads.Count(x => IsStatus(x.Status, "Lost"))}");
            csv.AppendLine($"Total Lead Value,{leads.Sum(x => x.ExpectedValue).ToString(CultureInfo.InvariantCulture)}");
            csv.AppendLine();

            csv.AppendLine("OPPORTUNITY REPORT");
            csv.AppendLine("Metric,Value");
            csv.AppendLine($"Total Opportunities,{opportunities.Count}");
            csv.AppendLine($"Open Opportunities,{opportunities.Count(x => !IsStatus(x.Stage, "Won") && !IsStatus(x.Stage, "Lost"))}");
            csv.AppendLine($"Won Opportunities,{opportunities.Count(x => IsStatus(x.Stage, "Won"))}");
            csv.AppendLine($"Lost Opportunities,{opportunities.Count(x => IsStatus(x.Stage, "Lost"))}");
            csv.AppendLine($"Won Revenue,{opportunities.Where(x => IsStatus(x.Stage, "Won")).Sum(x => x.Amount).ToString(CultureInfo.InvariantCulture)}");
            csv.AppendLine($"Weighted Pipeline Value,{opportunities.Where(x => !IsStatus(x.Stage, "Won") && !IsStatus(x.Stage, "Lost")).Sum(x => x.Amount * x.Probability / 100m).ToString(CultureInfo.InvariantCulture)}");
            csv.AppendLine();

            csv.AppendLine("FOLLOW-UP REPORT");
            csv.AppendLine("Metric,Value");
            csv.AppendLine($"Total Follow-ups,{followUps.Count}");
            csv.AppendLine($"Planned Follow-ups,{followUps.Count(x => IsStatus(x.Status, "Planned"))}");
            csv.AppendLine($"Completed Follow-ups,{followUps.Count(x => IsStatus(x.Status, "Completed"))}");
            csv.AppendLine($"Missed Follow-ups,{followUps.Count(x => IsStatus(x.Status, "Missed"))}");
            csv.AppendLine($"Cancelled Follow-ups,{followUps.Count(x => IsStatus(x.Status, "Cancelled"))}");
            csv.AppendLine();

            csv.AppendLine("ACTIVITY REPORT");
            csv.AppendLine("Metric,Value");
            csv.AppendLine($"Total Activities,{activities.Count}");
            csv.AppendLine($"Planned Activities,{activities.Count(x => IsStatus(x.Status, "Planned"))}");
            csv.AppendLine($"Completed Activities,{activities.Count(x => IsStatus(x.Status, "Completed"))}");
            csv.AppendLine($"Cancelled Activities,{activities.Count(x => IsStatus(x.Status, "Cancelled"))}");
            csv.AppendLine();

            csv.AppendLine("MONTHLY TRENDS");
            csv.AppendLine("Month,Leads,Opportunities,Won Opportunity Value");

            foreach (var item in monthly)
            {
                csv.AppendLine(
                    $"{Csv(item.Label)},{item.Leads},{item.Opportunities},{item.WonOpportunityValue.ToString(CultureInfo.InvariantCulture)}");
            }

            csv.AppendLine();
            csv.AppendLine("SALES EXECUTIVE PERFORMANCE");
            csv.AppendLine("Sales Executive,Leads,Opportunities,Won Opportunities,Pipeline Value,Won Value");

            foreach (var item in employees)
            {
                csv.AppendLine(
                    $"{Csv(item.Name)},{item.Leads},{item.Opportunities},{item.WonOpportunities},{item.PipelineValue.ToString(CultureInfo.InvariantCulture)},{item.WonValue.ToString(CultureInfo.InvariantCulture)}");
            }

            var bytes = Encoding.UTF8.GetPreamble()
                .Concat(Encoding.UTF8.GetBytes(csv.ToString()))
                .ToArray();

            var fileName = $"RajibConnect_Report_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

            return File(bytes, "text/csv; charset=utf-8", fileName);
        }

        private async Task<List<SalesExecutiveReportItem>> BuildSalesExecutivePerformance(
            List<Lead> leads,
            List<Opportunity> opportunities,
            bool isSalesExecutive,
            string? currentUserId)
        {
            var usersQuery = _context.Users.AsNoTracking();

            if (isSalesExecutive && !string.IsNullOrEmpty(currentUserId))
            {
                usersQuery = usersQuery.Where(x => x.Id == currentUserId);
            }

            var users = await usersQuery.ToListAsync();

            var result = new List<SalesExecutiveReportItem>();

            foreach (var user in users)
            {
                var userLeads = leads
                    .Where(x => x.AssignedTo == user.Id)
                    .ToList();

                var userOpportunities = opportunities
                    .Where(x => x.AssignedTo == user.Id)
                    .ToList();

                result.Add(new SalesExecutiveReportItem
                {
                    Name = string.IsNullOrWhiteSpace(user.FullName)
                        ? (user.Email ?? "Unknown User")
                        : user.FullName,

                    Leads = userLeads.Count,
                    Opportunities = userOpportunities.Count,
                    WonOpportunities = userOpportunities.Count(x => IsStatus(x.Stage, "Won")),

                    PipelineValue = userOpportunities
                        .Where(x => !IsStatus(x.Stage, "Won") && !IsStatus(x.Stage, "Lost"))
                        .Sum(x => x.Amount),

                    WonValue = userOpportunities
                        .Where(x => IsStatus(x.Stage, "Won"))
                        .Sum(x => x.Amount)
                });
            }

            return result
                .OrderByDescending(x => x.WonValue)
                .ThenBy(x => x.Name)
                .ToList();
        }

        private static List<MonthlyReportItem> BuildMonthlyTrends(
            List<Lead> leads,
            List<Opportunity> opportunities,
            DateTime? start,
            DateTime? endExclusive)
        {
            var firstMonth = start.HasValue
                ? new DateTime(start.Value.Year, start.Value.Month, 1)
                : new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-5);

            var lastDate = endExclusive.HasValue
                ? endExclusive.Value.AddTicks(-1)
                : DateTime.Today;

            var lastMonth = new DateTime(lastDate.Year, lastDate.Month, 1);

            // Prevent unexpectedly large charts for very long custom ranges.
            if ((lastMonth.Year - firstMonth.Year) * 12 +
                lastMonth.Month - firstMonth.Month > 59)
            {
                firstMonth = lastMonth.AddMonths(-59);
            }

            var result = new List<MonthlyReportItem>();

            for (var month = firstMonth; month <= lastMonth; month = month.AddMonths(1))
            {
                var nextMonth = month.AddMonths(1);

                var monthlyLeads = leads
                    .Where(x => x.CreatedDate >= month && x.CreatedDate < nextMonth)
                    .ToList();

                var monthlyOpportunities = opportunities
                    .Where(x => x.CreatedDate >= month && x.CreatedDate < nextMonth)
                    .ToList();

                result.Add(new MonthlyReportItem
                {
                    Label = month.ToString("MMM yyyy", CultureInfo.InvariantCulture),
                    Leads = monthlyLeads.Count,
                    Opportunities = monthlyOpportunities.Count,
                    WonOpportunityValue = monthlyOpportunities
                        .Where(x => IsStatus(x.Stage, "Won"))
                        .Sum(x => x.Amount)
                });
            }

            return result;
        }

        private static bool IsStatus(string? actual, string expected)
        {
            return string.Equals(
                actual?.Trim(),
                expected,
                StringComparison.OrdinalIgnoreCase);
        }

        private static string Csv(string? value)
        {
            value ??= string.Empty;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static (
            DateTime? Start,
            DateTime? EndExclusive,
            string Period,
            bool IsValid
        ) ResolveDateRange(
            string? period,
            DateTime? startDate,
            DateTime? endDate)
        {
            var today = DateTime.Today;

            // Preset filters are resolved first.
            // Old custom date values are deliberately ignored for these options.
            switch (period?.Trim().ToLowerInvariant())
            {
                case "today":
                    return (today, today.AddDays(1), "Today", true);

                case "week":
                    int daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
                    var monday = today.AddDays(-daysSinceMonday);
                    return (monday, monday.AddDays(7), "This Week", true);

                case "month":
                    var monthStart = new DateTime(today.Year, today.Month, 1);
                    return (monthStart, monthStart.AddMonths(1), "This Month", true);

                case "year":
                    var yearStart = new DateTime(today.Year, 1, 1);
                    return (yearStart, yearStart.AddYears(1), "This Year", true);

                case "custom":
                    var customStart = startDate?.Date;
                    var customEnd = endDate?.Date;

                    bool valid =
                        !customStart.HasValue ||
                        !customEnd.HasValue ||
                        customEnd.Value >= customStart.Value;

                    return (
                        customStart,
                        customEnd?.AddDays(1),
                        "Custom Range",
                        valid);

                case "all":
                case null:
                case "":
                default:
                    return (null, null, "All Time", true);
            }
        }
    }
}
