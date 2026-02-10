using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ticket.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInternetMessageIdSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InReplyToInternetMessageId",
                table: "TicketMessages",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InternetMessageId",
                table: "TicketMessages",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InReplyToInternetMessageId",
                table: "EmailIngestReceipts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InternetMessageId",
                table: "EmailIngestReceipts",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketMessages_InReplyToInternetMessageId",
                table: "TicketMessages",
                column: "InReplyToInternetMessageId",
                filter: "[InReplyToInternetMessageId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TicketMessages_InternetMessageId",
                table: "TicketMessages",
                column: "InternetMessageId",
                filter: "[InternetMessageId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EmailIngestReceipts_InternetMessageId",
                table: "EmailIngestReceipts",
                column: "InternetMessageId",
                unique: true,
                filter: "[InternetMessageId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TicketMessages_InReplyToInternetMessageId",
                table: "TicketMessages");

            migrationBuilder.DropIndex(
                name: "IX_TicketMessages_InternetMessageId",
                table: "TicketMessages");

            migrationBuilder.DropIndex(
                name: "IX_EmailIngestReceipts_InternetMessageId",
                table: "EmailIngestReceipts");

            migrationBuilder.DropColumn(
                name: "InReplyToInternetMessageId",
                table: "TicketMessages");

            migrationBuilder.DropColumn(
                name: "InternetMessageId",
                table: "TicketMessages");

            migrationBuilder.DropColumn(
                name: "InReplyToInternetMessageId",
                table: "EmailIngestReceipts");

            migrationBuilder.DropColumn(
                name: "InternetMessageId",
                table: "EmailIngestReceipts");
        }
    }
}
