using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace IdentityWs.Migrations
{
    public partial class Initial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Beings",
                columns: table => new
                {
                    BeingID = table.Column<int>(nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    DateCreated = table.Column<DateTime>(nullable: false),
                    PasswordResetToken = table.Column<string>(nullable: true),
                    PasswordResetTokenValidUntil = table.Column<DateTime>(nullable: true),
                    SaltedHashedPassword = table.Column<string>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Beings", x => x.BeingID);
                });

            migrationBuilder.CreateTable(
                name: "Aliases",
                columns: table => new
                {
                    AliasID = table.Column<int>(nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    BeingID = table.Column<int>(nullable: false),
                    ConfirmationToken = table.Column<string>(nullable: true),
                    DateConfirmed = table.Column<DateTime>(nullable: true),
                    DateCreated = table.Column<DateTime>(nullable: false),
                    EmailAddress = table.Column<string>(maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Aliases", x => x.AliasID);
                    table.ForeignKey(
                        name: "FK_Aliases_Beings_BeingID",
                        column: x => x.BeingID,
                        principalTable: "Beings",
                        principalColumn: "BeingID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BeingClients",
                columns: table => new
                {
                    BeingClientID = table.Column<int>(nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    BeingID = table.Column<int>(nullable: false),
                    ClientName = table.Column<string>(maxLength: 20, nullable: false),
                    DateCreated = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BeingClients", x => x.BeingClientID);
                    table.ForeignKey(
                        name: "FK_BeingClients_Beings_BeingID",
                        column: x => x.BeingID,
                        principalTable: "Beings",
                        principalColumn: "BeingID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Emails",
                columns: table => new
                {
                    EmailID = table.Column<int>(nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    AliasID = table.Column<int>(nullable: false),
                    BodyHTML = table.Column<string>(nullable: true),
                    BodyText = table.Column<string>(nullable: true),
                    DateCreated = table.Column<DateTime>(nullable: false),
                    DateLastProcessed = table.Column<DateTime>(nullable: true),
                    From = table.Column<string>(maxLength: 100, nullable: false),
                    LastProcessingError = table.Column<string>(nullable: true),
                    ProcessingCount = table.Column<int>(nullable: false),
                    ReplyTo = table.Column<string>(maxLength: 100, nullable: true),
                    SendIfUnconfirmed = table.Column<bool>(nullable: false),
                    Subject = table.Column<string>(maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Emails", x => x.EmailID);
                    table.ForeignKey(
                        name: "FK_Emails_Aliases_AliasID",
                        column: x => x.AliasID,
                        principalTable: "Aliases",
                        principalColumn: "AliasID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LoginAttempts",
                columns: table => new
                {
                    LoginAttemptID = table.Column<int>(nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    AliasID = table.Column<int>(nullable: false),
                    ClientName = table.Column<string>(maxLength: 20, nullable: false),
                    DateCreated = table.Column<DateTime>(nullable: false),
                    Success = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoginAttempts", x => x.LoginAttemptID);
                    table.ForeignKey(
                        name: "FK_LoginAttempts_Aliases_AliasID",
                        column: x => x.AliasID,
                        principalTable: "Aliases",
                        principalColumn: "AliasID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BeingClientData",
                columns: table => new
                {
                    BeingClientDatumID = table.Column<int>(nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    BeingClientID = table.Column<int>(nullable: false),
                    Key = table.Column<string>(nullable: false),
                    Value = table.Column<string>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BeingClientData", x => x.BeingClientDatumID);
                    table.ForeignKey(
                        name: "FK_BeingClientData_BeingClients_BeingClientID",
                        column: x => x.BeingClientID,
                        principalTable: "BeingClients",
                        principalColumn: "BeingClientID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Aliases_BeingID",
                table: "Aliases",
                column: "BeingID");

            migrationBuilder.CreateIndex(
                name: "IX_Aliases_EmailAddress",
                table: "Aliases",
                column: "EmailAddress",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BeingClientData_BeingClientID_Key",
                table: "BeingClientData",
                columns: new[] { "BeingClientID", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BeingClients_BeingID_ClientName",
                table: "BeingClients",
                columns: new[] { "BeingID", "ClientName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Emails_AliasID",
                table: "Emails",
                column: "AliasID");

            migrationBuilder.CreateIndex(
                name: "IX_LoginAttempts_AliasID",
                table: "LoginAttempts",
                column: "AliasID");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BeingClientData");

            migrationBuilder.DropTable(
                name: "Emails");

            migrationBuilder.DropTable(
                name: "LoginAttempts");

            migrationBuilder.DropTable(
                name: "BeingClients");

            migrationBuilder.DropTable(
                name: "Aliases");

            migrationBuilder.DropTable(
                name: "Beings");
        }
    }
}
