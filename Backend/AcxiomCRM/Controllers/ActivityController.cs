
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize(Roles = "Admin,Manager,SalesExecutive")]
public class ActivityController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AuditLogService _auditLogService;

    private string CurrentUserId =>
        _userManager.GetUserId(User) ?? string.Empty;

    private bool IsAdminOrManager =>
        User.IsInRole("Admin") || User.IsInRole("Manager");

    private static readonly string[] ActivityTypes =
    {
        "Call", "Meeting", "Email", "Task"
    };

    private static readonly string[] ActivityStatuses =
    {
        "Planned", "Completed", "Cancelled"
    };

    public ActivityController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        AuditLogService auditLogService)
    {
        _context = context;
        _userManager = userManager;
        _auditLogService = auditLogService;
    }

    // ----------------------------------------------------
    // INDEX
    // ----------------------------------------------------

    public async Task<IActionResult> Index(
        string? searchTerm,
        string? statusFilter,
        string? typeFilter)
    {
        var query = ScopedActivities();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            searchTerm = searchTerm.Trim();

            query = query.Where(a =>
                a.Subject.Contains(searchTerm) ||
                (a.Description != null &&
                 a.Description.Contains(searchTerm)) ||
                (a.Customer != null &&
                 a.Customer.CustomerName.Contains(searchTerm)) ||
                (a.Lead != null &&
                 a.Lead.LeadName.Contains(searchTerm)));
        }

        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            query = query.Where(a => a.Status == statusFilter);
        }

        if (!string.IsNullOrWhiteSpace(typeFilter))
        {
            query = query.Where(a => a.ActivityType == typeFilter);
        }

        var allActivities = ScopedActivities();

        var model = new ActivityListViewModel
        {
            Activities = await query
                .AsNoTracking()
                .OrderByDescending(a => a.ActivityDate)
                .ToListAsync(),

            SearchTerm = searchTerm,
            StatusFilter = statusFilter,
            TypeFilter = typeFilter,

            TotalActivities = await allActivities.CountAsync(),

            PlannedCount = await allActivities.CountAsync(
                a => a.Status == "Planned"),

            CompletedCount = await allActivities.CountAsync(
                a => a.Status == "Completed"),

            CancelledCount = await allActivities.CountAsync(
                a => a.Status == "Cancelled")
        };

        return View(model);
    }

    // ----------------------------------------------------
    // CREATE - GET
    // ----------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new ActivityViewModel
        {
            ActivityDate = DateTime.Today,
            ActivityType = "Call",
            Status = "Planned"
        };

        await LoadDropdownsAsync(model);

        return View(model);
    }

    // ----------------------------------------------------
    // CREATE - POST
    // ----------------------------------------------------

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ActivityViewModel model)
    {
        if (!model.CustomerId.HasValue &&
            !model.LeadId.HasValue)
        {
            ModelState.AddModelError(
                string.Empty,
                "Select at least one customer or lead.");
        }

        if (!ActivityTypes.Contains(model.ActivityType))
        {
            ModelState.AddModelError(
                nameof(model.ActivityType),
                "Invalid activity type.");
        }

        await ValidateReferencesAsync(model);

        string? assignedTo;

        if (IsAdminOrManager)
        {
            assignedTo = model.AssignedTo;

            if (string.IsNullOrWhiteSpace(assignedTo) ||
                !await IsActiveSalesExecutiveAsync(assignedTo))
            {
                ModelState.AddModelError(
                    nameof(model.AssignedTo),
                    "Select an active Sales Executive.");
            }
        }
        else
        {
            assignedTo = CurrentUserId;
        }

        if (!ModelState.IsValid)
        {
            await LoadDropdownsAsync(model);
            return View(model);
        }

        var activity = new Activity
        {
            ActivityType = model.ActivityType,
            Subject = model.Subject.Trim(),
            Description = model.Description,
            ActivityDate = model.ActivityDate,
            CustomerId = model.CustomerId,
            LeadId = model.LeadId,
            AssignedTo = assignedTo,
            Status = "Planned"
        };

        _context.Activities.Add(activity);

        await _context.SaveChangesAsync();

        await _auditLogService.LogAsync(
            action: "Create",
            entityName: "Activity",
            recordId: activity.ActivityId,
            newValue: new
            {
                activity.ActivityType,
                activity.Subject,
                activity.ActivityDate,
                activity.CustomerId,
                activity.LeadId,
                activity.AssignedTo,
                activity.Status
            }
        );

        TempData["SuccessMessage"] =
            "Activity created successfully.";

        return RedirectToAction(nameof(Index));
    }

    // ----------------------------------------------------
    // DETAILS
    // ----------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var activity = await ScopedActivities()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.ActivityId == id);

        if (activity == null)
            return NotFound();

        return View(activity);
    }

    // ----------------------------------------------------
    // EDIT - GET
    // ----------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var activity = await ScopedActivities()
            .FirstOrDefaultAsync(a => a.ActivityId == id);

        if (activity == null)
            return NotFound();

        var model = new ActivityViewModel
        {
            ActivityId = activity.ActivityId,
            ActivityType = activity.ActivityType,
            Subject = activity.Subject,
            Description = activity.Description,
            ActivityDate = activity.ActivityDate,
            CustomerId = activity.CustomerId,
            LeadId = activity.LeadId,
            AssignedTo = activity.AssignedTo,
            Status = activity.Status
        };

        await LoadDropdownsAsync(model);

        return View(model);
    }

    // ----------------------------------------------------
    // EDIT - POST
    // ----------------------------------------------------

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        ActivityViewModel model)
    {
        if (id != model.ActivityId)
            return BadRequest();

        var activity = await ScopedActivities()
            .FirstOrDefaultAsync(a => a.ActivityId == id);

        if (activity == null)
            return NotFound();

        if (!model.CustomerId.HasValue &&
            !model.LeadId.HasValue)
        {
            ModelState.AddModelError(
                string.Empty,
                "Select at least one customer or lead.");
        }

        if (!ActivityTypes.Contains(model.ActivityType))
        {
            ModelState.AddModelError(
                nameof(model.ActivityType),
                "Invalid activity type.");
        }

        if (!ActivityStatuses.Contains(model.Status))
        {
            ModelState.AddModelError(
                nameof(model.Status),
                "Invalid activity status.");
        }

        await ValidateReferencesAsync(model);

        if (IsAdminOrManager)
        {
            if (string.IsNullOrWhiteSpace(model.AssignedTo) ||
                !await IsActiveSalesExecutiveAsync(model.AssignedTo))
            {
                ModelState.AddModelError(
                    nameof(model.AssignedTo),
                    "Select an active Sales Executive.");
            }
        }

        if (!ModelState.IsValid)
        {
            await LoadDropdownsAsync(model);
            return View(model);
        }

        // Capture old values before updating.
        var oldValues = new
        {
            activity.ActivityType,
            activity.Subject,
            activity.ActivityDate,
            activity.CustomerId,
            activity.LeadId,
            activity.AssignedTo,
            activity.Status
        };

        activity.ActivityType = model.ActivityType;
        activity.Subject = model.Subject.Trim();
        activity.Description = model.Description;
        activity.ActivityDate = model.ActivityDate;
        activity.CustomerId = model.CustomerId;
        activity.LeadId = model.LeadId;
        activity.Status = model.Status;

        // Only Admin and Manager can reassign activities.
        if (IsAdminOrManager)
        {
            activity.AssignedTo = model.AssignedTo;
        }

        await _context.SaveChangesAsync();

        await _auditLogService.LogAsync(
            action: "Update",
            entityName: "Activity",
            recordId: activity.ActivityId,
            oldValue: oldValues,
            newValue: new
            {
                activity.ActivityType,
                activity.Subject,
                activity.ActivityDate,
                activity.CustomerId,
                activity.LeadId,
                activity.AssignedTo,
                activity.Status
            }
        );

        TempData["SuccessMessage"] =
            "Activity updated successfully.";

        return RedirectToAction(nameof(Index));
    }

    // ----------------------------------------------------
    // DELETE - GET
    // ----------------------------------------------------

    [Authorize(Roles = "Admin,Manager")]
    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var activity = await _context.Activities
            .Include(a => a.Customer)
            .Include(a => a.Lead)
            .Include(a => a.AssignedUser)
            .FirstOrDefaultAsync(a => a.ActivityId == id);

        if (activity == null)
            return NotFound();

        return View(activity);
    }

    // ----------------------------------------------------
    // DELETE - POST
    // ----------------------------------------------------

    [Authorize(Roles = "Admin,Manager")]
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var activity = await _context.Activities
            .FirstOrDefaultAsync(a => a.ActivityId == id);

        if (activity == null)
            return NotFound();

        // Capture details before deletion.
        var oldValues = new
        {
            activity.ActivityType,
            activity.Subject,
            activity.ActivityDate,
            activity.CustomerId,
            activity.LeadId,
            activity.AssignedTo,
            activity.Status
        };

        _context.Activities.Remove(activity);

        await _context.SaveChangesAsync();

        await _auditLogService.LogAsync(
            action: "Delete",
            entityName: "Activity",
            recordId: id,
            oldValue: oldValues,
            newValue: "Activity deleted."
        );

        TempData["SuccessMessage"] =
            "Activity deleted successfully.";

        return RedirectToAction(nameof(Index));
    }

    // ----------------------------------------------------
    // MARK AS COMPLETED
    // ----------------------------------------------------

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkCompleted(int id)
    {
        var activity = await ScopedActivities()
            .FirstOrDefaultAsync(a => a.ActivityId == id);

        if (activity == null)
            return NotFound();

        if (activity.Status != "Planned")
        {
            TempData["ErrorMessage"] =
                "Only planned activities can be completed.";

            return RedirectToAction(nameof(Index));
        }

        var oldStatus = activity.Status;

        activity.Status = "Completed";

        await _context.SaveChangesAsync();

        await _auditLogService.LogAsync(
            action: "Complete",
            entityName: "Activity",
            recordId: activity.ActivityId,
            oldValue: new { Status = oldStatus },
            newValue: new { Status = activity.Status }
        );

        TempData["SuccessMessage"] =
            "Activity marked as completed.";

        return RedirectToAction(nameof(Index));
    }

    // ----------------------------------------------------
    // MARK AS CANCELLED
    // ----------------------------------------------------

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkCancelled(int id)
    {
        var activity = await ScopedActivities()
            .FirstOrDefaultAsync(a => a.ActivityId == id);

        if (activity == null)
            return NotFound();

        if (activity.Status != "Planned")
        {
            TempData["ErrorMessage"] =
                "Only planned activities can be cancelled.";

            return RedirectToAction(nameof(Index));
        }

        var oldStatus = activity.Status;

        activity.Status = "Cancelled";

        await _context.SaveChangesAsync();

        await _auditLogService.LogAsync(
            action: "Cancel",
            entityName: "Activity",
            recordId: activity.ActivityId,
            oldValue: new { Status = oldStatus },
            newValue: new { Status = activity.Status }
        );

        TempData["SuccessMessage"] =
            "Activity cancelled successfully.";

        return RedirectToAction(nameof(Index));
    }

    // ----------------------------------------------------
    // SECURITY: SCOPE ACTIVITIES BY ROLE
    // ----------------------------------------------------

    private IQueryable<Activity> ScopedActivities()
    {
        var query = _context.Activities
            .Include(a => a.Customer)
            .Include(a => a.Lead)
            .Include(a => a.AssignedUser)
            .AsQueryable();

        if (IsAdminOrManager)
            return query;

        return query.Where(
            a => a.AssignedTo == CurrentUserId);
    }

    // ----------------------------------------------------
    // SECURITY: VALIDATE CUSTOMER AND LEAD OWNERSHIP
    // ----------------------------------------------------

    private async Task ValidateReferencesAsync(
        ActivityViewModel model)
    {
        if (model.CustomerId.HasValue)
        {
            var customer = await _context.Customers
                .FirstOrDefaultAsync(
                    c => c.CustomerId == model.CustomerId.Value);

            if (customer == null)
            {
                ModelState.AddModelError(
                    nameof(model.CustomerId),
                    "Selected customer does not exist.");
            }
            else if (!IsAdminOrManager &&
                     customer.OwnerId != CurrentUserId)
            {
                ModelState.AddModelError(
                    nameof(model.CustomerId),
                    "You cannot select another user's customer.");
            }
        }

        if (model.LeadId.HasValue)
        {
            var lead = await _context.Leads
                .FirstOrDefaultAsync(
                    l => l.LeadId == model.LeadId.Value);

            if (lead == null)
            {
                ModelState.AddModelError(
                    nameof(model.LeadId),
                    "Selected lead does not exist.");
            }
            else if (!IsAdminOrManager &&
                     lead.AssignedTo != CurrentUserId)
            {
                ModelState.AddModelError(
                    nameof(model.LeadId),
                    "You cannot select another user's lead.");
            }
        }
    }

    // ----------------------------------------------------
    // SECURITY: CHECK ACTIVE SALES EXECUTIVE
    // ----------------------------------------------------

    private async Task<bool> IsActiveSalesExecutiveAsync(
        string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);

        if (user == null || !user.IsActive)
            return false;

        return await _userManager.IsInRoleAsync(
            user, "SalesExecutive");
    }

    // ----------------------------------------------------
    // LOAD DROPDOWNS
    // ----------------------------------------------------

    private async Task LoadDropdownsAsync(
        ActivityViewModel model)
    {
        var customers = _context.Customers.AsQueryable();
        var leads = _context.Leads.AsQueryable();

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

        if (IsAdminOrManager)
        {
            var users = await _userManager
                .GetUsersInRoleAsync("SalesExecutive");

            model.SalesExecutives = users
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
}
