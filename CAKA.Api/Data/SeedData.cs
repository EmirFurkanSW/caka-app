using Microsoft.EntityFrameworkCore;

namespace CAKA.Api.Data;

public static class SeedData
{
    private const string AdminUserName = "admin";
    private const string DefaultAdminPassword = "1234";

    public static async Task EnsureAdminAsync(AppDbContext db)
    {
        if (await db.Users.AnyAsync(u => u.UserName == AdminUserName))
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
