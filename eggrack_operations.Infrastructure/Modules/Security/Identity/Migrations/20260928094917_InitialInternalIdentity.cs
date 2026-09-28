using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eggrack_operations.Infrastructure.Modules.Security.Identity.Migrations
{
    /// <inheritdoc />
    public partial class InitialInternalIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "eggrack_identity_role",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true, collation: "utf8mb4_general_ci"),
                    NormalizedName = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true, collation: "utf8mb4_general_ci"),
                    ConcurrencyStamp = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eggrack_identity_role", x => x.Id);
                })
                .Annotation("Relational:Collation", "utf8mb4_general_ci");

            migrationBuilder.CreateTable(
                name: "eggrack_identity_user",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    StaffRef = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false, collation: "utf8mb4_general_ci"),
                    DisplayName = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false, collation: "utf8mb4_general_ci"),
                    MustEnableTwoFactor = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UserName = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true, collation: "utf8mb4_general_ci"),
                    NormalizedUserName = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true, collation: "utf8mb4_general_ci"),
                    Email = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true, collation: "utf8mb4_general_ci"),
                    NormalizedEmail = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true, collation: "utf8mb4_general_ci"),
                    EmailConfirmed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    PasswordHash = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_general_ci"),
                    SecurityStamp = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_general_ci"),
                    ConcurrencyStamp = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_general_ci"),
                    PhoneNumber = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_general_ci"),
                    PhoneNumberConfirmed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eggrack_identity_user", x => x.Id);
                })
                .Annotation("Relational:Collation", "utf8mb4_general_ci");

            migrationBuilder.CreateTable(
                name: "eggrack_identity_role_claim",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    RoleId = table.Column<long>(type: "bigint", nullable: false),
                    ClaimType = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_general_ci"),
                    ClaimValue = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eggrack_identity_role_claim", x => x.Id);
                    table.ForeignKey(
                        name: "FK_eggrack_identity_role_claim_eggrack_identity_role_RoleId",
                        column: x => x.RoleId,
                        principalTable: "eggrack_identity_role",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("Relational:Collation", "utf8mb4_general_ci");

            migrationBuilder.CreateTable(
                name: "eggrack_identity_user_claim",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    ClaimType = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_general_ci"),
                    ClaimValue = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eggrack_identity_user_claim", x => x.Id);
                    table.ForeignKey(
                        name: "FK_eggrack_identity_user_claim_eggrack_identity_user_UserId",
                        column: x => x.UserId,
                        principalTable: "eggrack_identity_user",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("Relational:Collation", "utf8mb4_general_ci");

            migrationBuilder.CreateTable(
                name: "eggrack_identity_user_login",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: false, collation: "utf8mb4_general_ci"),
                    ProviderKey = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: false, collation: "utf8mb4_general_ci"),
                    ProviderDisplayName = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_general_ci"),
                    UserId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eggrack_identity_user_login", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_eggrack_identity_user_login_eggrack_identity_user_UserId",
                        column: x => x.UserId,
                        principalTable: "eggrack_identity_user",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("Relational:Collation", "utf8mb4_general_ci");

            migrationBuilder.CreateTable(
                name: "eggrack_identity_user_role",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    RoleId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eggrack_identity_user_role", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_eggrack_identity_user_role_eggrack_identity_role_RoleId",
                        column: x => x.RoleId,
                        principalTable: "eggrack_identity_role",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_eggrack_identity_user_role_eggrack_identity_user_UserId",
                        column: x => x.UserId,
                        principalTable: "eggrack_identity_user",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("Relational:Collation", "utf8mb4_general_ci");

            migrationBuilder.CreateTable(
                name: "eggrack_identity_user_token",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    LoginProvider = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: false, collation: "utf8mb4_general_ci"),
                    Name = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: false, collation: "utf8mb4_general_ci"),
                    Value = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eggrack_identity_user_token", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_eggrack_identity_user_token_eggrack_identity_user_UserId",
                        column: x => x.UserId,
                        principalTable: "eggrack_identity_user",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("Relational:Collation", "utf8mb4_general_ci");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "eggrack_identity_role",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_eggrack_identity_role_claim_RoleId",
                table: "eggrack_identity_role_claim",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "eggrack_identity_user",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_eggrack_identity_user_StaffRef",
                table: "eggrack_identity_user",
                column: "StaffRef",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "eggrack_identity_user",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_eggrack_identity_user_claim_UserId",
                table: "eggrack_identity_user_claim",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_eggrack_identity_user_login_UserId",
                table: "eggrack_identity_user_login",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_eggrack_identity_user_role_RoleId",
                table: "eggrack_identity_user_role",
                column: "RoleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "eggrack_identity_role_claim");

            migrationBuilder.DropTable(
                name: "eggrack_identity_user_claim");

            migrationBuilder.DropTable(
                name: "eggrack_identity_user_login");

            migrationBuilder.DropTable(
                name: "eggrack_identity_user_role");

            migrationBuilder.DropTable(
                name: "eggrack_identity_user_token");

            migrationBuilder.DropTable(
                name: "eggrack_identity_role");

            migrationBuilder.DropTable(
                name: "eggrack_identity_user");
        }
    }
}
