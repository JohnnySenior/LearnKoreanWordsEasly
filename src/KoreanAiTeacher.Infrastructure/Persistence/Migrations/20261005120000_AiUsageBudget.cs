using Microsoft.EntityFrameworkCore.Migrations;

namespace KoreanAiTeacher.Infrastructure.Persistence.Migrations;

public sealed partial class AiUsageBudget : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>("EstimatedUsd", "ai_usage", type: "numeric", nullable: true);
        migrationBuilder.AddColumn<DateOnly>("PriceAsOf", "ai_usage", type: "date", nullable: true);
        migrationBuilder.AddColumn<string>("Unit", "ai_usage", type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "tokens");
        migrationBuilder.Sql("UPDATE ai_usage SET \"Unit\" = CASE WHEN \"Feature\" = 'tts' THEN 'characters' ELSE 'audio-ms' END WHERE \"Feature\" IN ('tts','stt')");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("EstimatedUsd", "ai_usage");
        migrationBuilder.DropColumn("PriceAsOf", "ai_usage");
        migrationBuilder.DropColumn("Unit", "ai_usage");
    }
}
