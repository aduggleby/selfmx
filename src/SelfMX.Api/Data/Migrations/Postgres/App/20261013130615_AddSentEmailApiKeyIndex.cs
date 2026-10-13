using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SelfMX.Api.Data.Migrations.Postgres.App
{
    /// <inheritdoc />
    public partial class AddSentEmailApiKeyIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_SentEmails_ApiKeyId_SentAt",
                table: "SentEmails",
                columns: new[] { "ApiKeyId", "SentAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SentEmails_ApiKeyId_SentAt",
                table: "SentEmails");
        }
    }
}
