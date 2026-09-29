using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Order.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderProcessing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ConfirmedAtUtc",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                table: "Orders",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "Orders",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextAttemptAtUtc",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "Orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ConfirmedQuantity",
                table: "OrderItems",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Status_NextAttemptAtUtc",
                table: "Orders",
                columns: new[] { "Status", "NextAttemptAtUtc" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrderItems_ConfirmedQuantity",
                table: "OrderItems",
                sql: "\"ConfirmedQuantity\" IS NULL OR (\"ConfirmedQuantity\" >= 0 AND \"ConfirmedQuantity\" <= \"Quantity\")");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_Status_NextAttemptAtUtc",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OrderItems_ConfirmedQuantity",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "ConfirmedAtUtc",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "FailureReason",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "LastError",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "NextAttemptAtUtc",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ConfirmedQuantity",
                table: "OrderItems");
        }
    }
}
