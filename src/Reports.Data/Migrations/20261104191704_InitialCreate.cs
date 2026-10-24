using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reports.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JournalEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GeneratedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodFrom = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodTo = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SignerName = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    OperatorFilter = table.Column<string>(type: "TEXT", nullable: false),
                    SettlementFilter = table.Column<string>(type: "TEXT", nullable: false),
                    Order = table.Column<string>(type: "TEXT", nullable: false),
                    MissionCount = table.Column<int>(type: "INTEGER", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MunitionTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ShortName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    FullName = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MunitionTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "People",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 320, nullable: false),
                    FullName = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Rank = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Position = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_People", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Settlements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Region = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ShortName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    FullName = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settlements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TargetTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ShortName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    FullName = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TargetTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UavTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ShortName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    FullName = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UavTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Missions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SourceRowKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    OperatorEmail = table.Column<string>(type: "TEXT", maxLength: 320, nullable: false),
                    SettlementId = table.Column<int>(type: "INTEGER", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Missions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Missions_Settlements_SettlementId",
                        column: x => x.SettlementId,
                        principalTable: "Settlements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TargetEngagements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MissionId = table.Column<int>(type: "INTEGER", nullable: false),
                    UavTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    TargetTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    UavCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TargetEngagements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TargetEngagements_Missions_MissionId",
                        column: x => x.MissionId,
                        principalTable: "Missions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TargetEngagements_TargetTypes_TargetTypeId",
                        column: x => x.TargetTypeId,
                        principalTable: "TargetTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TargetEngagements_UavTypes_UavTypeId",
                        column: x => x.UavTypeId,
                        principalTable: "UavTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MunitionUses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TargetEngagementId = table.Column<int>(type: "INTEGER", nullable: false),
                    MunitionTypeId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MunitionUses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MunitionUses_MunitionTypes_MunitionTypeId",
                        column: x => x.MunitionTypeId,
                        principalTable: "MunitionTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MunitionUses_TargetEngagements_TargetEngagementId",
                        column: x => x.TargetEngagementId,
                        principalTable: "TargetEngagements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Missions_Date",
                table: "Missions",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_Missions_SettlementId",
                table: "Missions",
                column: "SettlementId");

            migrationBuilder.CreateIndex(
                name: "IX_Missions_SourceRowKey",
                table: "Missions",
                column: "SourceRowKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MunitionTypes_ShortName",
                table: "MunitionTypes",
                column: "ShortName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MunitionUses_MunitionTypeId",
                table: "MunitionUses",
                column: "MunitionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_MunitionUses_TargetEngagementId",
                table: "MunitionUses",
                column: "TargetEngagementId");

            migrationBuilder.CreateIndex(
                name: "IX_People_Email",
                table: "People",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Settlements_ShortName",
                table: "Settlements",
                column: "ShortName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TargetEngagements_MissionId",
                table: "TargetEngagements",
                column: "MissionId");

            migrationBuilder.CreateIndex(
                name: "IX_TargetEngagements_TargetTypeId",
                table: "TargetEngagements",
                column: "TargetTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_TargetEngagements_UavTypeId",
                table: "TargetEngagements",
                column: "UavTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_TargetTypes_ShortName",
                table: "TargetTypes",
                column: "ShortName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UavTypes_ShortName",
                table: "UavTypes",
                column: "ShortName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JournalEntries");

            migrationBuilder.DropTable(
                name: "MunitionUses");

            migrationBuilder.DropTable(
                name: "People");

            migrationBuilder.DropTable(
                name: "MunitionTypes");

            migrationBuilder.DropTable(
                name: "TargetEngagements");

            migrationBuilder.DropTable(
                name: "Missions");

            migrationBuilder.DropTable(
                name: "TargetTypes");

            migrationBuilder.DropTable(
                name: "UavTypes");

            migrationBuilder.DropTable(
                name: "Settlements");
        }
    }
}
