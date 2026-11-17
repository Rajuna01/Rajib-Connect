
using AcxiomCRM.Authorization;
using AcxiomCRM.Data;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AcxiomCRM.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _context;

    public DashboardController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        bool isAdmin = User.IsInRole(RoleConstants.Admin);
        bool isManager = User.IsInRole(RoleConstants.Manager);

        ViewBag.Role = isAdmin
            ? "Administrator"
            : isManager ? "Manager" : "Sales Executive";

        // Base database queries
        var customers = _context.Customers.AsQueryable();
        var leads = _context.Leads.AsQueryable();
        var opportunities = _context.Opportunities.AsQueryable();

        // Role-based data access
        if (!isAdmin && !isManager)
        {
            customers = customers.Where(c => c.OwnerId == userId);

            leads = leads.Where(l => l.AssignedTo == userId);

            opportunities = opportunities.Where(o => o.AssignedTo == userId);
        }

        // Initialize dashboard model
        var dashboard = new DashboardViewModel();

        // ---------------------------------------
        // 1. DASHBOARD KPI VALUES
        // ---------------------------------------

        dashboard.TotalCustomers = await customers.CountAsync();

        dashboard.TotalLeads = await leads.CountAsync();

        dashboard.OpenOpportunities = await opportunities
            .CountAsync(o => o.Status == "Open");

        dashboard.PipelineValue = await opportunities
            .Where(o => o.Status == "Open")
            .SumAsync(o => (decimal?)o.Amount) ?? 0m;

        dashboard.WeightedPipeline = await opportunities
            .Where(o => o.Status == "Open")
            .SumAsync(o =>
                (decimal?)(o.Amount * o.Probability / 100m)) ?? 0m;

        // ---------------------------------------
        // 2. LEAD STATUS CHART
        // ---------------------------------------

        var leadStats = await leads
            .GroupBy(l => l.Status)
            .Select(g => new
            {
                Status = g.Key,
                Count = g.Count()
            })
            .ToListAsync();

        dashboard.LeadLabels = leadStats
            .Select(x => x.Status)
            .ToList();

        dashboard.LeadValues = leadStats
            .Select(x => x.Count)
            .ToList();

        // ---------------------------------------
        // 3. OPPORTUNITY STAGE CHART
        // ---------------------------------------

        var opportunityStats = await opportunities
            .GroupBy(o => o.Stage)
            .Select(g => new
            {
                Stage = g.Key,
                Count = g.Count()
            })
            .ToListAsync();

        dashboard.OpportunityLabels = opportunityStats
            .Select(x => x.Stage)
            .ToList();

        dashboard.OpportunityValues = opportunityStats
            .Select(x => x.Count)
            .ToList();

        // ---------------------------------------
        // 4. RECENT LEADS
        // ---------------------------------------

        dashboard.RecentLeads = await leads
            .OrderByDescending(l => l.CreatedDate)
            .Take(5)
            .Select(l => new RecentLeadViewModel
            {
                LeadCode = l.LeadCode,
                LeadName = l.LeadName,
                CompanyName = l.CompanyName,
                Status = l.Status,
                CreatedDate = l.CreatedDate
            })
            .ToListAsync();

        // ---------------------------------------
        // 5. RETURN DASHBOARD DATA
        // ---------------------------------------

        return View(dashboard);
    }
}
