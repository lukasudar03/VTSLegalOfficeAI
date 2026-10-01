using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VTSLegalOfficeAI.Migrations
{
    /// <inheritdoc />
    public partial class AddDeadlineToChatMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DeadlineAmount",
                table: "ChatMessages",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeadlineUnit",
                table: "ChatMessages",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeadlineAmount",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "DeadlineUnit",
                table: "ChatMessages");
        }
    }
}
