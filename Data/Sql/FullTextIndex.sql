-- Optional: SQL Server full-text search for UC-05 (not available on LocalDB).
-- Run once after the first migration, then set "Search:UseFullText": true in appsettings.
-- EF Core migrations can also run this via migrationBuilder.Sql(..., suppressTransaction: true).

IF NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = 'IndlelaCatalog')
    CREATE FULLTEXT CATALOG IndlelaCatalog AS DEFAULT;
GO

-- KEY INDEX must be the primary key index name EF generated for Courses.
IF NOT EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('dbo.Courses'))
    CREATE FULLTEXT INDEX ON dbo.Courses
    (
        Title LANGUAGE 1033,
        Description LANGUAGE 1033,
        Provider LANGUAGE 1033,
        Category LANGUAGE 1033
    )
    KEY INDEX PK_Courses
    WITH CHANGE_TRACKING AUTO;
GO
