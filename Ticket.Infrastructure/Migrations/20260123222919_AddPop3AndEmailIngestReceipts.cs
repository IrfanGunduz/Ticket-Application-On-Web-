using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ticket.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPop3AndEmailIngestReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EncryptedPop3Password",
                table: "EmailIngestSettings",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Pop3Host",
                table: "EmailIngestSettings",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Pop3Port",
                table: "EmailIngestSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "Pop3UseSsl",
                table: "EmailIngestSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Pop3UserName",
                table: "EmailIngestSettings",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Protocol",
                table: "EmailIngestSettings",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "EmailIngestReceipts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExternalMessageId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    From = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailIngestReceipts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailIngestReceipts_ExternalMessageId",
                table: "EmailIngestReceipts",
                column: "ExternalMessageId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailIngestReceipts");

            migrationBuilder.DropColumn(
                name: "EncryptedPop3Password",
                table: "EmailIngestSettings");

            migrationBuilder.DropColumn(
                name: "Pop3Host",
                table: "EmailIngestSettings");

            migrationBuilder.DropColumn(
                name: "Pop3Port",
                table: "EmailIngestSettings");

            migrationBuilder.DropColumn(
                name: "Pop3UseSsl",
                table: "EmailIngestSettings");

            migrationBuilder.DropColumn(
                name: "Pop3UserName",
                table: "EmailIngestSettings");

            migrationBuilder.DropColumn(
                name: "Protocol",
                table: "EmailIngestSettings");
        }
    }
}
