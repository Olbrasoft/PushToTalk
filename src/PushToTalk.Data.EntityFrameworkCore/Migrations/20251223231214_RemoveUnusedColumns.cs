using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Olbrasoft.PushToTalk.Data.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUnusedColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "language",
                table: "whisper_transcriptions");

            migrationBuilder.DropColumn(
                name: "model_name",
                table: "whisper_transcriptions");

            migrationBuilder.DropColumn(
                name: "source_application",
                table: "whisper_transcriptions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "language",
                table: "whisper_transcriptions",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "model_name",
                table: "whisper_transcriptions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_application",
                table: "whisper_transcriptions",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);
        }
    }
}
