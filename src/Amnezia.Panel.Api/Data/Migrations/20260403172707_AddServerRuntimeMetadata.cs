using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amnezia.Panel.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddServerRuntimeMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AwgParametersJson",
                table: "servers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConfigPath",
                table: "servers",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InterfaceName",
                table: "servers",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PresharedKey",
                table: "servers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QuickCommand",
                table: "servers",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ServerPublicKey",
                table: "servers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShowCommand",
                table: "servers",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VpnSubnet",
                table: "servers",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "NetworkRxBytes",
                table: "server_metrics",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "NetworkTxBytes",
                table: "server_metrics",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AwgParametersJson",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "ConfigPath",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "InterfaceName",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "PresharedKey",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "QuickCommand",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "ServerPublicKey",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "ShowCommand",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "VpnSubnet",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "NetworkRxBytes",
                table: "server_metrics");

            migrationBuilder.DropColumn(
                name: "NetworkTxBytes",
                table: "server_metrics");
        }
    }
}
