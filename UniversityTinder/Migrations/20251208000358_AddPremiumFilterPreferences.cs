using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityTinder.Migrations
{
    /// <inheritdoc />
    public partial class AddPremiumFilterPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsProfileCreated",
                table: "UsersDto",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PreferredCity",
                table: "UserProfiles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredDepartment",
                table: "UserProfiles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredUniversityDomain",
                table: "UserProfiles",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsProfileCreated",
                table: "UsersDto");

            migrationBuilder.DropColumn(
                name: "PreferredCity",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "PreferredDepartment",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "PreferredUniversityDomain",
                table: "UserProfiles");
        }
    }
}
