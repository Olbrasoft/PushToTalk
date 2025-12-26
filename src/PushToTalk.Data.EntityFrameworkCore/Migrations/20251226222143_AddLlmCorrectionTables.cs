using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Olbrasoft.PushToTalk.Data.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddLlmCorrectionTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "circuit_breaker_states",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_open = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    opened_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    consecutive_failures = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_circuit_breaker_states", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "emails",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    smtp_server = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    smtp_port = table.Column<int>(type: "integer", nullable: false, defaultValue: 587),
                    use_ssl = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    from_email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    from_password = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    to_email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_emails", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "llm_api_keys",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    key_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    last_used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rate_limit_hit_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    key_created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_llm_api_keys", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "llm_corrections",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    whisper_transcription_id = table.Column<int>(type: "integer", nullable: false),
                    model_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    corrected_text = table.Column<string>(type: "text", nullable: true),
                    duration_ms = table.Column<int>(type: "integer", nullable: false),
                    success = table.Column<bool>(type: "boolean", nullable: false),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_llm_corrections", x => x.id);
                    table.ForeignKey(
                        name: "FK_llm_corrections_whisper_transcriptions_whisper_transcriptio~",
                        column: x => x.whisper_transcription_id,
                        principalTable: "whisper_transcriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                name: "IX_emails_is_active",
                table: "emails",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_llm_api_keys_provider_is_active",
                table: "llm_api_keys",
                columns: new[] { "provider", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_llm_api_keys_provider_key_hash",
                table: "llm_api_keys",
                columns: new[] { "provider", "key_hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_llm_corrections_created_at",
                table: "llm_corrections",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_llm_corrections_provider_success",
                table: "llm_corrections",
                columns: new[] { "provider", "success" });

            migrationBuilder.CreateIndex(
                name: "IX_llm_corrections_whisper_transcription_id",
                table: "llm_corrections",
                column: "whisper_transcription_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "circuit_breaker_states");

            migrationBuilder.DropTable(
                name: "emails");

            migrationBuilder.DropTable(
                name: "llm_api_keys");

            migrationBuilder.DropTable(
                name: "llm_corrections");
        }
    }
}
