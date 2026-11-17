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
[Route("api/activities")]
[Authorize(
    AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
    Roles = RoleConstants.AllRoles)]
public class ActivitiesApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AuditLogService _auditLogService;

    private string CurrentUserId =>
        _userManager.GetUserId(User) ?? string.Empty;

    private bool IsAdminOrManager =>
        User.IsInRole(RoleConstants.Admin) ||
        User.IsInRole(RoleConstants.Manager);

    private static readonly string[] ActivityTypes =
    {
        "Call", "Meeting", "Email", "Task"
    };

    private static readonly string[] ActivityStatuses =
    {
        "Planned", "Completed", "Cancelled"
    };

    public ActivitiesApiController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        AuditLogService auditLogService)
    {
        _context = context;
        _userManager = userManager;
        _auditLogService = auditLogService;
    }

    // --------------------------------------------------
    // GET: api/activities
    // GET: api/activities?searchTerm=meeting
    // GET: api/activities?status=Planned
    // GET: api/activities?activityType=Call
    // --------------------------------------------------

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ActivityApiResponse>>> GetActivities(
        [FromQuery] string? searchTerm,
        [FromQuery] string? status,
        [FromQuery] string? activityType)
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

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!ActivityStatuses.Contains(
                status, StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    message = "Invalid activity status."
                });
            }

            query = query.Where(a => a.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(activityType))
        {
            if (!ActivityTypes.Contains(
                activityType, StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    message = "Invalid activity type."
                });
            }

            query = query.Where(a => a.ActivityType == activityType);
        }

        var activities = await query
            .AsNoTracking()
            .OrderByDescending(a => a.ActivityDate)
            .ToListAsync();

        return Ok(activities.Select(ToResponse));
    }

    // --------------------------------------------------
    // GET: api/activities/5
    // --------------------------------------------------

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ActivityApiResponse>> GetActivity(int id)
    {
        var activity = await ScopedActivities()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.ActivityId == id);

        if (activity == null)
        {
            return NotFound(new
            {
                message = "Activity not found."
            });
        }

        return Ok(ToResponse(activity));
    }

    // --------------------------------------------------
    // POST: api/activities
    // --------------------------------------------------

    [HttpPost]
    public async Task<ActionResult<ActivityApiResponse>> CreateActivity(
        [FromBody] ActivityApiRequest request)
    {
        var validationError = ValidateRequest(request, isCreate: true);

        if (validationError != null)
            return BadRequest(new { message = validationError });

        var referenceError = await ValidateReferencesAsync(
            request.CustomerId,
            request.LeadId);

        if (referenceError != null)
            return BadRequest(new { message = referenceError });

        string assignedTo;

        if (IsAdminOrManager)
        {
            if (string.IsNullOrWhiteSpace(request.AssignedTo) ||
                !await IsActiveSalesExecutiveAsync(request.AssignedTo))
            {
                return BadRequest(new
                {
                    message = "Select an active Sales Executive."
                });
            }

            assignedTo = request.AssignedTo;
        }
        else
        {
            assignedTo = CurrentUserId;
        }

        var activity = new Activity
        {
            ActivityType = request.ActivityType.Trim(),
            Subject = request.Subject.Trim(),
            Description = request.Description,
            ActivityDate = request.ActivityDate,
            CustomerId = request.CustomerId,
            LeadId = request.LeadId,
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
            });

        var createdActivity = await ScopedActivities()
            .AsNoTracking()
            .FirstAsync(a => a.ActivityId == activity.ActivityId);

        return CreatedAtAction(
            nameof(GetActivity),
            new { id = activity.ActivityId },
            ToResponse(createdActivity));
    }

    // --------------------------------------------------
    // PUT: api/activities/5
    // --------------------------------------------------

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ActivityApiResponse>> UpdateActivity(
        int id,
        [FromBody] ActivityApiRequest request)
    {
        var activity = await ScopedActivities()
            .FirstOrDefaultAsync(a => a.ActivityId == id);

        if (activity == null)
        {
            return NotFound(new
            {
                message = "Activity not found."
            });
        }

        var validationError = ValidateRequest(request, isCreate: false);

        if (validationError != null)
            return BadRequest(new { message = validationError });

        var referenceError = await ValidateReferencesAsync(
            request.CustomerId,
            request.LeadId);

        if (referenceError != null)
            return BadRequest(new { message = referenceError });

        if (IsAdminOrManager)
        {
            if (string.IsNullOrWhiteSpace(request.AssignedTo) ||
                !await IsActiveSalesExecutiveAsync(request.AssignedTo))
            {
                return BadRequest(new
                {
                    message = "Select an active Sales Executive."
                });
            }
        }

        var oldValues = new
        {
            activity.ActivityType,
            activity.Subject,
            activity.Description,
            activity.ActivityDate,
            activity.CustomerId,
            activity.LeadId,
            activity.AssignedTo,
            activity.Status
        };

        activity.ActivityType = request.ActivityType.Trim();
        activity.Subject = request.Subject.Trim();
        activity.Description = request.Description;
        activity.ActivityDate = request.ActivityDate;
        activity.CustomerId = request.CustomerId;
        activity.LeadId = request.LeadId;
        activity.Status = request.Status;

        // Sales Executives cannot reassign activities.
        if (IsAdminOrManager)
        {
            activity.AssignedTo = request.AssignedTo;
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
                activity.Description,
                activity.ActivityDate,
                activity.CustomerId,
                activity.LeadId,
                activity.AssignedTo,
                activity.Status
            });

        var updatedActivity = await ScopedActivities()
            .AsNoTracking()
            .FirstAsync(a => a.ActivityId == id);

        return Ok(ToResponse(updatedActivity));
    }

    // --------------------------------------------------
    // DELETE: api/activities/5
    // Admin and Manager only
    // --------------------------------------------------

    [HttpDelete("{id:int}")]
    [Authorize(Roles = RoleConstants.AdminOrManager)]
    public async Task<IActionResult> DeleteActivity(int id)
    {
        var activity = await _context.Activities
            .FirstOrDefaultAsync(a => a.ActivityId == id);

        if (activity == null)
        {
            return NotFound(new
            {
                message = "Activity not found."
            });
        }

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
            newValue: "Activity deleted.");

        return Ok(new
        {
            message = "Activity deleted successfully."
        });
    }

    // --------------------------------------------------
    // POST: api/activities/5/complete
    // --------------------------------------------------

    [HttpPost("{id:int}/complete")]
    public async Task<IActionResult> MarkCompleted(int id)
    {
        var activity = await ScopedActivities()
            .FirstOrDefaultAsync(a => a.ActivityId == id);

        if (activity == null)
        {
            return NotFound(new
            {
                message = "Activity not found."
            });
        }

        if (activity.Status != "Planned")
        {
            return BadRequest(new
            {
                message = "Only planned activities can be completed."
            });
        }

        var oldStatus = activity.Status;

        activity.Status = "Completed";
        await _context.SaveChangesAsync();

        await _auditLogService.LogAsync(
            action: "Complete",
            entityName: "Activity",
            recordId: activity.ActivityId,
            oldValue: new { Status = oldStatus },
            newValue: new { Status = activity.Status });

        return Ok(new
        {
            message = "Activity marked as completed.",
            activityId = activity.ActivityId,
            status = activity.Status
        });
    }

    // --------------------------------------------------
    // POST: api/activities/5/cancel
    // --------------------------------------------------

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> MarkCancelled(int id)
    {
        var activity = await ScopedActivities()
            .FirstOrDefaultAsync(a => a.ActivityId == id);

        if (activity == null)
        {
            return NotFound(new
            {
                message = "Activity not found."
            });
        }

        if (activity.Status != "Planned")
        {
            return BadRequest(new
            {
                message = "Only planned activities can be cancelled."
            });
        }

        var oldStatus = activity.Status;

        activity.Status = "Cancelled";
        await _context.SaveChangesAsync();

        await _auditLogService.LogAsync(
            action: "Cancel",
            entityName: "Activity",
            recordId: activity.ActivityId,
            oldValue: new { Status = oldStatus },
            newValue: new { Status = activity.Status });

        return Ok(new
        {
            message = "Activity cancelled successfully.",
            activityId = activity.ActivityId,
            status = activity.Status
        });
    }

    // ==================================================
    // HELPER METHODS
    // ==================================================

    private IQueryable<Activity> ScopedActivities()
    {
        var query = _context.Activities
            .Include(a => a.Customer)
            .Include(a => a.Lead)
            .Include(a => a.AssignedUser)
            .AsQueryable();

        if (IsAdminOrManager)
            return query;

        return query.Where(a => a.AssignedTo == CurrentUserId);
    }

    private static ActivityApiResponse ToResponse(Activity activity)
    {
        return new ActivityApiResponse
        {
            ActivityId = activity.ActivityId,
            ActivityType = activity.ActivityType,
            Subject = activity.Subject,
            Description = activity.Description,
            ActivityDate = activity.ActivityDate,
            CustomerId = activity.CustomerId,
            CustomerName = activity.Customer?.CustomerName ?? "Not linked",
            LeadId = activity.LeadId,
            LeadName = activity.Lead?.LeadName ?? "Not linked",
            AssignedTo = activity.AssignedTo,
            AssignedUserName = activity.AssignedUser?.FullName ?? "Unassigned",
            Status = activity.Status
        };
    }

    private static string? ValidateRequest(
        ActivityApiRequest request,
        bool isCreate)
    {
        if (string.IsNullOrWhiteSpace(request.Subject))
            return "Activity subject is required.";

        if (request.Subject.Trim().Length > 150)
            return "Activity subject cannot exceed 150 characters.";

        if (request.Description?.Length > 1000)
            return "Description cannot exceed 1000 characters.";

        if (!ActivityTypes.Contains(
            request.ActivityType,
            StringComparer.OrdinalIgnoreCase))
        {
            return "Invalid activity type. Allowed types: Call, Meeting, Email, Task.";
        }

        if (!isCreate &&
            !ActivityStatuses.Contains(
                request.Status,
                StringComparer.OrdinalIgnoreCase))
        {
            return "Invalid status. Allowed statuses: Planned, Completed, Cancelled.";
        }

        return null;
    }

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
            var customer = await _context.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    c => c.CustomerId == customerId.Value);

            if (customer == null)
                return "Selected customer does not exist.";

            if (!IsAdminOrManager &&
                customer.OwnerId != CurrentUserId)
            {
                return "You cannot select another user's customer.";
            }
        }

        if (leadId.HasValue)
        {
            var lead = await _context.Leads
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    l => l.LeadId == leadId.Value);

            if (lead == null)
                return "Selected lead does not exist.";

            if (!IsAdminOrManager &&
                lead.AssignedTo != CurrentUserId)
            {
                return "You cannot select another user's lead.";
            }
        }

        return null;
    }

    private async Task<bool> IsActiveSalesExecutiveAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);

        if (user == null || !user.IsActive)
            return false;

        return await _userManager.IsInRoleAsync(
            user,
            RoleConstants.SalesExecutive);
    }
}