using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RealEstateSearch.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Listings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Neighbourhood = table.Column<string>(type: "text", nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    PropertyType = table.Column<string>(type: "text", nullable: true),
                    RoomType = table.Column<string>(type: "text", nullable: true),
                    Accommodates = table.Column<int>(type: "integer", nullable: true),
                    Bathrooms = table.Column<double>(type: "double precision", nullable: true),
                    Bedrooms = table.Column<int>(type: "integer", nullable: true),
                    Beds = table.Column<int>(type: "integer", nullable: true),
                    PricePerNight = table.Column<decimal>(type: "numeric", nullable: true),
                    MinimumNights = table.Column<int>(type: "integer", nullable: true),
                    Availability365 = table.Column<int>(type: "integer", nullable: true),
                    ReviewScoreRating = table.Column<double>(type: "double precision", nullable: true),
                    LastScraped = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Listings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Listings");
        }
    }
}
