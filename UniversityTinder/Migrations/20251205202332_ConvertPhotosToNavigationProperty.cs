using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityTinder.Migrations
{
    /// <inheritdoc />
    public partial class ConvertPhotosToNavigationProperty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Photos_UserProfiles_ProfileId",
                table: "Photos");

            migrationBuilder.DropColumn(
                name: "PhotosList",
                table: "UserProfiles");

            migrationBuilder.AddForeignKey(
                name: "FK_Photos_UserProfiles_ProfileId",
                table: "Photos",
                column: "ProfileId",
                principalTable: "UserProfiles",
                principalColumn: "ProfileId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Photos_UserProfiles_ProfileId",
                table: "Photos");

            migrationBuilder.AddColumn<string>(
                name: "PhotosList",
                table: "UserProfiles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Photos_UserProfiles_ProfileId",
                table: "Photos",
                column: "ProfileId",
                principalTable: "UserProfiles",
                principalColumn: "ProfileId");
        }
    }
}
