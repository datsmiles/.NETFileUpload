using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FileUploadApi.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovalTime",
                table: "FileRecords",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedBy",
                table: "FileRecords",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "FileRecords",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DocumentId",
                table: "FileRecords",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsApproved",
                table: "FileRecords",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "FileRecords",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "StageOfApproval",
                table: "FileRecords",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StageOfApproval = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsApproved = table.Column<bool>(type: "bit", nullable: false),
                    ApprovedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApprovalTime = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FileRecords_DocumentId",
                table: "FileRecords",
                column: "DocumentId");

            migrationBuilder.AddForeignKey(
                name: "FK_FileRecords_Documents_DocumentId",
                table: "FileRecords",
                column: "DocumentId",
                principalTable: "Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FileRecords_Documents_DocumentId",
                table: "FileRecords");

            migrationBuilder.DropTable(
                name: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_FileRecords_DocumentId",
                table: "FileRecords");

            migrationBuilder.DropColumn(
                name: "ApprovalTime",
                table: "FileRecords");

            migrationBuilder.DropColumn(
                name: "ApprovedBy",
                table: "FileRecords");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "FileRecords");

            migrationBuilder.DropColumn(
                name: "DocumentId",
                table: "FileRecords");

            migrationBuilder.DropColumn(
                name: "IsApproved",
                table: "FileRecords");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "FileRecords");

            migrationBuilder.DropColumn(
                name: "StageOfApproval",
                table: "FileRecords");
        }
    }
}
