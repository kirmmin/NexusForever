using Microsoft.EntityFrameworkCore.Migrations;

namespace NexusForever.Database.Character.Migrations
{
    public partial class PathMissions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "character_path_episode",
                columns: table => new
                {
                    id = table.Column<ulong>(type: "bigint(20) unsigned", nullable: false, defaultValue: 0ul),
                    episodeId = table.Column<uint>(type: "int(10) unsigned", nullable: false, defaultValue: 0u),
                    rewardReceived = table.Column<byte>(type: "tinyint(3) unsigned", nullable: false, defaultValue: (byte)0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => new { x.id, x.episodeId });
                    table.ForeignKey(
                        name: "FK__character_path_episode_id__character_id",
                        column: x => x.id,
                        principalTable: "character",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "character_path_mission",
                columns: table => new
                {
                    id = table.Column<ulong>(type: "bigint(20) unsigned", nullable: false, defaultValue: 0ul),
                    episodeId = table.Column<uint>(type: "int(10) unsigned", nullable: false, defaultValue: 0u),
                    missionId = table.Column<uint>(type: "int(10) unsigned", nullable: false, defaultValue: 0u),
                    progress = table.Column<uint>(type: "int(10) unsigned", nullable: false, defaultValue: 0u),
                    complete = table.Column<byte>(type: "tinyint(3) unsigned", nullable: false, defaultValue: (byte)0),
                    unlocked = table.Column<byte>(type: "tinyint(3) unsigned", nullable: false, defaultValue: (byte)0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => new { x.id, x.episodeId, x.missionId });
                    table.ForeignKey(
                        name: "FK__character_path_mission_id__character_path_episode_id",
                        columns: x => new { x.id, x.episodeId },
                        principalTable: "character_path_episode",
                        principalColumns: new[] { "id", "episodeId" },
                        onDelete: ReferentialAction.Cascade);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "character_path_mission");

            migrationBuilder.DropTable(
                name: "character_path_episode");
        }
    }
}
