using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;

namespace RecruitOps.Infrastructure.Persistence;

/// <summary>Development-only seed of a single tenant + admin user. Runs only when
/// Seed:AdminEmail and Seed:AdminPassword are supplied via config (user-secrets/env),
/// so no default credentials are ever committed.</summary>
public static class DbInitializer
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var config = sp.GetRequiredService<IConfiguration>();

        var email = config["Seed:AdminEmail"];
        var password = config["Seed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return; // nothing to seed without explicit, non-committed credentials

        var db = sp.GetRequiredService<AppDbContext>();

        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == email, ct))
            return;

        var company = new Company { Name = "Default Company", Slug = "default" };
        db.Companies.Add(company);

        var hasher = sp.GetRequiredService<IPasswordHasher<User>>();
        var admin = new User
        {
            TenantId = company.Id,
            Email = email,
            DisplayName = "Administrator",
            Role = UserRole.Admin,
            IsActive = true,
        };
        admin.PasswordHash = hasher.HashPassword(admin, password);
        db.Users.Add(admin);

        await db.SaveChangesAsync(ct);
    }
}
