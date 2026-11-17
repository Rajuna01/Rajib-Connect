using Microsoft.AspNetCore.Authentication.JwtBearer;
using AcxiomCRM.Authorization;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers.Api;

[ApiController]
[Route("api/auditlogs")]
[Authorize(
    AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
    Roles = RoleConstants.Admin)]
public class AuditLogsApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public AuditLogsApiController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // --------------------------------------------------
    // GET: api/auditlogs
    // Optional filters:
    // ?action=Create
    // ?entityName=Customer
    // ?result=Success
    // ?searchTerm=Activity
    // ?startDate=2026-11-01
    // ?endDate=2026-11-30
    // ?page=1&pageSize=20
    // --------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] string? action,
        [FromQuery] string? entityName,
        [FromQuery] string? result,
        [FromQuery] string? searchTerm,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1)
        {
            return BadRequest(new
            {
                message = "Page must be at least 1."
            });
        }

        if (pageSize < 1 || pageSize > 100)
        {
            return BadRequest(new
            {
                message = "Page size must be between 1 and 100."
            });
        }

        if (startDate.HasValue &&
            endDate.HasValue &&
            endDate.Value.Date < startDate.Value.Date)
        {
            return BadRequest(new
            {
                message = "End date cannot be earlier than start date."
            });
        }

        var query = _context.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(action))
        {
            action = action.Trim();
            query = query.Where(log => log.Action == action);
        }

        if (!string.IsNullOrWhiteSpace(entityName))
        {
            entityName = entityName.Trim();
            query = query.Where(log => log.EntityName == entityName);
        }

        if (!string.IsNullOrWhiteSpace(result))
        {
            result = result.Trim();
            query = query.Where(log => log.Result == result);
        }

        if (startDate.HasValue)
        {
            var start = startDate.Value.Date;
            query = query.Where(log => log.CreatedDate >= start);
        }

        if (endDate.HasValue)
        {
            var endExclusive = endDate.Value.Date.AddDays(1);
            query = query.Where(log => log.CreatedDate < endExclusive);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            searchTerm = searchTerm.Trim();

            query = query.Where(log =>
                log.Action.Contains(searchTerm) ||
                log.EntityName.Contains(searchTerm) ||
                (log.OldValue != null &&
                 log.OldValue.Contains(searchTerm)) ||
                (log.NewValue != null &&
                 log.NewValue.Contains(searchTerm)) ||
                (log.IpAddress != null &&
                 log.IpAddress.Contains(searchTerm)));
        }

        var totalCount = await query.CountAsync();

        var logs = await query
            .OrderByDescending(log => log.CreatedDate)
            .ThenByDescending(log => log.AuditLogId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var userIds = logs
            .Where(log => !string.IsNullOrEmpty(log.UserId))
            .Select(log => log.UserId!)
            .Distinct()
            .ToList();

        var users = await _userManager.Users
            .Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(
                user => user.Id,
                user => user.FullName);

        var response = logs.Select(log =>
            ToResponse(log, users));

        return Ok(new
        {
            totalCount,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling(
                totalCount / (double)pageSize),
            records = response
        });
    }

    // --------------------------------------------------
    // GET: api/auditlogs/5
    // --------------------------------------------------

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AuditLogApiResponse>> GetAuditLog(
        int id)
    {
        var log = await _context.AuditLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(log =>
                log.AuditLogId == id);

        if (log == null)
        {
            return NotFound(new
            {
                message = "Audit log not found."
            });
        }

        var users = new Dictionary<string, string>();

        if (!string.IsNullOrEmpty(log.UserId))
        {
            var user = await _userManager.FindByIdAsync(log.UserId);

            if (user != null)
            {
                users[user.Id] = user.FullName;
            }
        }

        return Ok(ToResponse(log, users));
    }

    // --------------------------------------------------
    // CONVERT MODEL TO API RESPONSE
    // --------------------------------------------------

    private static AuditLogApiResponse ToResponse(
        AuditLog log,
        Dictionary<string, string> users)
    {
        string userName = "Unknown";

        if (!string.IsNullOrEmpty(log.UserId) &&
            users.TryGetValue(log.UserId, out var name) &&
            !string.IsNullOrWhiteSpace(name))
        {
            userName = name;
        }

        return new AuditLogApiResponse
        {
            AuditLogId = log.AuditLogId,
            UserId = log.UserId,
            UserName = userName,
            Action = log.Action,
            EntityName = log.EntityName,
            RecordId = log.RecordId,
            OldValue = log.OldValue,
            NewValue = log.NewValue,
            CreatedDate = log.CreatedDate,
            IpAddress = log.IpAddress,
            Result = log.Result
        };
    }
}