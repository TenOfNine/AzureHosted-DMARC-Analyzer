using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DmarcAnalyzer.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Domains",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DomainName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Domains", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GraphConnectionSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientSecretKeyVaultName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ConfiguredUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastValidatedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastValidationSucceeded = table.Column<bool>(type: "bit", nullable: false),
                    LastValidationError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GraphConnectionSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Mailboxes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MailboxUpn = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MailFolder = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastPolledUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastPollStatus = table.Column<int>(type: "int", nullable: false),
                    LastPollError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DeltaLink = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mailboxes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RetentionSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    RetentionDays = table.Column<int>(type: "int", nullable: false),
                    LastPurgeRunUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastPurgeRowsDeleted = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetentionSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VerifiedSenderOverrides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DomainId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceIpCidr = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    OrgNamePattern = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Label = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VerifiedSenderOverrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VerifiedSenderOverrides_Domains_DomainId",
                        column: x => x.DomainId,
                        principalTable: "Domains",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AggregateReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DomainId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MailboxId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GraphMessageId = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    ReportId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    OrgName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    ExtraContactInfo = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    DateRangeBeginUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateRangeEndUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PolicyPublishedDomain = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    PolicyAdkim = table.Column<int>(type: "int", nullable: false),
                    PolicyAspf = table.Column<int>(type: "int", nullable: false),
                    PolicyP = table.Column<int>(type: "int", nullable: false),
                    PolicySp = table.Column<int>(type: "int", nullable: true),
                    PolicyPct = table.Column<int>(type: "int", nullable: false),
                    PolicyFo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ReceivedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AggregateReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AggregateReports_Domains_DomainId",
                        column: x => x.DomainId,
                        principalTable: "Domains",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AggregateReports_Mailboxes_MailboxId",
                        column: x => x.MailboxId,
                        principalTable: "Mailboxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DomainMailboxes",
                columns: table => new
                {
                    DomainId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MailboxId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DomainMailboxes", x => new { x.DomainId, x.MailboxId });
                    table.ForeignKey(
                        name: "FK_DomainMailboxes_Domains_DomainId",
                        column: x => x.DomainId,
                        principalTable: "Domains",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DomainMailboxes_Mailboxes_MailboxId",
                        column: x => x.MailboxId,
                        principalTable: "Mailboxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProcessedMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MailboxId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GraphMessageId = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    ProcessedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AttachmentType = table.Column<int>(type: "int", nullable: false),
                    FailureReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcessedMessages_Mailboxes_MailboxId",
                        column: x => x.MailboxId,
                        principalTable: "Mailboxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AggregateReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false),
                    PolicyEvaluatedDisposition = table.Column<int>(type: "int", nullable: false),
                    PolicyEvaluatedDkim = table.Column<int>(type: "int", nullable: false),
                    PolicyEvaluatedSpf = table.Column<int>(type: "int", nullable: false),
                    PolicyOverrideReasons = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    HeaderFrom = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    EnvelopeFrom = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    EnvelopeTo = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Records_AggregateReports_AggregateReportId",
                        column: x => x.AggregateReportId,
                        principalTable: "AggregateReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DkimAuthResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DmarcRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Domain = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Selector = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Result = table.Column<int>(type: "int", nullable: false),
                    HumanResult = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DkimAuthResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DkimAuthResults_Records_DmarcRecordId",
                        column: x => x.DmarcRecordId,
                        principalTable: "Records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SpfAuthResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DmarcRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Domain = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    Result = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpfAuthResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpfAuthResults_Records_DmarcRecordId",
                        column: x => x.DmarcRecordId,
                        principalTable: "Records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SpfEvaluationResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DmarcRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvaluatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LiveSpfRecordText = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RecomputedResult = table.Column<int>(type: "int", nullable: false),
                    MatchedMechanism = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LookupCount = table.Column<int>(type: "int", nullable: false),
                    DiscrepancyFlag = table.Column<bool>(type: "bit", nullable: false),
                    DiscrepancyNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpfEvaluationResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpfEvaluationResults_Records_DmarcRecordId",
                        column: x => x.DmarcRecordId,
                        principalTable: "Records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DkimSelectorChecks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DkimAuthResultId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CheckedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RawTxtRecord = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    StaleFlag = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DkimSelectorChecks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DkimSelectorChecks_DkimAuthResults_DkimAuthResultId",
                        column: x => x.DkimAuthResultId,
                        principalTable: "DkimAuthResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AggregateReports_DomainId_DateRangeBeginUtc",
                table: "AggregateReports",
                columns: new[] { "DomainId", "DateRangeBeginUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AggregateReports_MailboxId",
                table: "AggregateReports",
                column: "MailboxId");

            migrationBuilder.CreateIndex(
                name: "IX_DkimAuthResults_DmarcRecordId",
                table: "DkimAuthResults",
                column: "DmarcRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_DkimSelectorChecks_DkimAuthResultId",
                table: "DkimSelectorChecks",
                column: "DkimAuthResultId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DomainMailboxes_MailboxId",
                table: "DomainMailboxes",
                column: "MailboxId");

            migrationBuilder.CreateIndex(
                name: "IX_Domains_DomainName",
                table: "Domains",
                column: "DomainName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Mailboxes_MailboxUpn",
                table: "Mailboxes",
                column: "MailboxUpn",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedMessages_MailboxId_GraphMessageId",
                table: "ProcessedMessages",
                columns: new[] { "MailboxId", "GraphMessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Records_AggregateReportId",
                table: "Records",
                column: "AggregateReportId");

            migrationBuilder.CreateIndex(
                name: "IX_Records_SourceIp",
                table: "Records",
                column: "SourceIp");

            migrationBuilder.CreateIndex(
                name: "IX_SpfAuthResults_DmarcRecordId",
                table: "SpfAuthResults",
                column: "DmarcRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_SpfEvaluationResults_DmarcRecordId",
                table: "SpfEvaluationResults",
                column: "DmarcRecordId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VerifiedSenderOverrides_DomainId",
                table: "VerifiedSenderOverrides",
                column: "DomainId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DkimSelectorChecks");

            migrationBuilder.DropTable(
                name: "DomainMailboxes");

            migrationBuilder.DropTable(
                name: "GraphConnectionSettings");

            migrationBuilder.DropTable(
                name: "ProcessedMessages");

            migrationBuilder.DropTable(
                name: "RetentionSettings");

            migrationBuilder.DropTable(
                name: "SpfAuthResults");

            migrationBuilder.DropTable(
                name: "SpfEvaluationResults");

            migrationBuilder.DropTable(
                name: "VerifiedSenderOverrides");

            migrationBuilder.DropTable(
                name: "DkimAuthResults");

            migrationBuilder.DropTable(
                name: "Records");

            migrationBuilder.DropTable(
                name: "AggregateReports");

            migrationBuilder.DropTable(
                name: "Domains");

            migrationBuilder.DropTable(
                name: "Mailboxes");
        }
    }
}
