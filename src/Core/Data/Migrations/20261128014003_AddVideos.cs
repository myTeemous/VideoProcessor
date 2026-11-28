using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoProcessor.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVideos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Videos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    DeclaredSize = table.Column<long>(type: "bigint", nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: true),
                    BlobName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RejectionReason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Metadata_Container = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Metadata_Duration = table.Column<TimeSpan>(type: "interval", nullable: true),
                    Metadata_Width = table.Column<int>(type: "integer", nullable: true),
                    Metadata_Height = table.Column<int>(type: "integer", nullable: true),
                    Metadata_VideoCodec = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Metadata_AudioCodec = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Metadata_Bitrate = table.Column<long>(type: "bigint", nullable: true),
                    Metadata_FrameRate = table.Column<double>(type: "double precision", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Videos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Videos_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Videos_BlobName",
                table: "Videos",
                column: "BlobName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Videos_UserId_CreatedAt",
                table: "Videos",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Videos");
        }
    }
}
