-- =============================================================================
-- EGYSZERI SQL SCRIPT - Meglévő adatbázis előkészítése a migrációs rendszerhez
-- =============================================================================
--
-- MIKOR KELL FUTTATNI:
--   Csak egyszer, az app frissítése ELŐTT, minden olyan gépen/adatbázison,
--   ahol a ToolRental app már fut (tehát már van ToolRentalDB).
--
-- MIT CSINÁL:
--   1. Létrehozza az EF Core migrációs "naplófüzet" táblát (__EFMigrationsHistory)
--   2. Beírja, hogy az első migráció (InitialSqlServer) már kész
--      – mert a te DB-d már tartalmazza az összes eredeti táblát/mezőt
--   3. Így az app indulásakor a Migrate() tudni fogja:
--      – "InitialSqlServer" ✅ kész, nem kell újra létrehozni
--      – "AddValidationConstraints" ❌ új, ezt lefuttatom automatikusan
--
-- FIGYELEM:
--   - Futtasd MINDKÉT adatbázison: ToolRentalDB és ToolRentalDB_TEST
--   - Ha új gépen telepíted az appot (üres DB), NEM kell ez a script!
--     Az app a Migrate()-tel automatikusan mindent létrehoz.
-- =============================================================================

-- Válaszd ki az adatbázist (ismételd meg ToolRentalDB_TEST-tel is!)
-- USE ToolRentalDB;
-- GO

-- 1. Migrációs napló tábla létrehozása (ha még nincs)
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '__EFMigrationsHistory')
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
    PRINT 'Migrációs napló tábla létrehozva.';
END
ELSE
BEGIN
    PRINT 'Migrációs napló tábla már létezik.';
END
GO

-- 2. Az első migráció megjelölése "már kész"-ként
-- (mert a DB-d már tartalmazza az összes eredeti táblát)
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20260222173707_InitialSqlServer')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES ('20260222173707_InitialSqlServer', '9.0.7');
    PRINT 'InitialSqlServer migráció megjelölve kész-ként.';
END
ELSE
BEGIN
    PRINT 'InitialSqlServer migráció már meg van jelölve.';
END
GO

PRINT '';
PRINT 'KÉSZ! Most frissítheted az appot.';
PRINT 'Az app első indításakor automatikusan lefuttatja az "AddValidationConstraints" migrációt.';
GO
