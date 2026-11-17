
using Microsoft.AspNetCore.Authentication.JwtBearer;
using AcxiomCRM.Authorization;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers.Api;

[ApiController]
[Route("api/leads")]
[Authorize(
    AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
    Roles = RoleConstants.AllRoles)]
public class LeadsApiController : ControllerBase
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

    public LeadsApiController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        AuditLogService auditLogService)
    {
        _context = context;
        _userManager = userManager;
        _auditLogService = auditLogService;
    }

    // =====================================================
    // HELPER METHODS
    // =====================================================

    private bool IsAdminOrManager()
    {
        return User.IsInRole(RoleConstants.Admin) ||
               User.IsInRole(RoleConstants.Manager);
    }

    private bool CanAccessLead(Lead lead)
    {
        if (IsAdminOrManager())
            return true;

        var currentUserId = _userManager.GetUserId(User);

        return lead.AssignedTo == currentUserId;
    }

    private static LeadApiResponse ToResponse(Lead lead)
    {
        return new LeadApiResponse
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
            AssignedTo = lead.AssignedTo,
            AssignedToName = lead.AssignedUser?.FullName ?? "Unassigned",
            CreatedDate = lead.CreatedDate
        };
    }

    private static bool IsValidSource(string? source)
    {
        return ValidSources.Contains(
            source?.Trim() ?? string.Empty,
            StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsValidPriority(string? priority)
    {
        return ValidPriorities.Contains(
            priority?.Trim() ?? string.Empty,
            StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsValidStatus(string? status)
    {
        return ValidStatuses.Contains(
            status?.Trim() ?? string.Empty,
            StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsValidStatusTransition(
        string currentStatus,
        string newStatus)
    {
        if (string.Equals(
            currentStatus,
            newStatus,
            StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

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

    private async Task<bool> IsValidSalesExecutiveAsync(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return false;

        var user = await _userManager.FindByIdAsync(userId);

        if (user == null || !user.IsActive)
            return false;

        return await _userManager.IsInRoleAsync(
            user,
            RoleConstants.SalesExecutive);
    }

    private ActionResult ValidationError(string message)
    {
        return BadRequest(new
        {
            message
        });
    }

    // =====================================================
    // GET: api/leads
    // List leads accessible to the current user.
    // Optional filters: search, status, priority.
    // =====================================================

    [HttpGet]
    public async Task<ActionResult<IEnumerable<LeadApiResponse>>> GetLeads(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] string? priority)
    {
        var query = _context.Leads
            .AsNoTracking()
            .Include(l => l.AssignedUser)
            .AsQueryable();

        // Sales Executives can see only their own leads.
        if (!IsAdminOrManager())
        {
            var userId = _userManager.GetUserId(User);

            query = query.Where(l => l.AssignedTo == userId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();

            query = query.Where(l =>
                l.LeadName.Contains(term) ||
                l.LeadCode.Contains(term) ||
                l.CompanyName.Contains(term) ||
                l.Status.Contains(term) ||
                (l.AssignedUser != null &&
                 l.AssignedUser.FullName.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var selectedStatus = status.Trim();
            query = query.Where(l => l.Status == selectedStatus);
        }

        if (!string.IsNullOrWhiteSpace(priority))
        {
            var selectedPriority = priority.Trim();
            query = query.Where(l => l.Priority == selectedPriority);
        }

        var leads = await query
            .OrderByDescending(l => l.CreatedDate)
            .ToListAsync();

        return Ok(leads.Select(ToResponse).ToList());
    }

    // =====================================================
    // GET: api/leads/{id}
    // Get a single lead.
    // =====================================================

    [HttpGet("{id:int}")]
    public async Task<ActionResult<LeadApiResponse>> GetLeadById(int id)
    {
        var lead = await _context.Leads
            .AsNoTracking()
            .Include(l => l.AssignedUser)
            .FirstOrDefaultAsync(l => l.LeadId == id);

        if (lead == null)
        {
            return NotFound(new
            {
                message = "Lead not found."
            });
        }

        if (!CanAccessLead(lead))
            return Forbid();

        return Ok(ToResponse(lead));
    }

    // =====================================================
    // POST: api/leads
    // Create a new lead.
    // New leads must have New status.
    // =====================================================

    [HttpPost]
    public async Task<ActionResult<LeadApiResponse>> CreateLead(
        [FromBody] LeadApiRequest request)
    {
        var leadCode = request.LeadCode.Trim();

        if (!IsValidSource(request.Source))
        {
            return ValidationError(
                "Select a valid lead source.");
        }

        if (!IsValidPriority(request.Priority))
        {
            return ValidationError(
                "Select a valid priority.");
        }

        if (!string.Equals(
            request.Status?.Trim(),
            "New",
            StringComparison.OrdinalIgnoreCase))
        {
            return ValidationError(
                "A newly created lead must have New status.");
        }

        if (request.ExpectedValue < 0)
        {
            return ValidationError(
                "Expected value cannot be negative.");
        }

        if (await _context.Leads.AnyAsync(
            l => l.LeadCode == leadCode))
        {
            return Conflict(new
            {
                message = "This lead code already exists."
            });
        }

        string? assignedTo;

        if (IsAdminOrManager())
        {
            assignedTo = request.AssignedTo?.Trim();

            if (!await IsValidSalesExecutiveAsync(assignedTo))
            {
                return ValidationError(
                    "Select a valid active Sales Executive.");
            }
        }
        else
        {
            // Sales Executive is automatically assigned the lead.
            assignedTo = _userManager.GetUserId(User);
        }

        var lead = new Lead
        {
            LeadCode = leadCode,
            LeadName = request.LeadName.Trim(),
            Email = request.Email?.Trim(),
            Phone = request.Phone?.Trim(),
            CompanyName = request.CompanyName.Trim(),
            Source = request.Source.Trim(),
            Status = "New",
            Priority = request.Priority.Trim(),
            ExpectedValue = request.ExpectedValue,
            AssignedTo = assignedTo,
            CreatedDate = DateTime.UtcNow
        };

        _context.Leads.Add(lead);

        await _context.SaveChangesAsync();

        // Audit without recording contact information.
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

        await _context.Entry(lead)
            .Reference(l => l.AssignedUser)
            .LoadAsync();

        var response = ToResponse(lead);

        return CreatedAtAction(
            nameof(GetLeadById),
            new { id = lead.LeadId },
            response);
    }

    // =====================================================
    // PUT: api/leads/{id}
    // Update an existing lead.
    // =====================================================

    [HttpPut("{id:int}")]
    public async Task<ActionResult<LeadApiResponse>> UpdateLead(
        int id,
        [FromBody] LeadApiRequest request)
    {
        var lead = await _context.Leads
            .Include(l => l.AssignedUser)
            .FirstOrDefaultAsync(l => l.LeadId == id);

        if (lead == null)
        {
            return NotFound(new
            {
                message = "Lead not found."
            });
        }

        if (!CanAccessLead(lead))
            return Forbid();

        var leadCode = request.LeadCode.Trim();

        if (!IsValidSource(request.Source))
        {
            return ValidationError(
                "Select a valid lead source.");
        }

        if (!IsValidPriority(request.Priority))
        {
            return ValidationError(
                "Select a valid priority.");
        }

        if (!IsValidStatus(request.Status))
        {
            return ValidationError(
                "Select a valid lead status.");
        }

        if (request.ExpectedValue < 0)
        {
            return ValidationError(
                "Expected value cannot be negative.");
        }

        if (await _context.Leads.AnyAsync(l =>
            l.LeadCode == leadCode &&
            l.LeadId != id))
        {
            return Conflict(new
            {
                message = "This lead code already exists."
            });
        }

        if (!IsValidStatusTransition(lead.Status, request.Status.Trim()))
        {
            return ValidationError(
                $"The status cannot be changed from {lead.Status} to {request.Status}.");
        }

        string? newAssignedTo = lead.AssignedTo;

        if (IsAdminOrManager())
        {
            newAssignedTo = request.AssignedTo?.Trim();

            if (!await IsValidSalesExecutiveAsync(newAssignedTo))
            {
                return ValidationError(
                    "Select a valid active Sales Executive.");
            }
        }

        // Sales Executives retain the existing assignment.
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
        lead.LeadName = request.LeadName.Trim();
        lead.Email = request.Email?.Trim();
        lead.Phone = request.Phone?.Trim();
        lead.CompanyName = request.CompanyName.Trim();
        lead.Source = request.Source.Trim();
        lead.Status = request.Status.Trim();
        lead.Priority = request.Priority.Trim();
        lead.ExpectedValue = request.ExpectedValue;
        lead.AssignedTo = newAssignedTo;

        await _context.SaveChangesAsync();

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

        await _context.Entry(lead)
            .Reference(l => l.AssignedUser)
            .LoadAsync();

        return Ok(ToResponse(lead));
    }

    // =====================================================
    // DELETE: api/leads/{id}
    // Only Admin and Manager can delete.
    // Related records prevent deletion.
    // =====================================================

    [HttpDelete("{id:int}")]
    [Authorize(Roles = RoleConstants.AdminOrManager)]
    public async Task<IActionResult> DeleteLead(int id)
    {
        var lead = await _context.Leads
            .FirstOrDefaultAsync(l => l.LeadId == id);

        if (lead == null)
        {
            return NotFound(new
            {
                message = "Lead not found."
            });
        }

        bool hasOpportunities =
            await _context.Opportunities
                .AnyAsync(o => o.LeadId == id);

        bool hasFollowUps =
            await _context.FollowUps
                .AnyAsync(f => f.LeadId == id);

        bool hasActivities =
            await _context.Activities
                .AnyAsync(a => a.LeadId == id);

        if (hasOpportunities || hasFollowUps || hasActivities)
        {
            return Conflict(new
            {
                message = "This lead cannot be deleted because related CRM records exist."
            });
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

        try
        {
            // AuditLogService saves pending deletion and audit entry.
            await _auditLogService.LogAsync(
                action: "Delete",
                entityName: "Lead",
                recordId: lead.LeadId,
                oldValue: oldValues);
        }
        catch (DbUpdateException)
        {
            return Conflict(new
            {
                message = "This lead cannot be deleted because related CRM records exist."
            });
        }

        return Ok(new
        {
            message = "Lead deleted successfully."
        });
    }
}
