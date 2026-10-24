using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Indlela.Migrations
{
    /// <inheritdoc />
    public partial class AddCertificateCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CertificateCode",
                table: "LearnerEnrollments",
                type: "nvarchar(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LearnerEnrollments_CertificateCode",
                table: "LearnerEnrollments",
                column: "CertificateCode",
                unique: true,
                filter: "[CertificateCode] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LearnerEnrollments_CertificateCode",
                table: "LearnerEnrollments");

            migrationBuilder.DropColumn(
                name: "CertificateCode",
                table: "LearnerEnrollments");
        }
    }
}
