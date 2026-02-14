using Microsoft.EntityFrameworkCore;

namespace CAKA.Api.Data;

public static class SeedData
{
    private const string AdminUserName = "admin";
    private const string DefaultAdminPassword = "1234";

    private const string LegacyAdminUserName = "oguzturunc";

    public static async Task EnsureAdminAsync(AppDbContext db)
    {
        var hasAdmin = await db.Users.AnyAsync(u => u.UserName == AdminUserName);
        var legacyUser = await db.Users.FirstOrDefaultAsync(u => u.UserName == LegacyAdminUserName);

        if (legacyUser != null)
        {
            if (hasAdmin)
            {
                await db.WorkLogs.Where(w => w.UserName == LegacyAdminUserName)
                    .ExecuteUpdateAsync(s => s.SetProperty(w => w.UserName, AdminUserName));
                db.Users.Remove(legacyUser);
                await db.SaveChangesAsync();
            }
            else
            {
                legacyUser.UserName = AdminUserName;
                legacyUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultAdminPassword);
                legacyUser.DisplayName = "Yönetici";
                legacyUser.Role = "Admin";
                await db.WorkLogs.Where(w => w.UserName == LegacyAdminUserName)
                    .ExecuteUpdateAsync(s => s.SetProperty(w => w.UserName, AdminUserName));
                await db.SaveChangesAsync();
            }
            return;
        }

        if (hasAdmin)
            return;

        var hash = BCrypt.Net.BCrypt.HashPassword(DefaultAdminPassword);
        db.Users.Add(new UserEntity
        {
            UserName = AdminUserName,
            PasswordHash = hash,
            DisplayName = "Yönetici",
            Department = "",
            IsSuspended = false,
            Role = "Admin"
        });
        await db.SaveChangesAsync();
    }
}
