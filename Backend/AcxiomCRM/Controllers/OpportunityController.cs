
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AcxiomCRM.Controllers;

[Authorize(Roles = "Admin,Manager,SalesExecutive")]
public class OpportunityController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AuditLogService _auditLogService;

    private static readonly string[] Stages =
    {
        "Qualification",
        "Proposal",
        "Negotiation",
        "Won",
        "Lost"
    };

    private static readonly string[] Sources =
    {
        "Website",
        "Referral",
        "Advertisement",
        "Cold Call",
        "Existing Customer",
        "Other"
    };

    public OpportunityController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        AuditLogService auditLogService)
    {
        _context = context;
        _userManager = userManager;
        _auditLogService = auditLogService;
    }

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private bool IsAdminOrManager =>
        User.IsInRole("Admin") || User.IsInRole("Manager");

    // Restrict Sales Executives to their own opportunities.
    private IQueryable<Opportunity> ScopedOpportunities()
    {
        var query = _context.Opportunities
            .Include(o => o.Customer)
            .Include(o => o.Lead)
            .Include(o => o.AssignedUser)
            .AsQueryable();

        if (!IsAdminOrManager)
        {
            query = query.Where(
                o => o.AssignedTo == CurrentUserId);
        }

        return query;
    }

    // Verify that the current user can access an opportunity.
    private async Task<bool> CanAccessAsync(int id)
    {
        var opportunity = await _context.Opportunities
            .AsNoTracking()
            .FirstOrDefaultAsync(
                o => o.OpportunityId == id);

        return opportunity != null &&
            (IsAdminOrManager ||
             opportunity.AssignedTo == CurrentUserId);
    }

    // GET: /Opportunity
    [HttpGet]
    public async Task<IActionResult> Index(string? search)
    {
        var query = ScopedOpportunities();

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();

            query = query.Where(o =>
                o.OpportunityName.Contains(search) ||
                (o.Customer != null &&
                 o.Customer.CustomerName.Contains(search)) ||
                o.Stage.Contains(search));
        }

        var opportunities = await query
            .OrderByDescending(o => o.CreatedDate)
            .ToListAsync();

        ViewBag.Search = search;

        var result = opportunities.Select(o =>
            new OpportunityListViewModel
            {
                OpportunityId = o.OpportunityId,
                OpportunityName = o.OpportunityName,

                CustomerName =
                    o.Customer?.CustomerName ?? "Not linked",

                LeadName =
                    o.Lead?.LeadName ?? "Not linked",

                AssignedToName =
                    o.AssignedUser?.FullName ?? "Unassigned",

                Amount = o.Amount,
                Probability = o.Probability,
                WeightedValue = o.WeightedValue,
                Stage = o.Stage,
                Status = o.Status,
                ExpectedCloseDate = o.ExpectedCloseDate,
                Source = o.Source
            }).ToList();

        return View(result);
    }

    // GET: /Opportunity/Create
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new OpportunityViewModel();

        await LoadDropdownsAsync(model);

        return View(model);
    }

    // POST: /Opportunity/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        OpportunityViewModel model)
    {
        ValidateOpportunity(model);

        if (!await ValidateReferencesAsync(model))
        {
            await LoadDropdownsAsync(model);
            return View(model);
        }

        // Sales Executives are automatically assigned to themselves.
        if (!IsAdminOrManager)
        {
            model.AssignedTo = CurrentUserId;
        }

        if (IsAdminOrManager &&
            string.IsNullOrWhiteSpace(model.AssignedTo))
        {
            ModelState.AddModelError(
                nameof(model.AssignedTo),
                "Please assign a Sales Executive.");
        }

        if (!ModelState.IsValid)
        {
            await LoadDropdownsAsync(model);
            return View(model);
        }

        var opportunity = new Opportunity
        {
            OpportunityName = model.OpportunityName.Trim(),

            CustomerId = model.CustomerId,
            LeadId = model.LeadId,

            Amount = model.Amount,
            Probability = model.Probability,

            Stage = model.Stage,
            Status = GetStatusFromStage(model.Stage),

            ExpectedCloseDate = model.ExpectedCloseDate.Date,

            Source = model.Source,
            Notes = model.Notes,

            AssignedTo = model.AssignedTo,

            CreatedBy = CurrentUserId,
            CreatedDate = DateTime.UtcNow
        };

        _context.Opportunities.Add(opportunity);

        await _context.SaveChangesAsync();

        // Audit: Opportunity created.
        await _auditLogService.LogAsync(
            action: "Create",
            entityName: "Opportunity",
            recordId: opportunity.OpportunityId,
            newValue: new
            {
                opportunity.OpportunityName,
                opportunity.CustomerId,
                opportunity.LeadId,
                opportunity.Amount,
                opportunity.Probability,
                opportunity.Stage,
                opportunity.Status,
                opportunity.ExpectedCloseDate,
                opportunity.Source,
                opportunity.AssignedTo
            }
        );

        TempData["Success"] =
            "Opportunity created successfully.";

        return RedirectToAction(nameof(Index));
    }

    // GET: /Opportunity/Details/5
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        if (!await CanAccessAsync(id))
        {
            return NotFound();
        }

        var opportunity = await ScopedOpportunities()
            .FirstOrDefaultAsync(
                o => o.OpportunityId == id);

        if (opportunity == null)
        {
            return NotFound();
        }

        return View(opportunity);
    }

    // GET: /Opportunity/Edit/5
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        if (!await CanAccessAsync(id))
        {
            return NotFound();
        }

        var opportunity = await _context.Opportunities
            .FirstOrDefaultAsync(
                o => o.OpportunityId == id);

        if (opportunity == null)
        {
            return NotFound();
        }

        var model = new OpportunityViewModel
        {
            OpportunityId = opportunity.OpportunityId,

            OpportunityName = opportunity.OpportunityName,

            CustomerId = opportunity.CustomerId,
            LeadId = opportunity.LeadId,

            Amount = opportunity.Amount,
            Probability = opportunity.Probability,

            Stage = opportunity.Stage,
            Status = opportunity.Status,

            ExpectedCloseDate = opportunity.ExpectedCloseDate,

            Source = opportunity.Source,
            Notes = opportunity.Notes,

            AssignedTo = opportunity.AssignedTo
        };

        await LoadDropdownsAsync(model);

        return View(model);
    }

    // POST: /Opportunity/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        OpportunityViewModel model)
    {
        if (id != model.OpportunityId ||
            !await CanAccessAsync(id))
        {
            return NotFound();
        }

        var opportunity = await _context.Opportunities
            .FirstOrDefaultAsync(
                o => o.OpportunityId == id);

        if (opportunity == null)
        {
            return NotFound();
        }

        ValidateOpportunity(model);

        if (!await ValidateReferencesAsync(model))
        {
            await LoadDropdownsAsync(model);
            return View(model);
        }

        if (!IsAdminOrManager)
        {
            model.AssignedTo = CurrentUserId;
        }

        if (IsAdminOrManager &&
            string.IsNullOrWhiteSpace(model.AssignedTo))
        {
            ModelState.AddModelError(
                nameof(model.AssignedTo),
                "Please assign a Sales Executive.");
        }

        if (!ModelState.IsValid)
        {
            await LoadDropdownsAsync(model);
            return View(model);
        }

        // Capture the old values before modifying the opportunity.
        var oldValues = new
        {
            opportunity.OpportunityName,
            opportunity.CustomerId,
            opportunity.LeadId,
            opportunity.Amount,
            opportunity.Probability,
            opportunity.Stage,
            opportunity.Status,
            opportunity.ExpectedCloseDate,
            opportunity.Source,
            opportunity.AssignedTo
        };

        opportunity.OpportunityName =
            model.OpportunityName.Trim();

        opportunity.CustomerId = model.CustomerId;
        opportunity.LeadId = model.LeadId;

        opportunity.Amount = model.Amount;
        opportunity.Probability = model.Probability;

        opportunity.Stage = model.Stage;
        opportunity.Status = GetStatusFromStage(model.Stage);

        opportunity.ExpectedCloseDate =
            model.ExpectedCloseDate.Date;

        opportunity.Source = model.Source;
        opportunity.Notes = model.Notes;

        opportunity.AssignedTo = model.AssignedTo;

        opportunity.ModifiedDate = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Audit: Opportunity updated.
        await _auditLogService.LogAsync(
            action: "Update",
            entityName: "Opportunity",
            recordId: opportunity.OpportunityId,
            oldValue: oldValues,
            newValue: new
            {
                opportunity.OpportunityName,
                opportunity.CustomerId,
                opportunity.LeadId,
                opportunity.Amount,
                opportunity.Probability,
                opportunity.Stage,
                opportunity.Status,
                opportunity.ExpectedCloseDate,
                opportunity.Source,
                opportunity.AssignedTo
            }
        );

        TempData["Success"] =
            "Opportunity updated successfully.";

        return RedirectToAction(nameof(Index));
    }

    // POST: /Opportunity/Delete/5
    // Only Admin and Manager can delete opportunities.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Delete(int id)
    {
        var opportunity = await _context.Opportunities
            .FirstOrDefaultAsync(
                o => o.OpportunityId == id);

        if (opportunity == null)
        {
            return NotFound();
        }

        // Capture details before deleting the record.
        var oldValues = new
        {
            opportunity.OpportunityName,
            opportunity.CustomerId,
            opportunity.LeadId,
            opportunity.Amount,
            opportunity.Probability,
            opportunity.Stage,
            opportunity.Status,
            opportunity.ExpectedCloseDate,
            opportunity.Source,
            opportunity.AssignedTo
        };

        _context.Opportunities.Remove(opportunity);

        await _context.SaveChangesAsync();

        // Audit: Opportunity deleted.
        await _auditLogService.LogAsync(
            action: "Delete",
            entityName: "Opportunity",
            recordId: id,
            oldValue: oldValues,
            newValue: "Opportunity deleted."
        );

        TempData["Success"] =
            "Opportunity deleted successfully.";

        return RedirectToAction(nameof(Index));
    }

    // Business validation
    private void ValidateOpportunity(
        OpportunityViewModel model)
    {
        if (!Stages.Contains(model.Stage))
        {
            ModelState.AddModelError(
                nameof(model.Stage),
                "Invalid opportunity stage.");
        }

        if (!Sources.Contains(model.Source))
        {
            ModelState.AddModelError(
                nameof(model.Source),
                "Invalid opportunity source.");
        }

        if (model.CustomerId == null &&
            model.LeadId == null)
        {
            ModelState.AddModelError(
                string.Empty,
                "Select at least one customer or lead.");
        }

        bool isActive =
            model.Stage != "Won" &&
            model.Stage != "Lost";

        if (isActive &&
            model.ExpectedCloseDate.Date < DateTime.Today)
        {
            ModelState.AddModelError(
                nameof(model.ExpectedCloseDate),
                "Expected close date cannot be in the past for an active opportunity.");
        }

        if (model.Amount <= 0)
        {
            ModelState.AddModelError(
                nameof(model.Amount),
                "Amount must be greater than zero.");
        }

        if (model.Probability < 0 ||
            model.Probability > 100)
        {
            ModelState.AddModelError(
                nameof(model.Probability),
                "Probability must be between 0 and 100.");
        }
    }

    // Derive opportunity status from its stage.
    private static string GetStatusFromStage(string stage)
    {
        return stage switch
        {
            "Won" => "Won",
            "Lost" => "Lost",
            _ => "Open"
        };
    }

    // Validate selected Customer, Lead and assigned user.
    private async Task<bool> ValidateReferencesAsync(
        OpportunityViewModel model)
    {
        if (model.CustomerId.HasValue)
        {
            var customerQuery =
                _context.Customers.AsQueryable();

            if (!IsAdminOrManager)
            {
                customerQuery = customerQuery.Where(
                    c => c.OwnerId == CurrentUserId);
            }

            bool customerExists =
                await customerQuery.AnyAsync(
                    c => c.CustomerId == model.CustomerId.Value);

            if (!customerExists)
            {
                ModelState.AddModelError(
                    nameof(model.CustomerId),
                    "Selected customer is unavailable.");
            }
        }

        if (model.LeadId.HasValue)
        {
            var leadQuery =
                _context.Leads.AsQueryable();

            if (!IsAdminOrManager)
            {
                leadQuery = leadQuery.Where(
                    l => l.AssignedTo == CurrentUserId);
            }

            bool leadExists =
                await leadQuery.AnyAsync(
                    l => l.LeadId == model.LeadId.Value);

            if (!leadExists)
            {
                ModelState.AddModelError(
                    nameof(model.LeadId),
                    "Selected lead is unavailable.");
            }
        }

        if (IsAdminOrManager &&
            !string.IsNullOrWhiteSpace(model.AssignedTo))
        {
            var user = await _userManager.FindByIdAsync(
                model.AssignedTo);

            if (user == null ||
                !user.IsActive ||
                !await _userManager.IsInRoleAsync(
                    user, "SalesExecutive"))
            {
                ModelState.AddModelError(
                    nameof(model.AssignedTo),
                    "Select an active Sales Executive.");
            }
        }

        return ModelState.IsValid;
    }

    // Populate dropdowns.
    private async Task LoadDropdownsAsync(
        OpportunityViewModel model)
    {
        var customers =
            _context.Customers.AsQueryable();

        var leads =
            _context.Leads.AsQueryable();

        if (!IsAdminOrManager)
        {
            customers = customers.Where(
                c => c.OwnerId == CurrentUserId);

            leads = leads.Where(
                l => l.AssignedTo == CurrentUserId);
        }

        model.Customers = await customers
            .OrderBy(c => c.CustomerName)
            .Select(c => new SelectListItem
            {
                Value = c.CustomerId.ToString(),
                Text = c.CustomerName,
                Selected = c.CustomerId == model.CustomerId
            })
            .ToListAsync();

        model.Leads = await leads
            .OrderBy(l => l.LeadName)
            .Select(l => new SelectListItem
            {
                Value = l.LeadId.ToString(),
                Text = l.LeadName,
                Selected = l.LeadId == model.LeadId
            })
            .ToListAsync();

        var salesUsers =
            await _userManager.GetUsersInRoleAsync(
                "SalesExecutive");

        model.SalesExecutives = salesUsers
            .Where(u => u.IsActive)
            .OrderBy(u => u.FullName)
            .Select(u => new SelectListItem
            {
                Value = u.Id,
                Text = u.FullName,
                Selected = u.Id == model.AssignedTo
            })
            .ToList();
    }
}
