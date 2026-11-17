
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
using System.Security.Claims;

namespace AcxiomCRM.Controllers.Api;

[ApiController]
[Route("api/opportunities")]
[Authorize(
    AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,Roles = RoleConstants.AllRoles)]
public class OpportunitiesApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AuditLogService _auditLogService;

    private static readonly string[] ValidStages =
    {
        "Qualification",
        "Proposal",
        "Negotiation",
        "Won",
        "Lost"
    };

    private static readonly string[] ValidSources =
    {
        "Website",
        "Referral",
        "Advertisement",
        "Cold Call",
        "Existing Customer",
        "Other"
    };

    public OpportunitiesApiController(
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
        User.IsInRole(RoleConstants.Admin) ||
        User.IsInRole(RoleConstants.Manager);

    // =====================================================
    // HELPER METHODS
    // =====================================================

    private IQueryable<Opportunity> ScopedOpportunities()
    {
        var query = _context.Opportunities
            .AsNoTracking()
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

    private static OpportunityApiResponse ToResponse(
        Opportunity opportunity)
    {
        return new OpportunityApiResponse
        {
            OpportunityId = opportunity.OpportunityId,
            OpportunityName = opportunity.OpportunityName,

            CustomerId = opportunity.CustomerId,
            CustomerName =
                opportunity.Customer?.CustomerName ?? "Not linked",

            LeadId = opportunity.LeadId,
            LeadName =
                opportunity.Lead?.LeadName ?? "Not linked",

            Amount = opportunity.Amount,
            Probability = opportunity.Probability,
            WeightedValue = opportunity.WeightedValue,

            Stage = opportunity.Stage,
            Status = opportunity.Status,

            ExpectedCloseDate = opportunity.ExpectedCloseDate,

            Source = opportunity.Source,
            Notes = opportunity.Notes,

            AssignedTo = opportunity.AssignedTo,
            AssignedToName =
                opportunity.AssignedUser?.FullName ?? "Unassigned",

            CreatedBy = opportunity.CreatedBy,
            CreatedDate = opportunity.CreatedDate,
            ModifiedDate = opportunity.ModifiedDate
        };
    }

    private static string GetStatusFromStage(string stage)
    {
        return stage switch
        {
            "Won" => "Won",
            "Lost" => "Lost",
            _ => "Open"
        };
    }

    private static bool IsValidStage(string? stage)
    {
        return ValidStages.Contains(
            stage?.Trim() ?? string.Empty,
            StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsValidSource(string? source)
    {
        return ValidSources.Contains(
            source?.Trim() ?? string.Empty,
            StringComparer.OrdinalIgnoreCase);
    }

    private async Task<bool> IsValidSalesExecutiveAsync(
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
    // VALIDATE CUSTOMER AND LEAD REFERENCES
    // =====================================================

    private async Task<bool> ValidateReferencesAsync(
        OpportunityApiRequest request)
    {
        if (!request.CustomerId.HasValue &&
            !request.LeadId.HasValue)
        {
            return false;
        }

        if (request.CustomerId.HasValue)
        {
            var customerQuery = _context.Customers.AsQueryable();

            if (!IsAdminOrManager)
            {
                customerQuery = customerQuery.Where(
                    c => c.OwnerId == CurrentUserId);
            }

            bool customerExists = await customerQuery.AnyAsync(
                c => c.CustomerId == request.CustomerId.Value);

            if (!customerExists)
                return false;
        }

        if (request.LeadId.HasValue)
        {
            var leadQuery = _context.Leads.AsQueryable();

            if (!IsAdminOrManager)
            {
                leadQuery = leadQuery.Where(
                    l => l.AssignedTo == CurrentUserId);
            }

            bool leadExists = await leadQuery.AnyAsync(
                l => l.LeadId == request.LeadId.Value);

            if (!leadExists)
                return false;
        }

        return true;
    }

    // =====================================================
    // VALIDATE REQUEST DATA
    // =====================================================

    private string? ValidateOpportunity(
        OpportunityApiRequest request)
    {
        if (!IsValidStage(request.Stage))
            return "Invalid opportunity stage.";

        if (!IsValidSource(request.Source))
            return "Invalid opportunity source.";

        if (request.Amount <= 0)
            return "Amount must be greater than zero.";

        if (request.Probability < 0 ||
            request.Probability > 100)
        {
            return "Probability must be between 0 and 100.";
        }

        bool isActive =
            request.Stage != "Won" &&
            request.Stage != "Lost";

        if (isActive &&
            request.ExpectedCloseDate.Date < DateTime.Today)
        {
            return "Expected close date cannot be in the past for an active opportunity.";
        }

        return null;
    }

    // =====================================================
    // GET: api/opportunities
    // List accessible opportunities.
    // =====================================================

    [HttpGet]
    public async Task<ActionResult<IEnumerable<OpportunityApiResponse>>>
        GetOpportunities(
            [FromQuery] string? search,
            [FromQuery] string? stage)
    {
        var query = ScopedOpportunities();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();

            query = query.Where(o =>
                o.OpportunityName.Contains(term) ||
                (o.Customer != null &&
                 o.Customer.CustomerName.Contains(term)) ||
                (o.Lead != null &&
                 o.Lead.LeadName.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(stage))
        {
            var selectedStage = stage.Trim();

            query = query.Where(
                o => o.Stage == selectedStage);
        }

        var opportunities = await query
            .OrderByDescending(o => o.CreatedDate)
            .ToListAsync();

        return Ok(
            opportunities.Select(ToResponse).ToList());
    }

    // =====================================================
    // GET: api/opportunities/{id}
    // Get one opportunity.
    // =====================================================

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OpportunityApiResponse>>
        GetOpportunityById(int id)
    {
        if (!await CanAccessAsync(id))
        {
            return NotFound(new
            {
                message = "Opportunity not found."
            });
        }

        var opportunity = await ScopedOpportunities()
            .FirstOrDefaultAsync(
                o => o.OpportunityId == id);

        if (opportunity == null)
        {
            return NotFound(new
            {
                message = "Opportunity not found."
            });
        }

        return Ok(ToResponse(opportunity));
    }

    // =====================================================
    // POST: api/opportunities
    // Create opportunity.
    // =====================================================

    [HttpPost]
    public async Task<ActionResult<OpportunityApiResponse>>
        CreateOpportunity(
            [FromBody] OpportunityApiRequest request)
    {
        var validationError = ValidateOpportunity(request);

        if (validationError != null)
        {
            return BadRequest(new
            {
                message = validationError
            });
        }

        if (!await ValidateReferencesAsync(request))
        {
            return BadRequest(new
            {
                message = "Select at least one valid Customer or Lead accessible to your account."
            });
        }

        string? assignedTo;

        if (IsAdminOrManager)
        {
            assignedTo = request.AssignedTo?.Trim();

            if (!await IsValidSalesExecutiveAsync(assignedTo))
            {
                return BadRequest(new
                {
                    message = "Select an active Sales Executive."
                });
            }
        }
        else
        {
            assignedTo = CurrentUserId;
        }

        var opportunity = new Opportunity
        {
            OpportunityName = request.OpportunityName.Trim(),

            CustomerId = request.CustomerId,
            LeadId = request.LeadId,

            Amount = request.Amount,
            Probability = request.Probability,

            Stage = request.Stage.Trim(),
            Status = GetStatusFromStage(request.Stage.Trim()),

            ExpectedCloseDate = request.ExpectedCloseDate.Date,

            Source = request.Source.Trim(),
            Notes = request.Notes?.Trim(),

            AssignedTo = assignedTo,

            CreatedBy = CurrentUserId,
            CreatedDate = DateTime.UtcNow
        };

        _context.Opportunities.Add(opportunity);

        await _context.SaveChangesAsync();

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
            });

        await _context.Entry(opportunity)
            .Reference(o => o.Customer)
            .LoadAsync();

        await _context.Entry(opportunity)
            .Reference(o => o.Lead)
            .LoadAsync();

        await _context.Entry(opportunity)
            .Reference(o => o.AssignedUser)
            .LoadAsync();

        var response = ToResponse(opportunity);

        return CreatedAtAction(
            nameof(GetOpportunityById),
            new { id = opportunity.OpportunityId },
            response);
    }

    // =====================================================
    // PUT: api/opportunities/{id}
    // Update opportunity.
    // =====================================================

    [HttpPut("{id:int}")]
    public async Task<ActionResult<OpportunityApiResponse>>
        UpdateOpportunity(
            int id,
            [FromBody] OpportunityApiRequest request)
    {
        var opportunity = await _context.Opportunities
            .FirstOrDefaultAsync(
                o => o.OpportunityId == id);

        if (opportunity == null ||
            !await CanAccessAsync(id))
        {
            return NotFound(new
            {
                message = "Opportunity not found."
            });
        }

        var validationError = ValidateOpportunity(request);

        if (validationError != null)
        {
            return BadRequest(new
            {
                message = validationError
            });
        }

        if (!await ValidateReferencesAsync(request))
        {
            return BadRequest(new
            {
                message = "Select at least one valid Customer or Lead accessible to your account."
            });
        }

        string? assignedTo = opportunity.AssignedTo;

        if (IsAdminOrManager)
        {
            assignedTo = request.AssignedTo?.Trim();

            if (!await IsValidSalesExecutiveAsync(assignedTo))
            {
                return BadRequest(new
                {
                    message = "Select an active Sales Executive."
                });
            }
        }

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
            request.OpportunityName.Trim();

        opportunity.CustomerId = request.CustomerId;
        opportunity.LeadId = request.LeadId;

        opportunity.Amount = request.Amount;
        opportunity.Probability = request.Probability;

        opportunity.Stage = request.Stage.Trim();
        opportunity.Status =
            GetStatusFromStage(request.Stage.Trim());

        opportunity.ExpectedCloseDate =
            request.ExpectedCloseDate.Date;

        opportunity.Source = request.Source.Trim();
        opportunity.Notes = request.Notes?.Trim();

        opportunity.AssignedTo = assignedTo;
        opportunity.ModifiedDate = DateTime.UtcNow;

        await _context.SaveChangesAsync();

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
            });

        await _context.Entry(opportunity)
            .Reference(o => o.Customer)
            .LoadAsync();

        await _context.Entry(opportunity)
            .Reference(o => o.Lead)
            .LoadAsync();

        await _context.Entry(opportunity)
            .Reference(o => o.AssignedUser)
            .LoadAsync();

        return Ok(ToResponse(opportunity));
    }

    // =====================================================
    // DELETE: api/opportunities/{id}
    // Only Admin and Manager can delete.
    // =====================================================

    [HttpDelete("{id:int}")]
    [Authorize(Roles = RoleConstants.AdminOrManager)]
    public async Task<IActionResult> DeleteOpportunity(int id)
    {
        var opportunity = await _context.Opportunities
            .FirstOrDefaultAsync(
                o => o.OpportunityId == id);

        if (opportunity == null)
        {
            return NotFound(new
            {
                message = "Opportunity not found."
            });
        }

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

        await _auditLogService.LogAsync(
            action: "Delete",
            entityName: "Opportunity",
            recordId: id,
            oldValue: oldValues);

        return Ok(new
        {
            message = "Opportunity deleted successfully."
        });
    }
}
