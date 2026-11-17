
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize(Roles = "Admin")]
public class AuditLogController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public AuditLogController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // ----------------------------------------------------
    // INDEX: SEARCH AND FILTER AUDIT LOGS
    // ----------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> Index(
        string? searchTerm,
        string? actionFilter,
        string? entityFilter)
    {
        var query = _context.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();

            // Find users matching their name, email or ID.
            var matchingUserIds = await _userManager.Users
                .AsNoTracking()
                .Where(u =>
                    (u.FullName != null &&
                     u.FullName.Contains(term)) ||
                    (u.Email != null &&
                     u.Email.Contains(term)) ||
                    u.Id.Contains(term))
                .Select(u => u.Id)
                .ToListAsync();

            query = query.Where(x =>
                x.Action.Contains(term) ||
                x.EntityName.Contains(term) ||
                (x.UserId != null &&
                 (x.UserId.Contains(term) ||
                  matchingUserIds.Contains(x.UserId))) ||
                (x.IpAddress != null &&
                 x.IpAddress.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(actionFilter))
        {
            query = query.Where(
                x => x.Action == actionFilter);
        }

        if (!string.IsNullOrWhiteSpace(entityFilter))
        {
            query = query.Where(
                x => x.EntityName == entityFilter);
        }

        var records = await query
            .OrderByDescending(x => x.CreatedDate)
            .ThenByDescending(x => x.AuditLogId)
            .ToListAsync();

        // Resolve user IDs into readable names.
        var users = await _userManager.Users
            .AsNoTracking()
            .ToDictionaryAsync(
                u => u.Id,
                u => string.IsNullOrWhiteSpace(u.FullName)
                    ? u.Email ?? u.Id
                    : u.FullName);

        var model = new AuditLogListViewModel
        {
            SearchTerm = searchTerm,
            ActionFilter = actionFilter,
            EntityFilter = entityFilter,
            TotalLogs = records.Count,

            Logs = records.Select(x =>
                new AuditLogViewModel
                {
                    AuditLogId = x.AuditLogId,

                    UserId = x.UserId != null &&
                             users.TryGetValue(
                                 x.UserId, out var name)
                        ? name
                        : x.UserId ?? "System",

                    Action = x.Action,
                    EntityName = x.EntityName,
                    RecordId = x.RecordId,
                    OldValue = x.OldValue,
                    NewValue = x.NewValue,
                    CreatedDate = x.CreatedDate,
                    IpAddress = x.IpAddress,
                    Result = x.Result
                }).ToList()
        };

        // Available action filters.
        ViewBag.Actions = await _context.AuditLogs
            .AsNoTracking()
            .Select(x => x.Action)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();

        // Available entity filters.
        ViewBag.Entities = await _context.AuditLogs
            .AsNoTracking()
            .Select(x => x.EntityName)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();

        return View(model);
    }

    // ----------------------------------------------------
    // DETAILS: VIEW INDIVIDUAL AUDIT RECORD
    // ----------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var log = await _context.AuditLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.AuditLogId == id);

        if (log == null)
        {
            return NotFound();
        }

        var userName = "System";

        if (!string.IsNullOrWhiteSpace(log.UserId))
        {
            var user = await _userManager.FindByIdAsync(
                log.UserId);

            if (user != null)
            {
                userName = string.IsNullOrWhiteSpace(user.FullName)
                    ? user.Email ?? user.Id
                    : user.FullName;
            }
            else
            {
                userName = log.UserId;
            }
        }

        var model = new AuditLogViewModel
        {
            AuditLogId = log.AuditLogId,
            UserId = userName,
            Action = log.Action,
            EntityName = log.EntityName,
            RecordId = log.RecordId,
            OldValue = log.OldValue,
            NewValue = log.NewValue,
            CreatedDate = log.CreatedDate,
            IpAddress = log.IpAddress,
            Result = log.Result
        };

        return View(model);
    }
}
