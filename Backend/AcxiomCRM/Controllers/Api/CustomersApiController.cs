
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
[Route("api/customers")]
[Authorize(
    AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
    Roles = RoleConstants.AllRoles)]
public class CustomersApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AuditLogService _auditLogService;

    public CustomersApiController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        AuditLogService auditLogService)
    {
        _context = context;
        _userManager = userManager;
        _auditLogService = auditLogService;
    }

    private bool IsAdminOrManager()
    {
        return User.IsInRole(RoleConstants.Admin) ||
               User.IsInRole(RoleConstants.Manager);
    }

    private async Task<bool> CanAccessCustomerAsync(Customer customer)
    {
        if (IsAdminOrManager())
            return true;

        var currentUserId = _userManager.GetUserId(User);

        return customer.OwnerId == currentUserId;
    }

    private static CustomerApiResponse ToResponse(Customer customer)
    {
        return new CustomerApiResponse
        {
            CustomerId = customer.CustomerId,
            CustomerCode = customer.CustomerCode,
            CustomerName = customer.CustomerName,
            Email = customer.Email,
            Phone = customer.Phone,
            CompanyName = customer.CompanyName,
            Address = customer.Address,
            City = customer.City,
            State = customer.State,
            Status = customer.Status,
            OwnerId = customer.OwnerId,
            OwnerName = customer.Owner?.FullName ?? "Unassigned",
            CreatedDate = customer.CreatedDate,
            ModifiedDate = customer.ModifiedDate
        };
    }

    private async Task<bool> IsValidSalesExecutiveAsync(string? ownerId)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
            return true;

        var executive = await _userManager.FindByIdAsync(ownerId);

        if (executive == null || !executive.IsActive)
            return false;

        return await _userManager.IsInRoleAsync(
            executive,
            RoleConstants.SalesExecutive);
    }

    private static bool IsValidStatus(string? status)
    {
        return string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, "Inactive", StringComparison.OrdinalIgnoreCase);
    }

    // =====================================================
    // GET: api/customers
    // List customers accessible to the logged-in user.
    // =====================================================

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CustomerApiResponse>>> GetCustomers(
        [FromQuery] string? search,
        [FromQuery] string? status)
    {
        var query = _context.Customers
            .AsNoTracking()
            .Include(c => c.Owner)
            .AsQueryable();

        if (!IsAdminOrManager())
        {
            var userId = _userManager.GetUserId(User);

            query = query.Where(c => c.OwnerId == userId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();

            query = query.Where(c =>
                c.CustomerName.Contains(term) ||
                c.CustomerCode.Contains(term) ||
                c.Email.Contains(term) ||
                c.CompanyName.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var selectedStatus = status.Trim();

            query = query.Where(c => c.Status == selectedStatus);
        }

        var customers = await query
            .OrderByDescending(c => c.CreatedDate)
            .ToListAsync();

        var response = customers.Select(ToResponse).ToList();

        return Ok(response);
    }

    // =====================================================
    // GET: api/customers/{id}
    // Get one customer.
    // =====================================================

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CustomerApiResponse>> GetCustomerById(int id)
    {
        var customer = await _context.Customers
            .AsNoTracking()
            .Include(c => c.Owner)
            .FirstOrDefaultAsync(c => c.CustomerId == id);

        if (customer == null)
            return NotFound(new
            {
                message = "Customer not found."
            });

        if (!await CanAccessCustomerAsync(customer))
            return Forbid();

        return Ok(ToResponse(customer));
    }

    // =====================================================
    // POST: api/customers
    // Create a customer.
    // =====================================================

    [HttpPost]
    public async Task<ActionResult<CustomerApiResponse>> CreateCustomer(
        [FromBody] CustomerApiRequest request)
    {
        var customerCode = request.CustomerCode.Trim();

        if (await _context.Customers.AnyAsync(
            c => c.CustomerCode == customerCode))
        {
            return Conflict(new
            {
                message = "Customer code already exists."
            });
        }

        string? ownerId;

        if (!IsAdminOrManager())
        {
            // Sales Executive is always assigned as owner.
            ownerId = _userManager.GetUserId(User);
        }
        else
        {
            ownerId = string.IsNullOrWhiteSpace(request.OwnerId)
                ? null
                : request.OwnerId.Trim();

            if (!await IsValidSalesExecutiveAsync(ownerId))
            {
                return BadRequest(new
                {
                    message = "Select an active Sales Executive as the owner."
                });
            }
        }

        var customer = new Customer
        {
            CustomerCode = customerCode,
            CustomerName = request.CustomerName.Trim(),
            Email = request.Email.Trim(),
            Phone = request.Phone.Trim(),
            CompanyName = request.CompanyName.Trim(),
            Address = request.Address.Trim(),
            City = request.City.Trim(),
            State = request.State.Trim(),
            Status = "Active",
            OwnerId = ownerId,
            CreatedDate = DateTime.UtcNow
        };

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();

        // Audit only non-contact business details.
        await _auditLogService.LogAsync(
            action: "Create",
            entityName: "Customer",
            recordId: customer.CustomerId,
            newValue: new
            {
                customer.CustomerCode,
                customer.CustomerName,
                customer.CompanyName,
                customer.City,
                customer.State,
                customer.Status,
                customer.OwnerId
            });

        await _context.Entry(customer)
            .Reference(c => c.Owner)
            .LoadAsync();

        var response = ToResponse(customer);

        return CreatedAtAction(
            nameof(GetCustomerById),
            new { id = customer.CustomerId },
            response);
    }

    // =====================================================
    // PUT: api/customers/{id}
    // Update an existing customer.
    // =====================================================

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CustomerApiResponse>> UpdateCustomer(
        int id,
        [FromBody] CustomerApiRequest request)
    {
        var customer = await _context.Customers
            .Include(c => c.Owner)
            .FirstOrDefaultAsync(c => c.CustomerId == id);

        if (customer == null)
            return NotFound(new
            {
                message = "Customer not found."
            });

        if (!await CanAccessCustomerAsync(customer))
            return Forbid();

        var customerCode = request.CustomerCode.Trim();

        if (await _context.Customers.AnyAsync(c =>
            c.CustomerCode == customerCode &&
            c.CustomerId != id))
        {
            return Conflict(new
            {
                message = "Customer code already exists."
            });
        }

        if (!IsValidStatus(request.Status))
        {
            return BadRequest(new
            {
                message = "Status must be Active or Inactive."
            });
        }

        string? ownerId;

        if (IsAdminOrManager())
        {
            ownerId = string.IsNullOrWhiteSpace(request.OwnerId)
                ? null
                : request.OwnerId.Trim();

            if (!await IsValidSalesExecutiveAsync(ownerId))
            {
                return BadRequest(new
                {
                    message = "Select an active Sales Executive as the owner."
                });
            }
        }
        else
        {
            // Sales Executive cannot transfer ownership.
            ownerId = customer.OwnerId;
        }

        var oldValues = new
        {
            customer.CustomerCode,
            customer.CustomerName,
            customer.CompanyName,
            customer.City,
            customer.State,
            customer.Status,
            customer.OwnerId
        };

        customer.CustomerCode = customerCode;
        customer.CustomerName = request.CustomerName.Trim();
        customer.Email = request.Email.Trim();
        customer.Phone = request.Phone.Trim();
        customer.CompanyName = request.CompanyName.Trim();
        customer.Address = request.Address.Trim();
        customer.City = request.City.Trim();
        customer.State = request.State.Trim();
        customer.Status = request.Status.Trim();
        customer.OwnerId = ownerId;
        customer.ModifiedDate = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await _auditLogService.LogAsync(
            action: "Update",
            entityName: "Customer",
            recordId: customer.CustomerId,
            oldValue: oldValues,
            newValue: new
            {
                customer.CustomerCode,
                customer.CustomerName,
                customer.CompanyName,
                customer.City,
                customer.State,
                customer.Status,
                customer.OwnerId
            });

        await _context.Entry(customer)
            .Reference(c => c.Owner)
            .LoadAsync();

        return Ok(ToResponse(customer));
    }

    // =====================================================
    // DELETE: api/customers/{id}
    // Only Admin and Manager can delete customers.
    // =====================================================

    [HttpDelete("{id:int}")]
    [Authorize(Roles = RoleConstants.AdminOrManager)]
    public async Task<IActionResult> DeleteCustomer(int id)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.CustomerId == id);

        if (customer == null)
            return NotFound(new
            {
                message = "Customer not found."
            });

        var oldValues = new
        {
            customer.CustomerCode,
            customer.CustomerName,
            customer.CompanyName,
            customer.City,
            customer.State,
            customer.Status,
            customer.OwnerId
        };

        _context.Customers.Remove(customer);

        try
        {
            // AuditLogService saves pending changes, including this deletion.
            await _auditLogService.LogAsync(
                action: "Delete",
                entityName: "Customer",
                recordId: customer.CustomerId,
                oldValue: oldValues);
        }
        catch (DbUpdateException)
        {
            return Conflict(new
            {
                message = "This customer cannot be deleted because other CRM records may be linked to it."
            });
        }

        return Ok(new
        {
            message = "Customer deleted successfully."
        });
    }
}
