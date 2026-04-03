using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amnezia.Panel.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClientLifecycleData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AllowedIps",
                table: "vpn_clients",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Config",
                table: "vpn_clients",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                table: "vpn_clients",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PresharedKey",
                table: "vpn_clients",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrivateKey",
                table: "vpn_clients",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QrCodeDataUri",
                table: "vpn_clients",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RevokedAt",
                table: "vpn_clients",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "TrafficLimitBytes",
                table: "vpn_clients",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_vpn_clients_ServerId_Address",
                table: "vpn_clients",
                columns: new[] { "ServerId", "Address" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_vpn_clients_ServerId_Address",
                table: "vpn_clients");

            migrationBuilder.DropColumn(
                name: "AllowedIps",
                table: "vpn_clients");

            migrationBuilder.DropColumn(
                name: "Config",
                table: "vpn_clients");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "vpn_clients");

            migrationBuilder.DropColumn(
                name: "PresharedKey",
                table: "vpn_clients");

            migrationBuilder.DropColumn(
                name: "PrivateKey",
                table: "vpn_clients");

            migrationBuilder.DropColumn(
                name: "QrCodeDataUri",
                table: "vpn_clients");

            migrationBuilder.DropColumn(
                name: "RevokedAt",
                table: "vpn_clients");

            migrationBuilder.DropColumn(
                name: "TrafficLimitBytes",
                table: "vpn_clients");
        }
    }
}
