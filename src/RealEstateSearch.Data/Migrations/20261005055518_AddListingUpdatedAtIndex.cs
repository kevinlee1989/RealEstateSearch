using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstateSearch.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddListingUpdatedAtIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Listings_UpdatedAt_Id",
                table: "Listings",
                columns: new[] { "UpdatedAt", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Listings_UpdatedAt_Id",
                table: "Listings");
        }
    }
}
