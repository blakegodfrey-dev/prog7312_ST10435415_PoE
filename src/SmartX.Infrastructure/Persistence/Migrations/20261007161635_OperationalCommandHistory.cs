using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OperationalCommandHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommandHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SensorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DesiredState = table.Column<bool>(type: "bit", nullable: false),
                    PreviousState = table.Column<bool>(type: "bit", nullable: true),
                    Successful = table.Column<bool>(type: "bit", nullable: false),
                    IsUndo = table.Column<bool>(type: "bit", nullable: false),
                    UndoOfId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Undone = table.Column<bool>(type: "bit", nullable: false),
                    AtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Context = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    TelemetryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommandHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommandHistory_Sensors_SensorId",
                        column: x => x.SensorId,
                        principalTable: "Sensors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GatewayReceipts",
                columns: table => new
                {
                    SensorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastSeenAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GatewayReceipts", x => x.SensorId);
                    table.ForeignKey(
                        name: "FK_GatewayReceipts_Sensors_SensorId",
                        column: x => x.SensorId,
                        principalTable: "Sensors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Interactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Query = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Context = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    AtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Interactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Interactions_Sensors_TargetId",
                        column: x => x.TargetId,
                        principalTable: "Sensors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommandHistory_AtUtc",
                table: "CommandHistory",
                column: "AtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CommandHistory_SensorId",
                table: "CommandHistory",
                column: "SensorId");

            migrationBuilder.CreateIndex(
                name: "IX_CommandHistory_Sequence",
                table: "CommandHistory",
                column: "Sequence",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Interactions_AtUtc",
                table: "Interactions",
                column: "AtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Interactions_TargetId",
                table: "Interactions",
                column: "TargetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommandHistory");

            migrationBuilder.DropTable(
                name: "GatewayReceipts");

            migrationBuilder.DropTable(
                name: "Interactions");
        }
    }
}
