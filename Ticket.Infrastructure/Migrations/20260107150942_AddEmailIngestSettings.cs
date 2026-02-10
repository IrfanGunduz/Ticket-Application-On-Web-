using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ticket.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailIngestSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmailIngestSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    PollSeconds = table.Column<int>(type: "int", nullable: false),
                    TargetAddress = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ImapHost = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ImapPort = table.Column<int>(type: "int", nullable: false),
                    ImapUseSsl = table.Column<bool>(type: "bit", nullable: false),
                    ImapUserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EncryptedImapPassword = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    MarkAsRead = table.Column<bool>(type: "bit", nullable: false),
                    Folder = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailIngestSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailIngestSettings");
        }
    }
}
