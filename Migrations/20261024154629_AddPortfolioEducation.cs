using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Indlela.Migrations
{
    /// <inheritdoc />
    public partial class AddPortfolioEducation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Education",
                table: "PortfolioProfiles",
                type: "nvarchar(600)",
                maxLength: 600,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Education",
                table: "PortfolioProfiles");
        }
    }
}
