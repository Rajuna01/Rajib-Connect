
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Identity;

namespace AcxiomCRM.Services;

public static class RoleInitializer
{
    public static async Task InitializeAsync(
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        var roleManager = serviceProvider
            .GetRequiredService<RoleManager<IdentityRole>>();

        var userManager = serviceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        // Roles required by AcxiomCRM
        string[] roles =
        {
            "Admin",
            "Manager",
            "SalesExecutive"
        };

        // Create roles if they do not already exist
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var result = await roleManager.CreateAsync(
                    new IdentityRole(role));

                if (!result.Succeeded)
                {
                    var errors = string.Join(
                        ", ",
                        result.Errors.Select(e => e.Description));

                    throw new InvalidOperationException(
                        $"Could not create role {role}: {errors}");
                }
            }
        }

        // Get configured administrator email
        var adminEmail = configuration["AdminSeed:Email"];

        if (string.IsNullOrWhiteSpace(adminEmail))
        {
            throw new InvalidOperationException(
                "AdminSeed:Email is not configured.");
        }

        // Find the existing administrator account
        var adminUser = await userManager.FindByEmailAsync(adminEmail);

        // Check for null BEFORE accessing administrator properties
        if (adminUser == null)
        {
            throw new InvalidOperationException(
                "Admin account not found. Register the configured email first.");
        }

        // Update administrator's full name
        adminUser.FullName = "Rajib Lochan Baruah";

        var updateResult = await userManager.UpdateAsync(adminUser);

        if (!updateResult.Succeeded)
        {
            var errors = string.Join(
                ", ",
                updateResult.Errors.Select(e => e.Description));

            throw new InvalidOperationException(
                $"Could not update administrator's full name: {errors}");
        }

        // Assign Admin role if it is not already assigned
        if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
        {
            var roleResult = await userManager.AddToRoleAsync(
                adminUser,
                "Admin");

            if (!roleResult.Succeeded)
            {
                var errors = string.Join(
                    ", ",
                    roleResult.Errors.Select(e => e.Description));

                throw new InvalidOperationException(
                    $"Could not assign Admin role: {errors}");
            }
        }

        // Verify assigned roles
        var assignedRoles = await userManager.GetRolesAsync(adminUser);

        Console.WriteLine(
            $"Admin account: {adminUser.Email}");

        Console.WriteLine(
            $"Assigned roles: {string.Join(", ", assignedRoles)}");
    }
}
