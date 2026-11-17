
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
[Route("api/followups")]
[Authorize(
    AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,Roles = RoleConstants.AllRoles)]
public class FollowUpsApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AuditLogService _auditLogService;

    private static readonly string[] ValidTypes =
    {
        "Call", "Email", "Meeting", "Other"
    };

    private static readonly string[] ValidStatuses =
    {
        "Planned", "Completed", "Missed", "Cancelled"
    };

    public FollowUpsApiController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        AuditLogService auditLogService)
    {
        _context = context;
        _userManager = userManager;
        _auditLogService = auditLogService;
    }

    private string CurrentUserId =>
        _userManager.GetUserId(User) ?? string.Empty;

    private bool IsAdminOrManager =>
        User.IsInRole(RoleConstants.Admin) ||
        User.IsInRole(RoleConstants.Manager);

    // =====================================================
    // HELPER: ROLE-BASED FOLLOW-UP QUERY
    // =====================================================

    private IQueryable<FollowUp> ScopedFollowUps()
    {
        var query = _context.FollowUps
            .Include(f => f.Customer)
            .Include(f => f.Lead)
            .Include(f => f.AssignedUser)
            .AsQueryable();

        if (!IsAdminOrManager)
        {
            query = query.Where(
                f => f.AssignedUserId == CurrentUserId);
        }

        return query;
    }

    // =====================================================
    // HELPER: RESPONSE MAPPING
    // =====================================================

    private static FollowUpApiResponse ToResponse(FollowUp followUp)
    {
        return new FollowUpApiResponse
        {
            FollowUpId = followUp.FollowUpId,

            CustomerId = followUp.CustomerId,
            CustomerName =
                followUp.Customer?.CustomerName ?? "Not linked",

            LeadId = followUp.LeadId,
            LeadName =
                followUp.Lead?.LeadName ?? "Not linked",

            FollowUpDate = followUp.FollowUpDate,
            Subject = followUp.Subject,
            FollowUpType = followUp.FollowUpType,
            Status = followUp.Status,

            AssignedUserId = followUp.AssignedUserId,
            AssignedUserName =
                followUp.AssignedUser?.FullName ?? "Unassigned",

            Notes = followUp.Notes
        };
    }

    // =====================================================
    // HELPER: CUSTOMER AND LEAD VALIDATION
    // =====================================================

    private async Task<string?> ValidateReferencesAsync(
        int? customerId,
        int? leadId)
    {
        if (!customerId.HasValue && !leadId.HasValue)
        {
            return "Select at least one customer or lead.";
        }

        if (customerId.HasValue)
        {
            var customerQuery = _context.Customers.AsQueryable();

            if (!IsAdminOrManager)
            {
                customerQuery = customerQuery.Where(
                    c => c.OwnerId == CurrentUserId);
            }

            bool customerExists = await customerQuery.AnyAsync(
                c => c.CustomerId == customerId.Value);

            if (!customerExists)
            {
                return "Selected customer is unavailable or you do not have access to it.";
            }
        }

        if (leadId.HasValue)
        {
            var leadQuery = _context.Leads.AsQueryable();

            if (!IsAdminOrManager)
            {
                leadQuery = leadQuery.Where(
                    l => l.AssignedTo == CurrentUserId);
            }

            bool leadExists = await leadQuery.AnyAsync(
                l => l.LeadId == leadId.Value);

            if (!leadExists)
            {
                return "Selected lead is unavailable or you do not have access to it.";
            }
        }

        return null;
    }

    // =====================================================
    // HELPER: REQUEST VALIDATION
    // =====================================================

    private string? ValidateRequest(
        FollowUpApiRequest request,
        bool isCreate)
    {
        if (string.IsNullOrWhiteSpace(request.Subject))
            return "Follow-up subject is required.";

        if (request.Subject.Trim().Length > 150)
            return "Subject cannot exceed 150 characters.";

        if (!ValidTypes.Contains(
            request.FollowUpType?.Trim() ?? string.Empty,
            StringComparer.OrdinalIgnoreCase))
        {
            return "Invalid follow-up type.";
        }

        if (isCreate)
        {
            if (!string.Equals(
                request.Status?.Trim(),
                "Planned",
                StringComparison.OrdinalIgnoreCase))
            {
                return "A newly created follow-up must have Planned status.";
            }

            if (request.FollowUpDate.Date < DateTime.Today)
                return "Follow-up date cannot be in the past.";
        }
        else
        {
            if (!ValidStatuses.Contains(
                request.Status?.Trim() ?? string.Empty,
                StringComparer.OrdinalIgnoreCase))
            {
                return "Invalid follow-up status.";
            }

            if (string.Equals(
                request.Status?.Trim(),
                "Planned",
                StringComparison.OrdinalIgnoreCase) &&
                request.FollowUpDate.Date < DateTime.Today)
            {
                return "A planned follow-up cannot have a past date.";
            }
        }

        if (request.Notes?.Length > 1000)
            return "Notes cannot exceed 1000 characters.";

        return null;
    }

    // =====================================================
    // HELPER: ACTIVE SALES EXECUTIVE VALIDATION
    // =====================================================

    private async Task<bool> IsActiveSalesExecutiveAsync(
        string? userId)
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

    // =====================================================
    // GET: api/followups
    // List follow-ups with optional search and status filter.
    // =====================================================

    [HttpGet]
    public async Task<ActionResult<IEnumerable<FollowUpApiResponse>>>
        GetFollowUps(
            [FromQuery] string? searchTerm,
            [FromQuery] string? status)
    {
        var query = ScopedFollowUps()
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();

            query = query.Where(f =>
                f.Subject.Contains(term) ||
                (f.Customer != null &&
                 f.Customer.CustomerName.Contains(term)) ||
                (f.Lead != null &&
                 f.Lead.LeadName.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var selectedStatus = status.Trim();

            if (!ValidStatuses.Contains(
                selectedStatus,
                StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    message = "Invalid follow-up status filter."
                });
            }

            query = query.Where(
                f => f.Status == selectedStatus);
        }

        var followUps = await query
            .OrderBy(f => f.FollowUpDate)
            .ToListAsync();

        return Ok(followUps.Select(ToResponse).ToList());
    }

    // =====================================================
    // GET: api/followups/{id}
    // Get one follow-up.
    // =====================================================

    [HttpGet("{id:int}")]
    public async Task<ActionResult<FollowUpApiResponse>>
        GetFollowUpById(int id)
    {
        var followUp = await ScopedFollowUps()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                f => f.FollowUpId == id);

        if (followUp == null)
        {
            return NotFound(new
            {
                message = "Follow-up not found."
            });
        }

        return Ok(ToResponse(followUp));
    }

    // =====================================================
    // POST: api/followups
    // Create a planned follow-up.
    // =====================================================

    [HttpPost]
    public async Task<ActionResult<FollowUpApiResponse>>
        CreateFollowUp(
            [FromBody] FollowUpApiRequest request)
    {
        var validationError = ValidateRequest(
            request,
            isCreate: true);

        if (validationError != null)
        {
            return BadRequest(new
            {
                message = validationError
            });
        }

        var referenceError = await ValidateReferencesAsync(
            request.CustomerId,
            request.LeadId);

        if (referenceError != null)
        {
            return BadRequest(new
            {
                message = referenceError
            });
        }

        string? assignedUserId;

        if (IsAdminOrManager)
        {
            assignedUserId = request.AssignedUserId?.Trim();

            if (!await IsActiveSalesExecutiveAsync(assignedUserId))
            {
                return BadRequest(new
                {
                    message = "Select an active Sales Executive."
                });
            }
        }
        else
        {
            assignedUserId = CurrentUserId;
        }

        var followUp = new FollowUp
        {
            CustomerId = request.CustomerId,
            LeadId = request.LeadId,
            FollowUpDate = request.FollowUpDate,
            Subject = request.Subject.Trim(),
            FollowUpType = request.FollowUpType.Trim(),
            Status = "Planned",
            AssignedUserId = assignedUserId,
            Notes = request.Notes?.Trim()
        };

        _context.FollowUps.Add(followUp);

        await _context.SaveChangesAsync();

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

        await _context.Entry(followUp)
            .Reference(f => f.Customer)
            .LoadAsync();

        await _context.Entry(followUp)
            .Reference(f => f.Lead)
            .LoadAsync();

        await _context.Entry(followUp)
            .Reference(f => f.AssignedUser)
            .LoadAsync();

        return CreatedAtAction(
            nameof(GetFollowUpById),
            new { id = followUp.FollowUpId },
            ToResponse(followUp));
    }

    // =====================================================
    // PUT: api/followups/{id}
    // Update follow-up.
    // =====================================================

    [HttpPut("{id:int}")]
    public async Task<ActionResult<FollowUpApiResponse>>
        UpdateFollowUp(
            int id,
            [FromBody] FollowUpApiRequest request)
    {
        var followUp = await ScopedFollowUps()
            .FirstOrDefaultAsync(
                f => f.FollowUpId == id);

        if (followUp == null)
        {
            return NotFound(new
            {
                message = "Follow-up not found."
            });
        }

        var validationError = ValidateRequest(
            request,
            isCreate: false);

        if (validationError != null)
        {
            return BadRequest(new
            {
                message = validationError
            });
        }

        var referenceError = await ValidateReferencesAsync(
            request.CustomerId,
            request.LeadId);

        if (referenceError != null)
        {
            return BadRequest(new
            {
                message = referenceError
            });
        }

        string? assignedUserId = followUp.AssignedUserId;

        if (IsAdminOrManager)
        {
            assignedUserId = request.AssignedUserId?.Trim();

            if (!await IsActiveSalesExecutiveAsync(assignedUserId))
            {
                return BadRequest(new
                {
                    message = "Select an active Sales Executive."
                });
            }
        }

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

        followUp.CustomerId = request.CustomerId;
        followUp.LeadId = request.LeadId;
        followUp.FollowUpDate = request.FollowUpDate;
        followUp.Subject = request.Subject.Trim();
        followUp.FollowUpType = request.FollowUpType.Trim();
        followUp.Status = request.Status.Trim();
        followUp.Notes = request.Notes?.Trim();
        followUp.AssignedUserId = assignedUserId;

        await _context.SaveChangesAsync();

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

        await _context.Entry(followUp)
            .Reference(f => f.Customer)
            .LoadAsync();

        await _context.Entry(followUp)
            .Reference(f => f.Lead)
            .LoadAsync();

        await _context.Entry(followUp)
            .Reference(f => f.AssignedUser)
            .LoadAsync();

        return Ok(ToResponse(followUp));
    }

    // =====================================================
    // DELETE: api/followups/{id}
    // Only Admin and Manager can delete.
    // =====================================================

    [HttpDelete("{id:int}")]
    [Authorize(Roles = RoleConstants.AdminOrManager)]
    public async Task<IActionResult> DeleteFollowUp(int id)
    {
        var followUp = await _context.FollowUps
            .FirstOrDefaultAsync(
                f => f.FollowUpId == id);

        if (followUp == null)
        {
            return NotFound(new
            {
                message = "Follow-up not found."
            });
        }

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

        try
        {
            // Saves the pending deletion and the audit entry.
            await _auditLogService.LogAsync(
                action: "Delete",
                entityName: "FollowUp",
                recordId: followUp.FollowUpId,
                oldValue: oldValues);
        }
        catch (DbUpdateException)
        {
            return Conflict(new
            {
                message = "The follow-up could not be deleted."
            });
        }

        return Ok(new
        {
            message = "Follow-up deleted successfully."
        });
    }

    // =====================================================
    // POST: api/followups/{id}/complete
    // Mark a planned follow-up as completed.
    // =====================================================

    [HttpPost("{id:int}/complete")]
    public async Task<IActionResult> MarkCompleted(int id)
    {
        var followUp = await ScopedFollowUps()
            .FirstOrDefaultAsync(
                f => f.FollowUpId == id);

        if (followUp == null)
        {
            return NotFound(new
            {
                message = "Follow-up not found."
            });
        }

        if (followUp.Status != "Planned")
        {
            return Conflict(new
            {
                message = "Only planned follow-ups can be completed."
            });
        }

        var oldStatus = followUp.Status;

        followUp.Status = "Completed";

        await _context.SaveChangesAsync();

        await _auditLogService.LogAsync(
            action: "Complete",
            entityName: "FollowUp",
            recordId: followUp.FollowUpId,
            oldValue: new { Status = oldStatus },
            newValue: new { Status = followUp.Status });

        return Ok(new
        {
            message = "Follow-up marked as completed.",
            followUpId = followUp.FollowUpId,
            status = followUp.Status
        });
    }

    // =====================================================
    // POST: api/followups/{id}/miss
    // Mark a planned follow-up as missed.
    // =====================================================

    [HttpPost("{id:int}/miss")]
    public async Task<IActionResult> MarkMissed(int id)
    {
        var followUp = await ScopedFollowUps()
            .FirstOrDefaultAsync(
                f => f.FollowUpId == id);

        if (followUp == null)
        {
            return NotFound(new
            {
                message = "Follow-up not found."
            });
        }

        if (followUp.Status != "Planned")
        {
            return Conflict(new
            {
                message = "Only planned follow-ups can be marked as missed."
            });
        }

        var oldStatus = followUp.Status;

        followUp.Status = "Missed";

        await _context.SaveChangesAsync();

        await _auditLogService.LogAsync(
            action: "Missed",
            entityName: "FollowUp",
            recordId: followUp.FollowUpId,
            oldValue: new { Status = oldStatus },
            newValue: new { Status = followUp.Status });

        return Ok(new
        {
            message = "Follow-up marked as missed.",
            followUpId = followUp.FollowUpId,
            status = followUp.Status
        });
    }
}
