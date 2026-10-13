using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SelfMX.Api.Data.Migrations.SqlServer.App
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApiKeys",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    KeyHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    KeySalt = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    KeyPrefix = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: false),
                    IsAdmin = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUsedIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Domains",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VerificationStartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SesIdentityArn = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DnsRecordsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastCheckedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Domains", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RevokedApiKeys",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    KeyPrefix = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: false),
                    IsAdmin = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ArchivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUsedIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AllowedDomainIds = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevokedApiKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApiKeyDomains",
                columns: table => new
                {
                    ApiKeyId = table.Column<string>(type: "nvarchar(36)", nullable: false),
                    DomainId = table.Column<string>(type: "nvarchar(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiKeyDomains", x => new { x.ApiKeyId, x.DomainId });
                    table.ForeignKey(
                        name: "FK_ApiKeyDomains_ApiKeys_ApiKeyId",
                        column: x => x.ApiKeyId,
                        principalTable: "ApiKeys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ApiKeyDomains_Domains_DomainId",
                        column: x => x.DomainId,
                        principalTable: "Domains",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SentEmails",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    MessageId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FromAddress = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    ToAddresses = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CcAddresses = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BccAddresses = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReplyTo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(998)", maxLength: 998, nullable: false),
                    HtmlBody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TextBody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DomainId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    ApiKeyId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SentEmails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SentEmails_Domains_DomainId",
                        column: x => x.DomainId,
                        principalTable: "Domains",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeyDomains_DomainId",
                table: "ApiKeyDomains",
                column: "DomainId");

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_KeyPrefix",
                table: "ApiKeys",
                column: "KeyPrefix");

            migrationBuilder.CreateIndex(
                name: "IX_Domains_Name",
                table: "Domains",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RevokedApiKeys_KeyPrefix",
                table: "RevokedApiKeys",
                column: "KeyPrefix");

            migrationBuilder.CreateIndex(
                name: "IX_RevokedApiKeys_RevokedAt",
                table: "RevokedApiKeys",
                column: "RevokedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SentEmails_DomainId_SentAt",
                table: "SentEmails",
                columns: new[] { "DomainId", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SentEmails_MessageId",
                table: "SentEmails",
                column: "MessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SentEmails_SentAt",
                table: "SentEmails",
                column: "SentAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiKeyDomains");

            migrationBuilder.DropTable(
                name: "RevokedApiKeys");

            migrationBuilder.DropTable(
                name: "SentEmails");

            migrationBuilder.DropTable(
                name: "ApiKeys");

            migrationBuilder.DropTable(
                name: "Domains");
        }
    }
}
