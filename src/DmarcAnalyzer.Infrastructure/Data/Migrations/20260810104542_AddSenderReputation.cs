using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DmarcAnalyzer.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSenderReputation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SenderReputations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DomainId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: false),
                    FirstSeenUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalVolume = table.Column<long>(type: "bigint", nullable: false),
                    AlignedPassVolume = table.Column<long>(type: "bigint", nullable: false),
                    CurrentSpfResult = table.Column<int>(type: "int", nullable: false),
                    CurrentDkimStale = table.Column<bool>(type: "bit", nullable: false),
                    ReverseDnsHostname = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ForwardConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    IsOverrideMatch = table.Column<bool>(type: "bit", nullable: false),
                    LegitimacyLevel = table.Column<int>(type: "int", nullable: false),
                    LastEvaluatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SenderReputations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SenderReputations_Domains_DomainId",
                        column: x => x.DomainId,
                        principalTable: "Domains",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SenderReputations_DomainId_LegitimacyLevel",
                table: "SenderReputations",
                columns: new[] { "DomainId", "LegitimacyLevel" });

            migrationBuilder.CreateIndex(
                name: "IX_SenderReputations_DomainId_SourceIp",
                table: "SenderReputations",
                columns: new[] { "DomainId", "SourceIp" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SenderReputations");
        }
    }
}
