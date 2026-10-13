using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SelfMX.Api.Data.Migrations.Postgres.App
{
    /// <inheritdoc />
    public partial class SentEmailsStatsIndexWithDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SentEmails_SentAt_ApiKeyId",
                table: "SentEmails");

            migrationBuilder.CreateIndex(
                name: "IX_SentEmails_SentAt_ApiKeyId_DomainId",
                table: "SentEmails",
                columns: new[] { "SentAt", "ApiKeyId", "DomainId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SentEmails_SentAt_ApiKeyId_DomainId",
                table: "SentEmails");

            migrationBuilder.CreateIndex(
                name: "IX_SentEmails_SentAt_ApiKeyId",
                table: "SentEmails",
                columns: new[] { "SentAt", "ApiKeyId" });
        }
    }
}
