using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VTSLegalOfficeAI.Migrations
{
    /// <inheritdoc />
    public partial class AddIsDraftRequestToChatMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDraftRequest",
                table: "ChatMessages",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDraftRequest",
                table: "ChatMessages");
        }
    }
}
