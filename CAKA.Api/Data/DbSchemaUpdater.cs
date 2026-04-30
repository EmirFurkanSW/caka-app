using Microsoft.EntityFrameworkCore;

namespace CAKA.Api.Data;

/// <summary>
/// Mevcut veritabanına Jobs tablosu ve WorkLogs.JobId sütununu ekler (EnsureCreated eski kurulumda Jobs oluşturmadığı için).
/// </summary>
public static class DbSchemaUpdater
{
    public static void EnsureUserHourlyRateColumn(AppDbContext db)
    {
        var provider = db.Database.ProviderName ?? "";
        try
        {
            if (provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
                EnsureUserHourlyRateSqlite(db);
            else if (provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
                EnsureUserHourlyRateNpgsql(db);
        }
        catch (Exception ex)
        {
            Console.WriteLine("DbSchemaUpdater (HourlyRate): " + ex.Message);
        }
    }

    private static void EnsureUserHourlyRateSqlite(AppDbContext db)
    {
        try
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN HourlyRate REAL NOT NULL DEFAULT 0;");
        }
        catch
        {
            // Sütun zaten varsa hata verir, yoksay
        }
    }

    private static void EnsureUserHourlyRateNpgsql(AppDbContext db)
    {
        try
        {
            db.Database.ExecuteSqlRaw("""ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "HourlyRate" NUMERIC(12,2) NOT NULL DEFAULT 0;""");
        }
        catch
        {
            try
            {
                db.Database.ExecuteSqlRaw("""ALTER TABLE "Users" ADD COLUMN "HourlyRate" NUMERIC(12,2) NOT NULL DEFAULT 0;""");
            }
            catch { }
        }
    }

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

    /// <summary>İş aşamaları, çalışan atamaları ve plan saatleri tabloları.</summary>
    public static void EnsureJobPlanningTables(AppDbContext db)
    {
        var provider = db.Database.ProviderName ?? "";
        try
        {
            if (provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
                EnsureJobPlanningSqlite(db);
            else if (provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
                EnsureJobPlanningNpgsql(db);
        }
        catch (Exception ex)
        {
            Console.WriteLine("DbSchemaUpdater (JobPlanning): " + ex.Message);
        }
    }

    private static void EnsureJobPlanningSqlite(AppDbContext db)
    {
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS JobStages (
                Id TEXT NOT NULL PRIMARY KEY,
                JobId TEXT NOT NULL,
                Name TEXT NOT NULL,
                Description TEXT NOT NULL,
                SortOrder INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (JobId) REFERENCES Jobs(Id) ON DELETE CASCADE
            );
            """);
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_JobStages_JobId ON JobStages(JobId);");

        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS JobParticipants (
                JobId TEXT NOT NULL,
                UserName TEXT NOT NULL,
                HourlyRate REAL NOT NULL DEFAULT 0,
                HourlyRateCurrency TEXT NOT NULL DEFAULT 'TRY',
                PRIMARY KEY (JobId, UserName),
                FOREIGN KEY (JobId) REFERENCES Jobs(Id) ON DELETE CASCADE
            );
            """);

        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS JobStagePlans (
                Id TEXT NOT NULL PRIMARY KEY,
                JobStageId TEXT NOT NULL,
                UserName TEXT NOT NULL,
                PlannedHours REAL NOT NULL DEFAULT 0,
                FOREIGN KEY (JobStageId) REFERENCES JobStages(Id) ON DELETE CASCADE,
                UNIQUE (JobStageId, UserName)
            );
            """);
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_JobStagePlans_Stage ON JobStagePlans(JobStageId);");
    }

    private static void EnsureJobPlanningNpgsql(AppDbContext db)
    {
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "JobStages" (
                "Id" UUID NOT NULL PRIMARY KEY,
                "JobId" UUID NOT NULL REFERENCES "Jobs"("Id") ON DELETE CASCADE,
                "Name" VARCHAR(200) NOT NULL,
                "Description" VARCHAR(2000) NOT NULL,
                "SortOrder" INTEGER NOT NULL DEFAULT 0
            );
            """);
        db.Database.ExecuteSqlRaw("""CREATE INDEX IF NOT EXISTS "IX_JobStages_JobId" ON "JobStages" ("JobId");""");

        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "JobParticipants" (
                "JobId" UUID NOT NULL REFERENCES "Jobs"("Id") ON DELETE CASCADE,
                "UserName" VARCHAR(128) NOT NULL,
                "HourlyRate" NUMERIC(12,2) NOT NULL DEFAULT 0,
                "HourlyRateCurrency" VARCHAR(8) NOT NULL DEFAULT 'TRY',
                PRIMARY KEY ("JobId", "UserName")
            );
            """);

        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "JobStagePlans" (
                "Id" UUID NOT NULL PRIMARY KEY,
                "JobStageId" UUID NOT NULL REFERENCES "JobStages"("Id") ON DELETE CASCADE,
                "UserName" VARCHAR(128) NOT NULL,
                "PlannedHours" NUMERIC(12,2) NOT NULL DEFAULT 0,
                UNIQUE ("JobStageId", "UserName")
            );
            """);
        db.Database.ExecuteSqlRaw("""CREATE INDEX IF NOT EXISTS "IX_JobStagePlans_Stage" ON "JobStagePlans" ("JobStageId");""");
    }

    /// <summary>Mevcut JobParticipants tablosuna saatlik ücret para birimi sütunu ekler.</summary>
    public static void EnsureJobParticipantCurrencyColumn(AppDbContext db)
    {
        var provider = db.Database.ProviderName ?? "";
        try
        {
            if (provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    db.Database.ExecuteSqlRaw("ALTER TABLE JobParticipants ADD COLUMN HourlyRateCurrency TEXT NOT NULL DEFAULT 'TRY';");
                }
                catch { /* sütun zaten var */ }
            }
            else if (provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    db.Database.ExecuteSqlRaw(
                        """ALTER TABLE "JobParticipants" ADD COLUMN IF NOT EXISTS "HourlyRateCurrency" VARCHAR(8) NOT NULL DEFAULT 'TRY';""");
                }
                catch
                {
                    try
                    {
                        db.Database.ExecuteSqlRaw(
                            """ALTER TABLE "JobParticipants" ADD COLUMN "HourlyRateCurrency" VARCHAR(8) NOT NULL DEFAULT 'TRY';""");
                    }
                    catch { }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("DbSchemaUpdater (JobParticipantCurrency): " + ex.Message);
        }
    }

    /// <summary>WorkLogs tablosuna JobStageId (hangi aşamada çalışıldığı) sütununu ekler.</summary>
    public static void EnsureWorkLogJobStageIdColumn(AppDbContext db)
    {
        var provider = db.Database.ProviderName ?? "";
        try
        {
            if (provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    db.Database.ExecuteSqlRaw("ALTER TABLE WorkLogs ADD COLUMN JobStageId TEXT NULL REFERENCES JobStages(Id) ON DELETE SET NULL;");
                }
                catch
                {
                    try
                    {
                        db.Database.ExecuteSqlRaw("ALTER TABLE WorkLogs ADD COLUMN JobStageId TEXT NULL;");
                    }
                    catch { }
                }
            }
            else if (provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    db.Database.ExecuteSqlRaw("""ALTER TABLE "WorkLogs" ADD COLUMN IF NOT EXISTS "JobStageId" UUID NULL REFERENCES "JobStages"("Id") ON DELETE SET NULL;""");
                }
                catch
                {
                    try
                    {
                        db.Database.ExecuteSqlRaw("""ALTER TABLE "WorkLogs" ADD COLUMN "JobStageId" UUID NULL;""");
                    }
                    catch { }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("DbSchemaUpdater (WorkLogJobStageId): " + ex.Message);
        }
    }
}
