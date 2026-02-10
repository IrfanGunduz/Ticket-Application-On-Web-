using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ticket.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixProblemIsActiveDefault2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DECLARE @dfName sysname;
DECLARE @sql nvarchar(max);

SELECT @dfName = dc.name
FROM sys.default_constraints dc
JOIN sys.columns c ON c.default_object_id = dc.object_id
JOIN sys.tables t ON t.object_id = c.object_id
WHERE t.name = N'Problems' AND c.name = N'IsActive';

IF @dfName IS NOT NULL
BEGIN
    SET @sql = N'ALTER TABLE dbo.Problems DROP CONSTRAINT ' + QUOTENAME(@dfName);
    EXEC sp_executesql @sql;
END

-- default yoksa ekle
IF NOT EXISTS (
    SELECT 1
    FROM sys.default_constraints dc
    JOIN sys.columns c ON c.default_object_id = dc.object_id
    JOIN sys.tables t ON t.object_id = c.object_id
    WHERE t.name = N'Problems' AND c.name = N'IsActive'
)
BEGIN
    ALTER TABLE dbo.Problems
    ADD CONSTRAINT DF_Problems_IsActive DEFAULT(1) FOR IsActive;
END
");
        }



        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DECLARE @dfName sysname;
DECLARE @sql nvarchar(max);

SELECT @dfName = dc.name
FROM sys.default_constraints dc
JOIN sys.columns c ON c.default_object_id = dc.object_id
JOIN sys.tables t ON t.object_id = c.object_id
WHERE t.name = N'Problems' AND c.name = N'IsActive';

IF @dfName IS NOT NULL
BEGIN
    SET @sql = N'ALTER TABLE dbo.Problems DROP CONSTRAINT ' + QUOTENAME(@dfName);
    EXEC sp_executesql @sql;
END
");
        }


    }
}
