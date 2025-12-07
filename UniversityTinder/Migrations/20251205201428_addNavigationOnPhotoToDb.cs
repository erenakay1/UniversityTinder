using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityTinder.Migrations
{
    /// <inheritdoc />
    public partial class addNavigationOnPhotoToDb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Photos_ProfileId",
                table: "Photos",
                column: "ProfileId");

            migrationBuilder.AddForeignKey(
                name: "FK_Photos_UserProfiles_ProfileId",
                table: "Photos",
                column: "ProfileId",
                principalTable: "UserProfiles",
                principalColumn: "ProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Photos_UserProfiles_ProfileId",
                table: "Photos");

            migrationBuilder.DropIndex(
                name: "IX_Photos_ProfileId",
                table: "Photos");
        }
    }
}
