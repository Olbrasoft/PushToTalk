using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Olbrasoft.PushToTalk.Data.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddTranscriptionCorrections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "transcription_corrections",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    incorrect_text = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    correct_text = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    case_sensitive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transcription_corrections", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_transcription_corrections_incorrect_text",
                table: "transcription_corrections",
                column: "incorrect_text",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_transcription_corrections_is_active",
                table: "transcription_corrections",
                column: "is_active",
                filter: "is_active = true");

            // Create table for tracking correction usage (analytics)
            migrationBuilder.Sql(@"
                CREATE TABLE transcription_correction_usage (
                    id SERIAL PRIMARY KEY,
                    correction_id INTEGER NOT NULL REFERENCES transcription_corrections(id) ON DELETE CASCADE,
                    applied_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                );

                CREATE INDEX IX_correction_usage_correction_id ON transcription_correction_usage(correction_id);
                CREATE INDEX IX_correction_usage_applied_at ON transcription_correction_usage(applied_at);
            ");

            // Seed initial corrections based on analysis of 3,718 transcriptions
            migrationBuilder.InsertData(
                table: "transcription_corrections",
                columns: new[] { "incorrect_text", "correct_text", "case_sensitive", "priority", "is_active", "notes" },
                values: new object[,]
                {
                    // High priority: compound words (prevent partial matches)
                    { "teďkon", "teď", false, 100, true, "Common speech concatenation error (72 occurrences)" },
                    { "tímhletím", "tímhle", false, 90, true, "Speech artifact - word concatenation" },

                    // Technology names - ASR engine
                    { "vyspru", "Whisper", false, 80, true, "ASR engine name (11 occurrences)" },
                    { "vyspra", "Whisper", false, 80, true, "ASR engine name variant (11 occurrences)" },
                    { "vyspr", "Whisper", false, 80, true, "ASR engine name variant" },

                    // Project names
                    { "pushtu talk", "PushToTalk", false, 85, true, "Project name" },
                    { "puštu talk", "PushToTalk", false, 85, true, "Project name variant" },
                    { "puštěnou", "PushToTalk", false, 75, true, "Project name conjugated" },
                    { "puštu", "Push", false, 70, true, "Push verb/prefix (6 occurrences)" },
                    { "pušne", "push", false, 60, true, "Push verb variant (6 occurrences)" },

                    // Keyboard keys
                    { "kapslok", "Caps Lock", false, 80, true, "Keyboard key (18 occurrences)" },
                    { "kapslou", "Caps Lock", false, 80, true, "Keyboard key variant (7 occurrences)" },
                    { "kapsloku", "Caps Lock", false, 80, true, "Keyboard key variant (3 occurrences)" },

                    // Technology frameworks
                    { "dotnetu", ".NET", false, 90, true, "Technology framework name" },
                    { "dot net", ".NET", false, 85, true, "Technology framework variant" },

                    // Other technical terms
                    { "Trigel", "Trigger", false, 80, true, "Trigger word misspelling" },
                    { "trigel", "trigger", false, 80, true, "Trigger word lowercase" },
                    { "bešový", "Bash", false, 75, true, "Shell type" },
                    { "bešových", "Bash", false, 75, true, "Shell type variant" },

                    // Czech verb corrections
                    { "riktovat", "diktovat", false, 60, true, "Dictation verb correction" },
                    { "riktuje", "diktuje", false, 60, true, "Dictation verb conjugated" },
                    { "zůstaná", "zůstane", false, 50, true, "Common conjugation error" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop usage table first (foreign key constraint)
            migrationBuilder.Sql("DROP TABLE IF EXISTS transcription_correction_usage;");

            migrationBuilder.DropTable(
                name: "transcription_corrections");
        }
    }
}
