using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeploymentNodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NodeType = table.Column<int>(type: "int", nullable: false),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeploymentNodes", x => x.Id);
                    table.CheckConstraint("CK_DeploymentNodes_NodeType", "[NodeType] BETWEEN 1 AND 4");
                    table.ForeignKey(
                        name: "FK_DeploymentNodes_DeploymentNodes_ParentId",
                        column: x => x.ParentId,
                        principalTable: "DeploymentNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Sensors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MacAddress = table.Column<string>(type: "nvarchar(17)", maxLength: 17, nullable: false),
                    FriendlyName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    MeasuredProperty = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ValueKind = table.Column<int>(type: "int", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ExpectedMinimum = table.Column<double>(type: "float", nullable: true),
                    ExpectedMaximum = table.Column<double>(type: "float", nullable: true),
                    DeploymentNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sensors", x => x.Id);
                    table.CheckConstraint("CK_Sensors_BooleanRange", "[ValueKind] <> 3 OR ([ExpectedMinimum] IS NULL AND [ExpectedMaximum] IS NULL)");
                    table.CheckConstraint("CK_Sensors_Category", "[Category] BETWEEN 1 AND 3");
                    table.CheckConstraint("CK_Sensors_ExpectedRange", "([ExpectedMinimum] IS NULL AND [ExpectedMaximum] IS NULL) OR ([ExpectedMinimum] IS NOT NULL AND [ExpectedMaximum] IS NOT NULL AND [ExpectedMinimum] <= [ExpectedMaximum])");
                    table.CheckConstraint("CK_Sensors_ValueKind", "[ValueKind] BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "FK_Sensors_DeploymentNodes_DeploymentNodeId",
                        column: x => x.DeploymentNodeId,
                        principalTable: "DeploymentNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SensorAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SensorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    StoredFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    RelativePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    UploadedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SensorAttachments", x => x.Id);
                    table.CheckConstraint("CK_SensorAttachments_Category", "[Category] BETWEEN 1 AND 3");
                    table.CheckConstraint("CK_SensorAttachments_SizeBytes", "[SizeBytes] > 0");
                    table.ForeignKey(
                        name: "FK_SensorAttachments_Sensors_SensorId",
                        column: x => x.SensorId,
                        principalTable: "Sensors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TelemetryRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SensorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ValueKind = table.Column<int>(type: "int", nullable: false),
                    FloatValue = table.Column<float>(type: "real", nullable: true),
                    IntegerValue = table.Column<int>(type: "int", nullable: true),
                    BooleanValue = table.Column<bool>(type: "bit", nullable: true),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    IsValid = table.Column<bool>(type: "bit", nullable: false),
                    ValidationMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelemetryRecords", x => x.Id);
                    table.CheckConstraint("CK_TelemetryRecords_TypedValue", "([ValueKind] = 1 AND [FloatValue] IS NOT NULL AND [IntegerValue] IS NULL AND [BooleanValue] IS NULL) OR ([ValueKind] = 2 AND [FloatValue] IS NULL AND [IntegerValue] IS NOT NULL AND [BooleanValue] IS NULL) OR ([ValueKind] = 3 AND [FloatValue] IS NULL AND [IntegerValue] IS NULL AND [BooleanValue] IS NOT NULL)");
                    table.CheckConstraint("CK_TelemetryRecords_Validation", "([IsValid] = 1 AND [ValidationMessage] IS NULL) OR ([IsValid] = 0 AND [ValidationMessage] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_TelemetryRecords_Sensors_SensorId",
                        column: x => x.SensorId,
                        principalTable: "Sensors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentNodes_Code",
                table: "DeploymentNodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentNodes_ParentId",
                table: "DeploymentNodes",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_SensorAttachments_RelativePath",
                table: "SensorAttachments",
                column: "RelativePath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SensorAttachments_SensorId_UploadedAtUtc",
                table: "SensorAttachments",
                columns: new[] { "SensorId", "UploadedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SensorAttachments_StoredFileName",
                table: "SensorAttachments",
                column: "StoredFileName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sensors_DeploymentNodeId",
                table: "Sensors",
                column: "DeploymentNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Sensors_MacAddress",
                table: "Sensors",
                column: "MacAddress",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TelemetryRecords_SensorId_RecordedAtUtc",
                table: "TelemetryRecords",
                columns: new[] { "SensorId", "RecordedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SensorAttachments");

            migrationBuilder.DropTable(
                name: "TelemetryRecords");

            migrationBuilder.DropTable(
                name: "Sensors");

            migrationBuilder.DropTable(
                name: "DeploymentNodes");
        }
    }
}
