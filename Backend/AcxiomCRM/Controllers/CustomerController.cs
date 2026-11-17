
using AcxiomCRM.Authorization;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize(Roles = RoleConstants.AllRoles)]
public class CustomerController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AuditLogService _auditLogService;

    public CustomerController(
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

    private Task<bool> CanAccessCustomerAsync(Customer customer)
    {
        if (IsAdminOrManager())
            return Task.FromResult(true);

        var currentUserId = _userManager.GetUserId(User);

        return Task.FromResult(customer.OwnerId == currentUserId);
    }

    private async Task LoadSalesExecutivesAsync(
        CustomerViewModel model)
    {
        var executives = await _userManager.GetUsersInRoleAsync(
            RoleConstants.SalesExecutive);

        model.SalesExecutives = executives
            .Where(u => u.IsActive)
            .Select(u => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = u.Id,
                Text = u.FullName
            })
            .ToList();
    }

    // CUSTOMER LIST
    public async Task<IActionResult> Index(string? search)
    {
        var query = _context.Customers.AsQueryable();

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

        var customers = await query
            .OrderByDescending(c => c.CreatedDate)
            .ToListAsync();

        var result = new List<CustomerListViewModel>();

        foreach (var customer in customers)
        {
            var owner = string.IsNullOrEmpty(customer.OwnerId)
                ? null
                : await _userManager.FindByIdAsync(customer.OwnerId);

            result.Add(new CustomerListViewModel
            {
                CustomerId = customer.CustomerId,
                CustomerCode = customer.CustomerCode,
                CustomerName = customer.CustomerName,
                Email = customer.Email,
                Phone = customer.Phone,
                CompanyName = customer.CompanyName,
                City = customer.City,
                State = customer.State,
                Status = customer.Status,
                OwnerName = owner?.FullName ?? "Unassigned"
            });
        }

        ViewBag.Search = search;

        return View(result);
    }

    // CREATE CUSTOMER - GET
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new CustomerViewModel();

        if (IsAdminOrManager())
        {
            await LoadSalesExecutivesAsync(model);
        }

        return View(model);
    }

    // CREATE CUSTOMER - POST
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CustomerViewModel model)
    {
        if (await _context.Customers.AnyAsync(
            c => c.CustomerCode == model.CustomerCode))
        {
            ModelState.AddModelError(
                nameof(model.CustomerCode),
                "Customer code already exists.");
        }

        if (!IsAdminOrManager())
        {
            model.OwnerId = _userManager.GetUserId(User);
        }
        else if (!string.IsNullOrEmpty(model.OwnerId))
        {
            var executives = await _userManager.GetUsersInRoleAsync(
                RoleConstants.SalesExecutive);

            if (!executives.Any(u =>
                u.Id == model.OwnerId && u.IsActive))
            {
                ModelState.AddModelError(
                    nameof(model.OwnerId),
                    "Select an active Sales Executive.");
            }
        }

        if (!ModelState.IsValid)
        {
            if (IsAdminOrManager())
                await LoadSalesExecutivesAsync(model);

            return View(model);
        }

        var customer = new Customer
        {
            CustomerCode = model.CustomerCode.Trim(),
            CustomerName = model.CustomerName.Trim(),
            Email = model.Email.Trim(),
            Phone = model.Phone.Trim(),
            CompanyName = model.CompanyName.Trim(),
            Address = model.Address.Trim(),
            City = model.City.Trim(),
            State = model.State.Trim(),
            Status = "Active",
            OwnerId = model.OwnerId,
            CreatedDate = DateTime.UtcNow
        };

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();

        // AUDIT LOG: CUSTOMER CREATED
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

        TempData["Success"] = "Customer created successfully.";

        return RedirectToAction(nameof(Index));
    }

    // EDIT CUSTOMER - GET
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var customer = await _context.Customers.FindAsync(id);

        if (customer == null)
            return NotFound();

        if (!await CanAccessCustomerAsync(customer))
            return Forbid();

        var model = new CustomerViewModel
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
            OwnerId = customer.OwnerId
        };

        if (IsAdminOrManager())
            await LoadSalesExecutivesAsync(model);

        return View(model);
    }

    // EDIT CUSTOMER - POST
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CustomerViewModel model)
    {
        var customer = await _context.Customers.FindAsync(
            model.CustomerId);

        if (customer == null)
            return NotFound();

        if (!await CanAccessCustomerAsync(customer))
            return Forbid();

        if (await _context.Customers.AnyAsync(c =>
            c.CustomerCode == model.CustomerCode &&
            c.CustomerId != model.CustomerId))
        {
            ModelState.AddModelError(
                nameof(model.CustomerCode),
                "Customer code already exists.");
        }

        if (IsAdminOrManager())
        {
            if (!string.IsNullOrEmpty(model.OwnerId))
            {
                var executives = await _userManager.GetUsersInRoleAsync(
                    RoleConstants.SalesExecutive);

                if (!executives.Any(u =>
                    u.Id == model.OwnerId && u.IsActive))
                {
                    ModelState.AddModelError(
                        nameof(model.OwnerId),
                        "Select an active Sales Executive.");
                }
            }
        }
        else
        {
            model.OwnerId = customer.OwnerId;
        }

        if (!ModelState.IsValid)
        {
            if (IsAdminOrManager())
                await LoadSalesExecutivesAsync(model);

            return View(model);
        }

        // CAPTURE OLD VALUES BEFORE UPDATING
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

        customer.CustomerCode = model.CustomerCode.Trim();
        customer.CustomerName = model.CustomerName.Trim();
        customer.Email = model.Email.Trim();
        customer.Phone = model.Phone.Trim();
        customer.CompanyName = model.CompanyName.Trim();
        customer.Address = model.Address.Trim();
        customer.City = model.City.Trim();
        customer.State = model.State.Trim();
        customer.Status = model.Status;
        customer.OwnerId = model.OwnerId;
        customer.ModifiedDate = DateTime.UtcNow;

        // SAVE CUSTOMER CHANGES
        await _context.SaveChangesAsync();

        // AUDIT LOG: CUSTOMER UPDATED
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

        TempData["Success"] = "Customer updated successfully.";

        return RedirectToAction(nameof(Index));
    }

    // DELETE CUSTOMER - POST
    [Authorize(Roles = RoleConstants.AdminOrManager)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var customer = await _context.Customers.FindAsync(id);

        if (customer == null)
            return NotFound();

        // CAPTURE VALUES BEFORE DELETION
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

        // AUDIT LOG AND DELETE ARE SAVED TOGETHER
        await _auditLogService.LogAsync(
            action: "Delete",
            entityName: "Customer",
            recordId: customer.CustomerId,
            oldValue: oldValues);

        TempData["Success"] = "Customer deleted successfully.";

        return RedirectToAction(nameof(Index));
    }
}