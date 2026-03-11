using Microsoft.EntityFrameworkCore;

namespace CAKA.Api.Data;

/// <summary>
/// Mevcut veritabanına Jobs tablosu ve WorkLogs.JobId sütununu ekler (EnsureCreated eski kurulumda Jobs oluşturmadığı için).
/// </summary>
public static class DbSchemaUpdater
{
    public static void EnsureJobsTableExists(AppDbContext db)
    {
        var provider = db.Database.ProviderName ?? "";
        try
        {
            if (provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
                EnsureJobsTableSqlite(db);
            else if (provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
                EnsureJobsTableNpgsql(db);
        }
        catch (Exception ex)
        {
            Console.WriteLine("DbSchemaUpdater: " + ex.Message);
        }
    }

    private static void EnsureJobsTableSqlite(AppDbContext db)
    {
        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS Jobs (
                Id TEXT NOT NULL PRIMARY KEY,
                Code TEXT NOT NULL,
                Description TEXT NOT NULL,
                IsActive INTEGER NOT NULL DEFAULT 1
            );
        ");
        db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_Jobs_Code ON Jobs(Code);");

        try
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE WorkLogs ADD COLUMN JobId TEXT NULL;");
        }
        catch
        {
            // Sütun zaten varsa hata verir, yoksay
        }
    }

    private static void EnsureJobsTableNpgsql(AppDbContext db)
    {
        const string createTable = """
            CREATE TABLE IF NOT EXISTS "Jobs" (
                "Id" UUID NOT NULL PRIMARY KEY,
                "Code" VARCHAR(64) NOT NULL,
                "Description" VARCHAR(500) NOT NULL,
                "IsActive" BOOLEAN NOT NULL DEFAULT TRUE
            );
            """;
        db.Database.ExecuteSqlRaw(createTable);
        db.Database.ExecuteSqlRaw("""CREATE UNIQUE INDEX IF NOT EXISTS "IX_Jobs_Code" ON "Jobs" ("Code");""");

        try
        {
            db.Database.ExecuteSqlRaw("""ALTER TABLE "WorkLogs" ADD COLUMN IF NOT EXISTS "JobId" UUID NULL;""");
        }
        catch
        {
            try
            {
                db.Database.ExecuteSqlRaw("""ALTER TABLE "WorkLogs" ADD COLUMN "JobId" UUID NULL;""");
            }
            catch { }
        }
    }
}
