using Microsoft.EntityFrameworkCore;

namespace CAKA.Api.Data;

public static class SeedData
{
    /// <summary>Silme sonrası korunan tek oturum (küçük harf).</summary>
    public const string AdminUserName = "admin";
    private const string DefaultAdminPassword = "1234";

    private const string LegacyAdminUserName = "oguzturunc";

    /// <summary>
    /// İş kayıtları, işler, aşamalar, katılımcılar ve <b>admin dışındaki tüm kullanıcıları</b> kalıcı olarak siler.
    /// Admin satırına dokunulmaz; böyle kullanıcı yoksa sonra <see cref="EnsureAdminAsync"/> ile oluşturulabilir.
    /// </summary>
    public static async Task WipeAllUserGeneratedDataKeepingAdminAsync(AppDbContext db)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            await db.WorkLogs.ExecuteDeleteAsync();
            await db.JobStagePlans.ExecuteDeleteAsync();
            await db.JobStages.ExecuteDeleteAsync();
            await db.JobParticipants.ExecuteDeleteAsync();
            await db.Jobs.ExecuteDeleteAsync();
            await db.Users.Where(u => u.UserName != AdminUserName).ExecuteDeleteAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

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
