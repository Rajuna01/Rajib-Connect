
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace AcxiomCRM.Services;

public class AuditLogService
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditLogService(
        ApplicationDbContext context,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task LogAsync(
        string action,
        string entityName,
        int? recordId = null,
        object? oldValue = null,
        object? newValue = null,
        string result = "Success")
    {
        var httpContext = _httpContextAccessor.HttpContext;

        var log = new AuditLog
        {
            UserId = httpContext?.User?
                .FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,

            Action = action,
            EntityName = entityName,
            RecordId = recordId,

            OldValue = oldValue == null
                ? null
                : JsonSerializer.Serialize(oldValue),

            NewValue = newValue == null
                ? null
                : JsonSerializer.Serialize(newValue),

            CreatedDate = DateTime.UtcNow,

            IpAddress = httpContext?.Connection.RemoteIpAddress?.ToString(),

            Result = result
        };

        _context.AuditLogs.Add(log);
        await _context.SaveChangesAsync();
    }
}
