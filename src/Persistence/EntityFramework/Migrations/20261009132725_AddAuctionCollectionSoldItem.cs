using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddAuctionCollectionSoldItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SoldItemId",
                schema: "data",
                table: "AuctionCollectionEntry",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuctionCollectionEntry_SoldItemId",
                schema: "data",
                table: "AuctionCollectionEntry",
                column: "SoldItemId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AuctionCollectionEntry_ItemStorage_SoldItemId",
                schema: "data",
                table: "AuctionCollectionEntry",
                column: "SoldItemId",
                principalSchema: "data",
                principalTable: "ItemStorage",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // The display copies of sold items are principals of their entries as well, so the trigger which deletes
            // the escrow storages of a character before it is deleted deletes them, too.
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION data.""DeleteAuctionHouseEscrowOfCharacter""() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    DELETE FROM data.""ItemStorage"" WHERE ""Id"" IN (
        SELECT ""EscrowId"" FROM data.""AuctionListing"" WHERE ""SellerCharacterId"" = OLD.""Id""
        UNION
        SELECT ""EscrowId"" FROM data.""AuctionCollectionEntry"" WHERE ""OwnerCharacterId"" = OLD.""Id""
        UNION
        SELECT ""SoldItemId"" FROM data.""AuctionCollectionEntry"" WHERE ""OwnerCharacterId"" = OLD.""Id"");
    RETURN OLD;
END
$$;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION data.""DeleteAuctionHouseEscrowOfCharacter""() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    DELETE FROM data.""ItemStorage"" WHERE ""Id"" IN (
        SELECT ""EscrowId"" FROM data.""AuctionListing"" WHERE ""SellerCharacterId"" = OLD.""Id""
        UNION
        SELECT ""EscrowId"" FROM data.""AuctionCollectionEntry"" WHERE ""OwnerCharacterId"" = OLD.""Id"");
    RETURN OLD;
END
$$;");

            // Without the column, the display copies and their items would be orphans, so they are deleted. Their
            // foreign key cascades to the entries, so it is dropped first; otherwise deleting a copy deletes its entry.
            migrationBuilder.DropForeignKey(
                name: "FK_AuctionCollectionEntry_ItemStorage_SoldItemId",
                schema: "data",
                table: "AuctionCollectionEntry");

            migrationBuilder.Sql(@"
DELETE FROM data.""ItemStorage"" WHERE ""Id"" IN (
    SELECT ""SoldItemId"" FROM data.""AuctionCollectionEntry"" WHERE ""SoldItemId"" IS NOT NULL);");

            migrationBuilder.DropIndex(
                name: "IX_AuctionCollectionEntry_SoldItemId",
                schema: "data",
                table: "AuctionCollectionEntry");

            migrationBuilder.DropColumn(
                name: "SoldItemId",
                schema: "data",
                table: "AuctionCollectionEntry");
        }
    }
}
