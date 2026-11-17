
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
public class LeadController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AuditLogService _auditLogService;

    private static readonly string[] ValidStatuses =
    {
        "New", "Contacted", "Qualified",
        "Unqualified", "Converted", "Lost"
    };

    private static readonly string[] ValidSources =
    {
        "Website", "Referral", "Social Media",
        "Advertisement", "Cold Call", "Other"
    };

    private static readonly string[] ValidPriorities =
    {
        "Low", "Medium", "High"
    };

    public LeadController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        AuditLogService auditLogService)
    {
        _context = context;
        _userManager = userManager;
        _auditLogService = auditLogService;
    }

    // ==========================================
    // 1. VIEW AND SEARCH LEADS
    // ==========================================

    public async Task<IActionResult> Index(string? search)
    {
        var query = _context.Leads
            .Include(l => l.AssignedUser)
            .AsQueryable();

        if (User.IsInRole("SalesExecutive"))
        {
            var userId = _userManager.GetUserId(User);
            query = query.Where(l => l.AssignedTo == userId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();

            query = query.Where(l =>
                l.LeadName.Contains(search) ||
                l.LeadCode.Contains(search) ||
                l.CompanyName.Contains(search) ||
                l.Status.Contains(search) ||
                (l.AssignedUser != null &&
                 l.AssignedUser.FullName.Contains(search)));
        }

        var leads = await query
            .OrderByDescending(l => l.CreatedDate)
            .Select(l => new LeadListViewModel
            {
                LeadId = l.LeadId,
                LeadCode = l.LeadCode,
                LeadName = l.LeadName,
                Email = l.Email,
                Phone = l.Phone,
                CompanyName = l.CompanyName,
                Source = l.Source,
                Status = l.Status,
                Priority = l.Priority,
                ExpectedValue = l.ExpectedValue,

                AssignedToName = l.AssignedUser != null
                    ? l.AssignedUser.FullName
                    : "Unassigned",

                CreatedDate = l.CreatedDate
            })
            .ToListAsync();

        ViewBag.Search = search;

        return View(leads);
    }

    // ==========================================
    // 2. CREATE LEAD
    // ==========================================

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new LeadViewModel();

        await LoadSalesExecutives(model);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(LeadViewModel model)
    {
        ValidateCommonFields(model);

        if (model.Status != "New")
        {
            ModelState.AddModelError(
                nameof(model.Status),
                "A newly created lead must have New status.");
        }

        var leadCode = model.LeadCode?.Trim() ?? string.Empty;

        if (await _context.Leads.AnyAsync(
            l => l.LeadCode == leadCode))
        {
            ModelState.AddModelError(
                nameof(model.LeadCode),
                "This lead code already exists.");
        }

        string? assignedTo = _userManager.GetUserId(User);

        if (User.IsInRole("Admin") || User.IsInRole("Manager"))
        {
            assignedTo = await ValidateSalesExecutive(model.AssignedTo);

            if (assignedTo == null)
            {
                ModelState.AddModelError(
                    nameof(model.AssignedTo),
                    "Select a valid active Sales Executive.");
            }
        }

        if (!ModelState.IsValid)
        {
            await LoadSalesExecutives(model);
            return View(model);
        }

        var lead = new Lead
        {
            LeadCode = leadCode,
            LeadName = model.LeadName.Trim(),
            Email = model.Email?.Trim(),
            Phone = model.Phone?.Trim(),
            CompanyName = model.CompanyName?.Trim() ?? string.Empty,
            Source = model.Source,
            Status = "New",
            Priority = model.Priority,
            ExpectedValue = model.ExpectedValue,
            AssignedTo = assignedTo,
            CreatedDate = DateTime.UtcNow
        };

        _context.Leads.Add(lead);
        await _context.SaveChangesAsync();

        // AUDIT: LEAD CREATED
        await _auditLogService.LogAsync(
            action: "Create",
            entityName: "Lead",
            recordId: lead.LeadId,
            newValue: new
            {
                lead.LeadCode,
                lead.LeadName,
                lead.CompanyName,
                lead.Source,
                lead.Status,
                lead.Priority,
                lead.ExpectedValue,
                lead.AssignedTo
            });

        TempData["SuccessMessage"] = "Lead created successfully.";

        return RedirectToAction(nameof(Index));
    }

    // ==========================================
    // 3. EDIT LEAD
    // ==========================================

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var lead = await _context.Leads.FindAsync(id);

        if (lead == null)
            return NotFound();

        if (!CanAccessLead(lead))
            return Forbid();

        var model = new LeadViewModel
        {
            LeadId = lead.LeadId,
            LeadCode = lead.LeadCode,
            LeadName = lead.LeadName,
            Email = lead.Email,
            Phone = lead.Phone,
            CompanyName = lead.CompanyName,
            Source = lead.Source,
            Status = lead.Status,
            Priority = lead.Priority,
            ExpectedValue = lead.ExpectedValue,
            AssignedTo = lead.AssignedTo
        };

        await LoadSalesExecutives(model);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        LeadViewModel model)
    {
        if (id != model.LeadId)
            return BadRequest();

        var lead = await _context.Leads.FindAsync(id);

        if (lead == null)
            return NotFound();

        if (!CanAccessLead(lead))
            return Forbid();

        ValidateCommonFields(model);

        var leadCode = model.LeadCode?.Trim() ?? string.Empty;

        if (await _context.Leads.AnyAsync(
            l => l.LeadCode == leadCode &&
                 l.LeadId != id))
        {
            ModelState.AddModelError(
                nameof(model.LeadCode),
                "This lead code already exists.");
        }

        if (!IsValidStatusTransition(lead.Status, model.Status))
        {
            ModelState.AddModelError(
                nameof(model.Status),
                $"The status cannot be changed from {lead.Status} to {model.Status}.");
        }

        string? newAssignedTo = lead.AssignedTo;

        if (User.IsInRole("Admin") || User.IsInRole("Manager"))
        {
            newAssignedTo =
                await ValidateSalesExecutive(model.AssignedTo);

            if (newAssignedTo == null)
            {
                ModelState.AddModelError(
                    nameof(model.AssignedTo),
                    "Select a valid active Sales Executive.");
            }
        }

        if (!ModelState.IsValid)
        {
            await LoadSalesExecutives(model);
            return View(model);
        }

        // CAPTURE OLD VALUES
        var oldValues = new
        {
            lead.LeadCode,
            lead.LeadName,
            lead.CompanyName,
            lead.Source,
            lead.Status,
            lead.Priority,
            lead.ExpectedValue,
            lead.AssignedTo
        };

        lead.LeadCode = leadCode;
        lead.LeadName = model.LeadName.Trim();
        lead.Email = model.Email?.Trim();
        lead.Phone = model.Phone?.Trim();
        lead.CompanyName = model.CompanyName?.Trim() ?? string.Empty;
        lead.Source = model.Source;
        lead.Status = model.Status;
        lead.Priority = model.Priority;
        lead.ExpectedValue = model.ExpectedValue;
        lead.AssignedTo = newAssignedTo;

        await _context.SaveChangesAsync();

        // AUDIT: LEAD UPDATED
        await _auditLogService.LogAsync(
            action: "Update",
            entityName: "Lead",
            recordId: lead.LeadId,
            oldValue: oldValues,
            newValue: new
            {
                lead.LeadCode,
                lead.LeadName,
                lead.CompanyName,
                lead.Source,
                lead.Status,
                lead.Priority,
                lead.ExpectedValue,
                lead.AssignedTo
            });

        TempData["SuccessMessage"] = "Lead updated successfully.";

        return RedirectToAction(nameof(Index));
    }

    // ==========================================
    // 4. DELETE LEAD
    // ==========================================

    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var lead = await _context.Leads.FindAsync(id);

        if (lead == null)
            return NotFound();

        bool hasOpportunities =
            await _context.Opportunities.AnyAsync(o => o.LeadId == id);

        bool hasFollowUps =
            await _context.FollowUps.AnyAsync(f => f.LeadId == id);

        bool hasActivities =
            await _context.Activities.AnyAsync(a => a.LeadId == id);

        if (hasOpportunities || hasFollowUps || hasActivities)
        {
            TempData["ErrorMessage"] =
                "This lead cannot be deleted because related CRM records exist.";

            return RedirectToAction(nameof(Index));
        }

        var oldValues = new
        {
            lead.LeadCode,
            lead.LeadName,
            lead.CompanyName,
            lead.Source,
            lead.Status,
            lead.Priority,
            lead.ExpectedValue,
            lead.AssignedTo
        };

        _context.Leads.Remove(lead);

        // AUDIT AND DELETE SAVED TOGETHER
        await _auditLogService.LogAsync(
            action: "Delete",
            entityName: "Lead",
            recordId: lead.LeadId,
            oldValue: oldValues);

        TempData["SuccessMessage"] = "Lead deleted successfully.";

        return RedirectToAction(nameof(Index));
    }

    // ==========================================
    // 5. LEAD CONVERSION - GET
    // ==========================================

    [HttpGet]
    public async Task<IActionResult> Convert(int id)
    {
        var lead = await _context.Leads.FindAsync(id);

        if (lead == null)
            return NotFound();

        if (!CanAccessLead(lead))
            return Forbid();

        if (lead.Status != "Qualified")
        {
            TempData["ErrorMessage"] =
                "Only Qualified leads can be converted.";

            return RedirectToAction(nameof(Index));
        }

        if (string.IsNullOrWhiteSpace(lead.Email) ||
            string.IsNullOrWhiteSpace(lead.Phone))
        {
            TempData["ErrorMessage"] =
                "Lead email and phone are required for conversion.";

            return RedirectToAction(nameof(Index));
        }

        var model = new LeadConversionViewModel
        {
            LeadId = lead.LeadId,
            LeadName = lead.LeadName,
            CompanyName = lead.CompanyName,
            Email = lead.Email,
            Phone = lead.Phone,
            OpportunityName = lead.CompanyName + " - " + lead.LeadName,
            Amount = lead.ExpectedValue > 0 ? lead.ExpectedValue : 1,
            Probability = 50,
            ExpectedCloseDate = DateTime.Today.AddDays(30)
        };

        return View(model);
    }

    // ==========================================
    // 6. LEAD CONVERSION - POST
    // ==========================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Convert(
        int id,
        LeadConversionViewModel model)
    {
        if (id != model.LeadId)
            return BadRequest();

        var lead = await _context.Leads.FindAsync(id);

        if (lead == null)
            return NotFound();

        if (!CanAccessLead(lead))
            return Forbid();

        if (lead.Status != "Qualified")
        {
            TempData["ErrorMessage"] =
                "Only Qualified leads can be converted.";

            return RedirectToAction(nameof(Index));
        }

        if (string.IsNullOrWhiteSpace(lead.Email) ||
            string.IsNullOrWhiteSpace(lead.Phone))
        {
            ModelState.AddModelError(
                string.Empty,
                "Lead email and phone are required.");
        }

        if (model.ExpectedCloseDate.Date < DateTime.Today)
        {
            ModelState.AddModelError(
                nameof(model.ExpectedCloseDate),
                "Expected close date cannot be in the past.");
        }

        if (!ModelState.IsValid)
        {
            model.LeadName = lead.LeadName;
            model.CompanyName = lead.CompanyName;
            model.Email = lead.Email;
            model.Phone = lead.Phone;

            return View(model);
        }

        var email = lead.Email!.Trim();

        var existingCustomer = await _context.Customers
            .AnyAsync(c => c.Email.ToLower() == email.ToLower());

        if (existingCustomer)
        {
            ModelState.AddModelError(
                string.Empty,
                "A customer with this email already exists. Resolve the existing customer before conversion.");

            model.LeadName = lead.LeadName;
            model.CompanyName = lead.CompanyName;
            model.Email = lead.Email;
            model.Phone = lead.Phone;

            return View(model);
        }

        var customerCode = "CUST-" + lead.LeadCode;

        if (await _context.Customers.AnyAsync(
            c => c.CustomerCode == customerCode))
        {
            ModelState.AddModelError(
                string.Empty,
                "The generated customer code already exists.");

            model.LeadName = lead.LeadName;
            model.CompanyName = lead.CompanyName;
            model.Email = lead.Email;
            model.Phone = lead.Phone;

            return View(model);
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            var currentUserId = _userManager.GetUserId(User);

            var oldLeadValues = new
            {
                lead.LeadCode,
                lead.LeadName,
                lead.CompanyName,
                lead.Status,
                lead.AssignedTo
            };

            // CREATE CUSTOMER
            var customer = new Customer
            {
                CustomerCode = customerCode,
                CustomerName = lead.LeadName,
                Email = email,
                Phone = lead.Phone!.Trim(),
                CompanyName = lead.CompanyName,
                Address = string.Empty,
                City = string.Empty,
                State = string.Empty,
                Status = "Active",
                OwnerId = lead.AssignedTo,
                CreatedDate = DateTime.UtcNow
            };

            _context.Customers.Add(customer);

            await _context.SaveChangesAsync();

            // CREATE OPPORTUNITY
            var opportunity = new Opportunity
            {
                OpportunityName = model.OpportunityName.Trim(),
                CustomerId = customer.CustomerId,
                LeadId = lead.LeadId,
                Amount = model.Amount,
                Probability = model.Probability,
                Stage = "Qualification",
                ExpectedCloseDate = model.ExpectedCloseDate.Date,
                Status = "Open",
                AssignedTo = lead.AssignedTo,
                CreatedBy = currentUserId ?? string.Empty,
                CreatedDate = DateTime.UtcNow
            };

            _context.Opportunities.Add(opportunity);

            // UPDATE LEAD
            lead.Status = "Converted";

            await _context.SaveChangesAsync();

            // AUDIT: CUSTOMER CREATED THROUGH CONVERSION
            _context.AuditLogs.Add(new AuditLog
            {
                UserId = currentUserId,
                Action = "Create",
                EntityName = "Customer",
                RecordId = customer.CustomerId,
                NewValue = System.Text.Json.JsonSerializer.Serialize(new
                {
                    customer.CustomerCode,
                    customer.CustomerName,
                    customer.CompanyName,
                    customer.Status,
                    customer.OwnerId
                }),
                CreatedDate = DateTime.UtcNow,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                Result = "Success"
            });

            // AUDIT: OPPORTUNITY CREATED THROUGH CONVERSION
            _context.AuditLogs.Add(new AuditLog
            {
                UserId = currentUserId,
                Action = "Create",
                EntityName = "Opportunity",
                RecordId = opportunity.OpportunityId,
                NewValue = System.Text.Json.JsonSerializer.Serialize(new
                {
                    opportunity.OpportunityName,
                    opportunity.Amount,
                    opportunity.Probability,
                    opportunity.Stage,
                    opportunity.Status,
                    opportunity.CustomerId,
                    opportunity.LeadId,
                    opportunity.AssignedTo
                }),
                CreatedDate = DateTime.UtcNow,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                Result = "Success"
            });

            // AUDIT: LEAD CONVERTED
            _context.AuditLogs.Add(new AuditLog
            {
                UserId = currentUserId,
                Action = "Convert",
                EntityName = "Lead",
                RecordId = lead.LeadId,
                OldValue = System.Text.Json.JsonSerializer.Serialize(oldLeadValues),
                NewValue = System.Text.Json.JsonSerializer.Serialize(new
                {
                    lead.LeadCode,
                    lead.LeadName,
                    lead.CompanyName,
                    lead.Status,
                    lead.AssignedTo,
                    CustomerId = customer.CustomerId,
                    OpportunityId = opportunity.OpportunityId
                }),
                CreatedDate = DateTime.UtcNow,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                Result = "Success"
            });

            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            TempData["SuccessMessage"] =
                "Lead converted successfully into a Customer and Opportunity.";

            return RedirectToAction(nameof(Index));
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();

            ModelState.AddModelError(
                string.Empty,
                "Conversion failed. Please verify the information and try again.");

            model.LeadName = lead.LeadName;
            model.CompanyName = lead.CompanyName;
            model.Email = lead.Email;
            model.Phone = lead.Phone;

            return View(model);
        }
    }

    // ==========================================
    // 7. ROLE-BASED ACCESS CHECK
    // ==========================================

    private bool CanAccessLead(Lead lead)
    {
        if (User.IsInRole("Admin") ||
            User.IsInRole("Manager"))
        {
            return true;
        }

        var userId = _userManager.GetUserId(User);

        return lead.AssignedTo == userId;
    }

    // ==========================================
    // 8. VALIDATION
    // ==========================================

    private void ValidateCommonFields(LeadViewModel model)
    {
        if (!ValidSources.Contains(model.Source))
        {
            ModelState.AddModelError(
                nameof(model.Source),
                "Select a valid lead source.");
        }

        if (!ValidPriorities.Contains(model.Priority))
        {
            ModelState.AddModelError(
                nameof(model.Priority),
                "Select a valid priority.");
        }

        if (!ValidStatuses.Contains(model.Status))
        {
            ModelState.AddModelError(
                nameof(model.Status),
                "Select a valid lead status.");
        }

        if (model.ExpectedValue < 0)
        {
            ModelState.AddModelError(
                nameof(model.ExpectedValue),
                "Expected value cannot be negative.");
        }
    }

    // ==========================================
    // 9. LEAD STATUS TRANSITIONS
    // ==========================================

    private bool IsValidStatusTransition(
        string currentStatus,
        string newStatus)
    {
        if (currentStatus == newStatus)
            return true;

        return currentStatus switch
        {
            "New" =>
                newStatus is "Contacted"
                    or "Unqualified"
                    or "Lost",

            "Contacted" =>
                newStatus is "Qualified"
                    or "Unqualified"
                    or "Lost",

            "Qualified" =>
                newStatus is "Converted"
                    or "Lost",

            "Unqualified" =>
                newStatus is "New"
                    or "Lost",

            "Lost" => false,

            "Converted" => false,

            _ => false
        };
    }

    // ==========================================
    // 10. VALIDATE SALES EXECUTIVE
    // ==========================================

    private async Task<string?> ValidateSalesExecutive(
        string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return null;

        var user = await _userManager.FindByIdAsync(userId);

        if (user == null || !user.IsActive)
            return null;

        if (!await _userManager.IsInRoleAsync(
            user, "SalesExecutive"))
        {
            return null;
        }

        return user.Id;
    }

    // ==========================================
    // 11. LOAD SALES EXECUTIVES
    // ==========================================

    private async Task LoadSalesExecutives(
        LeadViewModel model)
    {
        var users = await _userManager.GetUsersInRoleAsync(
            "SalesExecutive");

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
