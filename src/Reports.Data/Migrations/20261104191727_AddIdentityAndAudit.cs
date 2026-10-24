using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reports.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentityAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Login = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    LoginNormalized = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Role = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    IsBlocked = table.Column<bool>(type: "INTEGER", nullable: false),
                    PasswordRestrictionsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    FailedAttempts = table.Column<int>(type: "INTEGER", nullable: false),
                    LockedUntilUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    MustChangePassword = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsBuiltIn = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EventUuid = table.Column<Guid>(type: "TEXT", nullable: false),
                    OccurredAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    ActorAccountId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ActorLogin = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ActionType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    TargetType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    TargetId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Outcome = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Details = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    OsUser = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    MachineName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    PrevHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Hash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_LoginNormalized",
                table: "Accounts",
                column: "LoginNormalized",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_ActionType",
                table: "AuditEvents",
                column: "ActionType");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_ActorLogin",
                table: "AuditEvents",
                column: "ActorLogin");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_EventUuid",
                table: "AuditEvents",
                column: "EventUuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_OccurredAtUtc",
                table: "AuditEvents",
                column: "OccurredAtUtc");

            // Append-only guarantees of the audit log (SEC-09). Raw SQL, not part of the EF model.
            migrationBuilder.Sql(@"CREATE TABLE ""AuditRetentionGate"" (""CutoffUtcTicks"" INTEGER NOT NULL);");
            migrationBuilder.Sql(@"CREATE TRIGGER ""AuditEvents_NoUpdate"" BEFORE UPDATE ON ""AuditEvents""
BEGIN SELECT RAISE(ABORT, 'AuditEvents is append-only'); END;");
            migrationBuilder.Sql(@"CREATE TRIGGER ""AuditEvents_RetentionOnlyDelete"" BEFORE DELETE ON ""AuditEvents""
WHEN OLD.""OccurredAtUtc"" >= COALESCE((SELECT MAX(""CutoffUtcTicks"") FROM ""AuditRetentionGate""), 0)
BEGIN SELECT RAISE(ABORT, 'AuditEvents can be deleted only by retention'); END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP TRIGGER ""AuditEvents_RetentionOnlyDelete"";");
            migrationBuilder.Sql(@"DROP TRIGGER ""AuditEvents_NoUpdate"";");
            migrationBuilder.Sql(@"DROP TABLE ""AuditRetentionGate"";");

            migrationBuilder.DropTable(
                name: "Accounts");

            migrationBuilder.DropTable(
                name: "AuditEvents");
        }
    }
}
