using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SelfMX.Api.Data.Migrations.SqlServer.App
{
    /// <inheritdoc />
    public partial class SentEmailsSentAtApiKeyIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SentEmails_SentAt",
                table: "SentEmails");

            migrationBuilder.CreateIndex(
                name: "IX_SentEmails_SentAt_ApiKeyId",
                table: "SentEmails",
                columns: new[] { "SentAt", "ApiKeyId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SentEmails_SentAt_ApiKeyId",
                table: "SentEmails");

            migrationBuilder.CreateIndex(
                name: "IX_SentEmails_SentAt",
                table: "SentEmails",
                column: "SentAt");
        }
    }
}
