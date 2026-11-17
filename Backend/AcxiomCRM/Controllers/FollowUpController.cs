
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
public class FollowUpController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AuditLogService _auditLogService;

    private string CurrentUserId =>
        _userManager.GetUserId(User) ?? string.Empty;

    private bool IsAdminOrManager =>
        User.IsInRole("Admin") || User.IsInRole("Manager");

    private static readonly string[] FollowUpTypes =
    {
        "Call", "Email", "Meeting", "Other"
    };

    private static readonly string[] FollowUpStatuses =
    {
        "Planned", "Completed", "Missed", "Cancelled"
    };

    public FollowUpController(
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
        string? statusFilter)
    {
        var query = ScopedFollowUps();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            searchTerm = searchTerm.Trim();

            query = query.Where(f =>
                f.Subject.Contains(searchTerm) ||
                (f.Customer != null &&
                 f.Customer.CustomerName.Contains(searchTerm)) ||
                (f.Lead != null &&
                 f.Lead.LeadName.Contains(searchTerm)));
        }

        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            query = query.Where(f => f.Status == statusFilter);
        }

        var allFollowUps = ScopedFollowUps();

        var model = new FollowUpListViewModel
        {
            FollowUps = await query
                .AsNoTracking()
                .OrderBy(f => f.FollowUpDate)
                .ToListAsync(),

            SearchTerm = searchTerm,
            StatusFilter = statusFilter,

            TotalFollowUps = await allFollowUps.CountAsync(),

            PlannedCount = await allFollowUps.CountAsync(
                f => f.Status == "Planned"),

            CompletedCount = await allFollowUps.CountAsync(
                f => f.Status == "Completed"),

            MissedCount = await allFollowUps.CountAsync(
                f => f.Status == "Missed"),

            OverdueCount = await allFollowUps.CountAsync(
                f => f.Status == "Planned" &&
                     f.FollowUpDate < DateTime.Now)
        };

        return View(model);
    }

    // ----------------------------------------------------
    // CREATE - GET
    // ----------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new FollowUpViewModel
        {
            FollowUpDate = DateTime.Today.AddDays(1),
            FollowUpType = "Call",
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
    public async Task<IActionResult> Create(FollowUpViewModel model)
    {
        if (!model.CustomerId.HasValue && !model.LeadId.HasValue)
        {
            ModelState.AddModelError(
                string.Empty,
                "Select at least one customer or lead.");
        }

        if (!FollowUpTypes.Contains(model.FollowUpType))
        {
            ModelState.AddModelError(
                nameof(model.FollowUpType),
                "Invalid follow-up type.");
        }

        if (model.FollowUpDate.Date < DateTime.Today)
        {
            ModelState.AddModelError(
                nameof(model.FollowUpDate),
                "Follow-up date cannot be in the past.");
        }

        await ValidateReferencesAsync(model);

        string? assignedUserId;

        if (IsAdminOrManager)
        {
            assignedUserId = model.AssignedUserId;

            if (string.IsNullOrWhiteSpace(assignedUserId) ||
                !await IsActiveSalesExecutiveAsync(assignedUserId))
            {
                ModelState.AddModelError(
                    nameof(model.AssignedUserId),
                    "Select an active Sales Executive.");
            }
        }
        else
        {
            assignedUserId = CurrentUserId;
        }

        if (!ModelState.IsValid)
        {
            await LoadDropdownsAsync(model);
            return View(model);
        }

        var followUp = new FollowUp
        {
            CustomerId = model.CustomerId,
            LeadId = model.LeadId,
            FollowUpDate = model.FollowUpDate,
            Subject = model.Subject.Trim(),
            FollowUpType = model.FollowUpType,
            Status = "Planned",
            AssignedUserId = assignedUserId,
            Notes = model.Notes
        };

        _context.FollowUps.Add(followUp);
        await _context.SaveChangesAsync();

        // AUDIT: FOLLOW-UP CREATED
        await _auditLogService.LogAsync(
            action: "Create",
            entityName: "FollowUp",
            recordId: followUp.FollowUpId,
            newValue: new
            {
                followUp.Subject,
                followUp.FollowUpType,
                followUp.FollowUpDate,
                followUp.Status,
                followUp.CustomerId,
                followUp.LeadId,
                followUp.AssignedUserId
            });

        TempData["SuccessMessage"] =
            "Follow-up created successfully.";

        return RedirectToAction(nameof(Index));
    }

    // ----------------------------------------------------
    // DETAILS
    // ----------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var followUp = await ScopedFollowUps()
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.FollowUpId == id);

        if (followUp == null)
            return NotFound();

        return View(followUp);
    }

    // ----------------------------------------------------
    // EDIT - GET
    // ----------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var followUp = await ScopedFollowUps()
            .FirstOrDefaultAsync(f => f.FollowUpId == id);

        if (followUp == null)
            return NotFound();

        var model = new FollowUpViewModel
        {
            FollowUpId = followUp.FollowUpId,
            CustomerId = followUp.CustomerId,
            LeadId = followUp.LeadId,
            FollowUpDate = followUp.FollowUpDate,
            Subject = followUp.Subject,
            FollowUpType = followUp.FollowUpType,
            Status = followUp.Status,
            AssignedUserId = followUp.AssignedUserId,
            Notes = followUp.Notes
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
        FollowUpViewModel model)
    {
        if (id != model.FollowUpId)
            return BadRequest();

        var followUp = await ScopedFollowUps()
            .FirstOrDefaultAsync(f => f.FollowUpId == id);

        if (followUp == null)
            return NotFound();

        if (!model.CustomerId.HasValue && !model.LeadId.HasValue)
        {
            ModelState.AddModelError(
                string.Empty,
                "Select at least one customer or lead.");
        }

        if (!FollowUpTypes.Contains(model.FollowUpType))
        {
            ModelState.AddModelError(
                nameof(model.FollowUpType),
                "Invalid follow-up type.");
        }

        if (!FollowUpStatuses.Contains(model.Status))
        {
            ModelState.AddModelError(
                nameof(model.Status),
                "Invalid follow-up status.");
        }

        if (model.Status == "Planned" &&
            model.FollowUpDate.Date < DateTime.Today)
        {
            ModelState.AddModelError(
                nameof(model.FollowUpDate),
                "A planned follow-up cannot have a past date.");
        }

        await ValidateReferencesAsync(model);

        if (IsAdminOrManager)
        {
            if (string.IsNullOrWhiteSpace(model.AssignedUserId) ||
                !await IsActiveSalesExecutiveAsync(model.AssignedUserId))
            {
                ModelState.AddModelError(
                    nameof(model.AssignedUserId),
                    "Select an active Sales Executive.");
            }
        }

        if (!ModelState.IsValid)
        {
            await LoadDropdownsAsync(model);
            return View(model);
        }

        // CAPTURE OLD VALUES
        var oldValues = new
        {
            followUp.Subject,
            followUp.FollowUpType,
            followUp.FollowUpDate,
            followUp.Status,
            followUp.CustomerId,
            followUp.LeadId,
            followUp.AssignedUserId
        };

        followUp.CustomerId = model.CustomerId;
        followUp.LeadId = model.LeadId;
        followUp.FollowUpDate = model.FollowUpDate;
        followUp.Subject = model.Subject.Trim();
        followUp.FollowUpType = model.FollowUpType;
        followUp.Notes = model.Notes;
        followUp.Status = model.Status;

        if (IsAdminOrManager)
        {
            followUp.AssignedUserId = model.AssignedUserId;
        }

        await _context.SaveChangesAsync();

        // AUDIT: FOLLOW-UP UPDATED
        await _auditLogService.LogAsync(
            action: "Update",
            entityName: "FollowUp",
            recordId: followUp.FollowUpId,
            oldValue: oldValues,
            newValue: new
            {
                followUp.Subject,
                followUp.FollowUpType,
                followUp.FollowUpDate,
                followUp.Status,
                followUp.CustomerId,
                followUp.LeadId,
                followUp.AssignedUserId
            });

        TempData["SuccessMessage"] =
            "Follow-up updated successfully.";

        return RedirectToAction(nameof(Index));
    }

    // ----------------------------------------------------
    // DELETE - GET
    // ----------------------------------------------------

    [Authorize(Roles = "Admin,Manager")]
    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var followUp = await _context.FollowUps
            .Include(f => f.Customer)
            .Include(f => f.Lead)
            .Include(f => f.AssignedUser)
            .FirstOrDefaultAsync(f => f.FollowUpId == id);

        if (followUp == null)
            return NotFound();

        return View(followUp);
    }

    // ----------------------------------------------------
    // DELETE - POST
    // ----------------------------------------------------

    [Authorize(Roles = "Admin,Manager")]
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var followUp = await _context.FollowUps
            .FirstOrDefaultAsync(f => f.FollowUpId == id);

        if (followUp == null)
            return NotFound();

        var oldValues = new
        {
            followUp.Subject,
            followUp.FollowUpType,
            followUp.FollowUpDate,
            followUp.Status,
            followUp.CustomerId,
            followUp.LeadId,
            followUp.AssignedUserId
        };

        _context.FollowUps.Remove(followUp);

        // DELETE AND AUDIT SAVED TOGETHER
        await _auditLogService.LogAsync(
            action: "Delete",
            entityName: "FollowUp",
            recordId: followUp.FollowUpId,
            oldValue: oldValues);

        TempData["SuccessMessage"] =
            "Follow-up deleted successfully.";

        return RedirectToAction(nameof(Index));
    }

    // ----------------------------------------------------
    // MARK AS COMPLETED
    // ----------------------------------------------------

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkCompleted(int id)
    {
        var followUp = await ScopedFollowUps()
            .FirstOrDefaultAsync(f => f.FollowUpId == id);

        if (followUp == null)
            return NotFound();

        if (followUp.Status != "Planned")
        {
            TempData["ErrorMessage"] =
                "Only planned follow-ups can be completed.";

            return RedirectToAction(nameof(Index));
        }

        var oldStatus = followUp.Status;

        followUp.Status = "Completed";

        await _context.SaveChangesAsync();

        // AUDIT: FOLLOW-UP COMPLETED
        await _auditLogService.LogAsync(
            action: "Complete",
            entityName: "FollowUp",
            recordId: followUp.FollowUpId,
            oldValue: new { Status = oldStatus },
            newValue: new { Status = followUp.Status });

        TempData["SuccessMessage"] =
            "Follow-up marked as completed.";

        return RedirectToAction(nameof(Index));
    }

    // ----------------------------------------------------
    // MARK AS MISSED
    // ----------------------------------------------------

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkMissed(int id)
    {
        var followUp = await ScopedFollowUps()
            .FirstOrDefaultAsync(f => f.FollowUpId == id);

        if (followUp == null)
            return NotFound();

        if (followUp.Status != "Planned")
        {
            TempData["ErrorMessage"] =
                "Only planned follow-ups can be marked as missed.";

            return RedirectToAction(nameof(Index));
        }

        var oldStatus = followUp.Status;

        followUp.Status = "Missed";

        await _context.SaveChangesAsync();

        // AUDIT: FOLLOW-UP MARKED AS MISSED
        await _auditLogService.LogAsync(
            action: "Missed",
            entityName: "FollowUp",
            recordId: followUp.FollowUpId,
            oldValue: new { Status = oldStatus },
            newValue: new { Status = followUp.Status });

        TempData["SuccessMessage"] =
            "Follow-up marked as missed.";

        return RedirectToAction(nameof(Index));
    }

    // ----------------------------------------------------
    // SECURITY: SCOPE FOLLOW-UPS BY ROLE
    // ----------------------------------------------------

    private IQueryable<FollowUp> ScopedFollowUps()
    {
        var query = _context.FollowUps
            .Include(f => f.Customer)
            .Include(f => f.Lead)
            .Include(f => f.AssignedUser)
            .AsQueryable();

        if (IsAdminOrManager)
            return query;

        return query.Where(
            f => f.AssignedUserId == CurrentUserId);
    }

    // ----------------------------------------------------
    // SECURITY: VALIDATE CUSTOMER AND LEAD OWNERSHIP
    // ----------------------------------------------------

    private async Task<bool> ValidateReferencesAsync(
        FollowUpViewModel model)
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

                return false;
            }

            if (!IsAdminOrManager &&
                customer.OwnerId != CurrentUserId)
            {
                ModelState.AddModelError(
                    nameof(model.CustomerId),
                    "You cannot create a follow-up for this customer.");

                return false;
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

                return false;
            }

            if (!IsAdminOrManager &&
                lead.AssignedTo != CurrentUserId)
            {
                ModelState.AddModelError(
                    nameof(model.LeadId),
                    "You cannot create a follow-up for this lead.");

                return false;
            }
        }

        return true;
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
        FollowUpViewModel model)
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
                    Selected = u.Id == model.AssignedUserId
                })
                .ToList();
        }
    }
}
