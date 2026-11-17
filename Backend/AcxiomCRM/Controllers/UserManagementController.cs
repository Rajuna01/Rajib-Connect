
using AcxiomCRM.Authorization;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers;

[Authorize(Roles = RoleConstants.Admin)]
public class UserManagementController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly AuditLogService _auditLogService;

    public UserManagementController(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        AuditLogService auditLogService)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _auditLogService = auditLogService;
    }

    private static readonly string[] AllowedRoles =
    {
        RoleConstants.Admin,
        RoleConstants.Manager,
        RoleConstants.SalesExecutive
    };

    // ----------------------------------------------------
    // INDEX
    // ----------------------------------------------------

    public async Task<IActionResult> Index()
    {
        var users = _userManager.Users.ToList();
        var userDetails = new List<UserListItemViewModel>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);

            userDetails.Add(new UserListItemViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? "",
                Roles = string.Join(", ", roles),
                IsActive = user.IsActive
            });
        }

        return View(userDetails);
    }

    // ----------------------------------------------------
    // CREATE - GET
    // ----------------------------------------------------

    [HttpGet]
    public IActionResult Create()
    {
        return View(new CreateUserViewModel());
    }

    // ----------------------------------------------------
    // CREATE - POST
    // ----------------------------------------------------

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserViewModel model)
    {
        if (!AllowedRoles.Contains(model.Role))
        {
            ModelState.AddModelError(
                nameof(model.Role),
                "Invalid role.");
        }

        if (!ModelState.IsValid)
            return View(model);

        if (await _userManager.FindByEmailAsync(model.Email) != null)
        {
            ModelState.AddModelError(
                nameof(model.Email),
                "Email already registered.");

            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FullName = model.FullName.Trim(),
            IsActive = true,
            EmailConfirmed = false
        };

        var result = await _userManager.CreateAsync(
            user, model.Password);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            return View(model);
        }

        var roleResult = await _userManager.AddToRoleAsync(
            user, model.Role);

        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);

            foreach (var error in roleResult.Errors)
                ModelState.AddModelError("", error.Description);

            return View(model);
        }

        // Audit: User created.
        await _auditLogService.LogAsync(
            action: "Create",
            entityName: "ApplicationUser",
            newValue: new
            {
                UserId = user.Id,
                Role = model.Role,
                IsActive = user.IsActive
            }
        );

        TempData["Success"] = "User created successfully.";

        return RedirectToAction(nameof(Index));
    }

    // ----------------------------------------------------
    // EDIT - GET
    // ----------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return NotFound();

        var user = await _userManager.FindByIdAsync(id);

        if (user == null)
            return NotFound();

        var roles = await _userManager.GetRolesAsync(user);

        return View(new EditUserViewModel
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? "",
            Role = roles.FirstOrDefault()
                ?? RoleConstants.SalesExecutive
        });
    }

    // ----------------------------------------------------
    // EDIT - POST
    // ----------------------------------------------------

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditUserViewModel model)
    {
        if (!AllowedRoles.Contains(model.Role))
        {
            ModelState.AddModelError(
                nameof(model.Role),
                "Invalid role.");
        }

        if (!ModelState.IsValid)
            return View(model);

        var user = await _userManager.FindByIdAsync(model.Id);

        if (user == null)
            return NotFound();

        var currentUserId = _userManager.GetUserId(User);
        var isEditingSelf = user.Id == currentUserId;

        var currentRoles = await _userManager.GetRolesAsync(user);

        var oldRole = currentRoles.FirstOrDefault()
            ?? string.Empty;

        var wasAdmin = currentRoles.Contains(RoleConstants.Admin);
        var willBeAdmin = model.Role == RoleConstants.Admin;

        // Prevent the current Admin from removing their own Admin role.
        if (isEditingSelf && wasAdmin && !willBeAdmin)
        {
            ModelState.AddModelError(
                nameof(model.Role),
                "You cannot remove your own Admin role.");

            return View(model);
        }

        // Prevent removal of the last Admin.
        if (wasAdmin && !willBeAdmin &&
            await IsLastAdminAsync(user.Id))
        {
            ModelState.AddModelError(
                nameof(model.Role),
                "The last Admin cannot be demoted.");

            return View(model);
        }

        var existingEmail =
            await _userManager.FindByEmailAsync(model.Email);

        if (existingEmail != null && existingEmail.Id != user.Id)
        {
            ModelState.AddModelError(
                nameof(model.Email),
                "Email already registered.");

            return View(model);
        }

        // Capture which profile fields are being changed.
        var changedFields = new List<string>();

        if (user.FullName != model.FullName.Trim())
            changedFields.Add("FullName");

        if (user.Email != model.Email.Trim())
            changedFields.Add("Email");

        user.FullName = model.FullName.Trim();
        user.Email = model.Email.Trim();
        user.UserName = model.Email.Trim();

        var updateResult = await _userManager.UpdateAsync(user);

        if (!updateResult.Succeeded)
        {
            foreach (var error in updateResult.Errors)
                ModelState.AddModelError("", error.Description);

            return View(model);
        }

        var removeResult = await _userManager.RemoveFromRolesAsync(
            user, currentRoles);

        if (!removeResult.Succeeded)
        {
            TempData["Error"] = "Could not update the user's role.";
            return RedirectToAction(nameof(Index));
        }

        var addResult = await _userManager.AddToRoleAsync(
            user, model.Role);

        if (!addResult.Succeeded)
        {
            // Restore previous roles if the new assignment fails.
            await _userManager.AddToRolesAsync(user, currentRoles);

            TempData["Error"] =
                "Role update failed. Previous role restored.";

            return RedirectToAction(nameof(Index));
        }

        // Audit: Profile updated.
        if (changedFields.Count > 0)
        {
            await _auditLogService.LogAsync(
                action: "Update",
                entityName: "ApplicationUser",
                newValue: new
                {
                    UserId = user.Id,
                    ChangedFields = changedFields
                }
            );
        }

        // Audit: Role changed.
        if (oldRole != model.Role)
        {
            await _auditLogService.LogAsync(
                action: "RoleChange",
                entityName: "ApplicationUser",
                newValue: new
                {
                    UserId = user.Id,
                    OldRole = oldRole,
                    NewRole = model.Role
                }
            );
        }

        TempData["Success"] = "User updated successfully.";

        return RedirectToAction(nameof(Index));
    }

    // ----------------------------------------------------
    // ACTIVATE / DEACTIVATE USER
    // ----------------------------------------------------

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(string id)
    {
        var user = await _userManager.FindByIdAsync(id);

        if (user == null)
            return NotFound();

        var currentUserId = _userManager.GetUserId(User);

        if (user.Id == currentUserId)
        {
            TempData["Error"] =
                "You cannot deactivate your own account.";

            return RedirectToAction(nameof(Index));
        }

        var roles = await _userManager.GetRolesAsync(user);

        if (user.IsActive &&
            roles.Contains(RoleConstants.Admin) &&
            await IsLastAdminAsync(user.Id))
        {
            TempData["Error"] =
                "The last Admin cannot be deactivated.";

            return RedirectToAction(nameof(Index));
        }

        var oldStatus = user.IsActive;

        user.IsActive = !user.IsActive;

        var result = await _userManager.UpdateAsync(user);

        if (result.Succeeded)
        {
            var action = user.IsActive
                ? "Activate"
                : "Deactivate";

            await _auditLogService.LogAsync(
                action: action,
                entityName: "ApplicationUser",
                newValue: new
                {
                    UserId = user.Id,
                    OldIsActive = oldStatus,
                    NewIsActive = user.IsActive
                }
            );

            TempData["Success"] = "User status updated.";
        }
        else
        {
            TempData["Error"] = "Could not update user status.";
        }

        return RedirectToAction(nameof(Index));
    }

    // ----------------------------------------------------
    // SECURITY: CHECK LAST ACTIVE ADMIN
    // ----------------------------------------------------

    private async Task<bool> IsLastAdminAsync(string excludedUserId)
    {
        var admins = await _userManager.GetUsersInRoleAsync(
            RoleConstants.Admin);

        return admins.Count(u =>
            u.Id != excludedUserId && u.IsActive) == 0;
    }
}
