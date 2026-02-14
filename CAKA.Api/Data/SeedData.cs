using Microsoft.EntityFrameworkCore;

namespace CAKA.Api.Data;

public static class SeedData
{
    private const string AdminUserName = "admin";
    private const string DefaultAdminPassword = "1234";

    private const string LegacyAdminUserName = "oguzturunc";

    public static async Task EnsureAdminAsync(AppDbContext db)
    {
        if (await db.Users.AnyAsync(u => u.UserName == AdminUserName))
            return;

        var legacyAdmin = await db.Users.FirstOrDefaultAsync(u => u.UserName == LegacyAdminUserName);
        if (legacyAdmin != null)
        {
            legacyAdmin.UserName = AdminUserName;
            legacyAdmin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultAdminPassword);
            legacyAdmin.DisplayName = "Yönetici";
            legacyAdmin.Role = "Admin";
            await db.WorkLogs.Where(w => w.UserName == LegacyAdminUserName)
                .ExecuteUpdateAsync(s => s.SetProperty(w => w.UserName, AdminUserName));
            await db.SaveChangesAsync();
            return;
        }

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
