using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityTinder.Migrations
{
    /// <inheritdoc />
    public partial class addEnumTypeToDb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UsersDto");

            migrationBuilder.AddColumn<string>(
                name: "Hobbies",
                table: "UserProfiles",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Hobbies",
                table: "UserProfiles");

            migrationBuilder.CreateTable(
                name: "UsersDto",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Age = table.Column<int>(type: "int", nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Gender = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HasUnreadMessages = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsPassed = table.Column<bool>(type: "bit", nullable: false),
                    IsProfileCreated = table.Column<bool>(type: "bit", nullable: false),
                    IsSuperLike = table.Column<bool>(type: "bit", nullable: false),
                    IsVerified = table.Column<bool>(type: "bit", nullable: false),
                    Lock_Unlock = table.Column<bool>(type: "bit", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProfileImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Roles = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Surname = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UniversityName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserProfileProfileId = table.Column<int>(type: "int", nullable: true),
                    UserProfileProfileId1 = table.Column<int>(type: "int", nullable: true),
                    UserProfileProfileId2 = table.Column<int>(type: "int", nullable: true),
                    UserProfileProfileId3 = table.Column<int>(type: "int", nullable: true),
                    UserProfileProfileId4 = table.Column<int>(type: "int", nullable: true),
                    UserProfileProfileId5 = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsersDto", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UsersDto_UserProfiles_UserProfileProfileId",
                        column: x => x.UserProfileProfileId,
                        principalTable: "UserProfiles",
                        principalColumn: "ProfileId");
                    table.ForeignKey(
                        name: "FK_UsersDto_UserProfiles_UserProfileProfileId1",
                        column: x => x.UserProfileProfileId1,
                        principalTable: "UserProfiles",
                        principalColumn: "ProfileId");
                    table.ForeignKey(
                        name: "FK_UsersDto_UserProfiles_UserProfileProfileId2",
                        column: x => x.UserProfileProfileId2,
                        principalTable: "UserProfiles",
                        principalColumn: "ProfileId");
                    table.ForeignKey(
                        name: "FK_UsersDto_UserProfiles_UserProfileProfileId3",
                        column: x => x.UserProfileProfileId3,
                        principalTable: "UserProfiles",
                        principalColumn: "ProfileId");
                    table.ForeignKey(
                        name: "FK_UsersDto_UserProfiles_UserProfileProfileId4",
                        column: x => x.UserProfileProfileId4,
                        principalTable: "UserProfiles",
                        principalColumn: "ProfileId");
                    table.ForeignKey(
                        name: "FK_UsersDto_UserProfiles_UserProfileProfileId5",
                        column: x => x.UserProfileProfileId5,
                        principalTable: "UserProfiles",
                        principalColumn: "ProfileId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_UsersDto_UserProfileProfileId",
                table: "UsersDto",
                column: "UserProfileProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_UsersDto_UserProfileProfileId1",
                table: "UsersDto",
                column: "UserProfileProfileId1");

            migrationBuilder.CreateIndex(
                name: "IX_UsersDto_UserProfileProfileId2",
                table: "UsersDto",
                column: "UserProfileProfileId2");

            migrationBuilder.CreateIndex(
                name: "IX_UsersDto_UserProfileProfileId3",
                table: "UsersDto",
                column: "UserProfileProfileId3");

            migrationBuilder.CreateIndex(
                name: "IX_UsersDto_UserProfileProfileId4",
                table: "UsersDto",
                column: "UserProfileProfileId4");

            migrationBuilder.CreateIndex(
                name: "IX_UsersDto_UserProfileProfileId5",
                table: "UsersDto",
                column: "UserProfileProfileId5");
        }
    }
}
