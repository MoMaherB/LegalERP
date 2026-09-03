using LegalERP.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LegalERP.Infrastructure.Persistence;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        using var scope = serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

        // Ensure all roles exist
        string[] roles = { "SuperAdmin", "Admin", "Editor", "Viewer" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
                logger.LogInformation("Created role: {Role}", role);
            }
        }

        var seedConfig = configuration.GetSection("AdminSeeding");
        var superAdminEmail = seedConfig["SuperAdminEmail"] ?? "admin@legalerp.com";
        var superAdminFullName = seedConfig["SuperAdminFullName"] ?? "المدير العام";
        var defaultPassword = seedConfig["DefaultPassword"] ?? "TempP@ssword123!";
        bool.TryParse(seedConfig["ForceSuperAdminReset"], out var forceReset);

        var existingSuperAdmin = await userManager.FindByEmailAsync(superAdminEmail);

        // First-time seeding: create the SuperAdmin if no users exist
        if (existingSuperAdmin == null)
        {
            var superAdmin = new ApplicationUser
            {
                UserName = superAdminEmail,
                Email = superAdminEmail,
                FullName = superAdminFullName,
                EmailConfirmed = true,
                IsActive = true
            };  

            var result = await userManager.CreateAsync(superAdmin, defaultPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(superAdmin, "SuperAdmin");
                logger.LogInformation("SuperAdmin account created: {Email}", superAdminEmail);
            }
            else
            {
                foreach (var error in result.Errors)
                    logger.LogError("Seeder error: {Error}", error.Description);
            }
        }
        // Force-reset: if the flag is set in appsettings.json, reset the SuperAdmin's password
        else if (forceReset)
        {
            var resetToken = await userManager.GeneratePasswordResetTokenAsync(existingSuperAdmin);
            var resetResult = await userManager.ResetPasswordAsync(existingSuperAdmin, resetToken, defaultPassword);
            if (resetResult.Succeeded)
            {
                logger.LogWarning("SuperAdmin password was force-reset to the default password.");
            }

            // Ensure the user actually has the SuperAdmin role (in case they were created manually with a different role)
            var currentRoles = await userManager.GetRolesAsync(existingSuperAdmin);
            if (!currentRoles.Contains("SuperAdmin"))
            {
                await userManager.RemoveFromRolesAsync(existingSuperAdmin, currentRoles);
                await userManager.AddToRoleAsync(existingSuperAdmin, "SuperAdmin");
                logger.LogInformation("Granted SuperAdmin role to existing user: {Email}", superAdminEmail);
            }
        }
    }
}
