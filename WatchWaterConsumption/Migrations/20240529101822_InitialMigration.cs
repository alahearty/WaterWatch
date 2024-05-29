using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace WatchWaterConsumption.Migrations
{
    /// <inheritdoc />
    public partial class InitialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Consumptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Neighbourhood = table.Column<string>(type: "text", nullable: true),
                    SuburbGroup = table.Column<string>(type: "text", nullable: true),
                    AverageMonthlyKL = table.Column<int>(type: "integer", nullable: true),
                    Stroke = table.Column<string>(type: "text", nullable: true),
                    StrokeWidth = table.Column<string>(type: "text", nullable: true),
                    StrokeOpacity = table.Column<string>(type: "text", nullable: true),
                    Fill = table.Column<string>(type: "text", nullable: true),
                    FillOpacity = table.Column<string>(type: "text", nullable: true),
                    Coordinates = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Consumptions", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Consumptions");
        }
    }
}
