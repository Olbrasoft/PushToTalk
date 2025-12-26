using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Olbrasoft.PushToTalk.Data.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class RefactorLlmCorrectionTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "llm_api_keys");

            migrationBuilder.DropIndex(
                name: "IX_llm_corrections_provider_success",
                table: "llm_corrections");

            migrationBuilder.DropIndex(
                name: "IX_circuit_breaker_states_provider",
                table: "circuit_breaker_states");

            migrationBuilder.DropIndex(
                name: "IX_circuit_breaker_states_provider_is_open",
                table: "circuit_breaker_states");

            migrationBuilder.DropColumn(
                name: "error_message",
                table: "llm_corrections");

            migrationBuilder.DropColumn(
                name: "model_name",
                table: "llm_corrections");

            migrationBuilder.DropColumn(
                name: "provider",
                table: "llm_corrections");

            migrationBuilder.DropColumn(
                name: "success",
                table: "llm_corrections");

            migrationBuilder.DropColumn(
                name: "provider",
                table: "circuit_breaker_states");

            migrationBuilder.AlterColumn<string>(
                name: "corrected_text",
                table: "llm_corrections",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "llm_errors",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    whisper_transcription_id = table.Column<int>(type: "integer", nullable: false),
                    error_message = table.Column<string>(type: "text", nullable: false),
                    duration_ms = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_llm_errors", x => x.id);
                    table.ForeignKey(
                        name: "FK_llm_errors_whisper_transcriptions_whisper_transcription_id",
                        column: x => x.whisper_transcription_id,
                        principalTable: "whisper_transcriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mistral_configs",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    api_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "mistral-large-latest"),
                    base_url = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, defaultValue: "https://api.mistral.ai"),
                    timeout_seconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 30),
                    max_tokens = table.Column<int>(type: "integer", nullable: false, defaultValue: 1000),
                    temperature = table.Column<double>(type: "double precision", nullable: false, defaultValue: 0.29999999999999999),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mistral_configs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_llm_errors_created_at",
                table: "llm_errors",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_llm_errors_whisper_transcription_id",
                table: "llm_errors",
                column: "whisper_transcription_id");

            migrationBuilder.CreateIndex(
                name: "IX_mistral_configs_is_active",
                table: "mistral_configs",
                column: "is_active");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "llm_errors");

            migrationBuilder.DropTable(
                name: "mistral_configs");

            migrationBuilder.AlterColumn<string>(
                name: "corrected_text",
                table: "llm_corrections",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "error_message",
                table: "llm_corrections",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "model_name",
                table: "llm_corrections",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "provider",
                table: "llm_corrections",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "success",
                table: "llm_corrections",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "provider",
                table: "circuit_breaker_states",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "llm_api_keys",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    key_created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    key_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    last_used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    rate_limit_hit_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_llm_api_keys", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_llm_corrections_provider_success",
                table: "llm_corrections",
                columns: new[] { "provider", "success" });

            migrationBuilder.CreateIndex(
                name: "IX_circuit_breaker_states_provider",
                table: "circuit_breaker_states",
                column: "provider",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_circuit_breaker_states_provider_is_open",
                table: "circuit_breaker_states",
                columns: new[] { "provider", "is_open" });

            migrationBuilder.CreateIndex(
                name: "IX_llm_api_keys_provider_is_active",
                table: "llm_api_keys",
                columns: new[] { "provider", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_llm_api_keys_provider_key_hash",
                table: "llm_api_keys",
                columns: new[] { "provider", "key_hash" },
                unique: true);
        }
    }
}
