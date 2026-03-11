-- Mevcut veritabanınız varsa (EnsureCreated daha önce çalıştıysa) bu script ile Jobs tablosunu ve WorkLogs.JobId sütununu ekleyebilirsiniz.
-- SQLite için:

-- 1) Jobs tablosu
CREATE TABLE IF NOT EXISTS "Jobs" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Jobs" PRIMARY KEY,
    "Code" TEXT NOT NULL,
    "Description" TEXT NOT NULL,
    "IsActive" INTEGER NOT NULL DEFAULT 1
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Jobs_Code" ON "Jobs" ("Code");

-- 2) WorkLogs tablosuna JobId sütunu (yoksa ekleyin; SQLite'da ALTER ADD COLUMN)
-- NOT: SQLite'da sütun var mı kontrolü zor. Sütun yoksa: 
ALTER TABLE "WorkLogs" ADD COLUMN "JobId" TEXT NULL REFERENCES "Jobs"("Id");
