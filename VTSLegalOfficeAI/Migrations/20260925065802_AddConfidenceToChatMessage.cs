using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VTSLegalOfficeAI.Migrations
{
    /// <inheritdoc />
    public partial class AddConfidenceToChatMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Confidence",
                table: "ChatMessages",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "SREDNJA");

            migrationBuilder.AddColumn<string>(
                name: "ConfidenceNote",
                table: "ChatMessages",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Confidence",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "ConfidenceNote",
                table: "ChatMessages");
        }
    }
}
