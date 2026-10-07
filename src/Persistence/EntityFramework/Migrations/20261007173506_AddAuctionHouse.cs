using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddAuctionHouse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuctionCollectionEntry",
                schema: "data",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EscrowId = table.Column<Guid>(type: "uuid", nullable: true),
                    OwnerCharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<byte>(type: "smallint", nullable: false),
                    ListingId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemGroup = table.Column<byte>(type: "smallint", nullable: false),
                    ItemNumber = table.Column<short>(type: "smallint", nullable: false),
                    ItemLevel = table.Column<byte>(type: "smallint", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Gross = table.Column<long>(type: "bigint", nullable: false),
                    Fee = table.Column<long>(type: "bigint", nullable: false),
                    UncollectedMoney = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuctionCollectionEntry", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuctionCollectionEntry_Character_OwnerCharacterId",
                        column: x => x.OwnerCharacterId,
                        principalSchema: "data",
                        principalTable: "Character",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AuctionCollectionEntry_ItemStorage_EscrowId",
                        column: x => x.EscrowId,
                        principalSchema: "data",
                        principalTable: "ItemStorage",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuctionListing",
                schema: "data",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EscrowId = table.Column<Guid>(type: "uuid", nullable: true),
                    SellerCharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerName = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Kind = table.Column<byte>(type: "smallint", nullable: false),
                    ItemGroup = table.Column<byte>(type: "smallint", nullable: false),
                    ItemNumber = table.Column<short>(type: "smallint", nullable: false),
                    ItemLevel = table.Column<byte>(type: "smallint", nullable: false),
                    UnitPrice = table.Column<long>(type: "bigint", nullable: false),
                    ListedQuantity = table.Column<int>(type: "integer", nullable: false),
                    RemainingQuantity = table.Column<int>(type: "integer", nullable: false),
                    PostingFee = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    State = table.Column<byte>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuctionListing", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuctionListing_Character_SellerCharacterId",
                        column: x => x.SellerCharacterId,
                        principalSchema: "data",
                        principalTable: "Character",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AuctionListing_ItemStorage_EscrowId",
                        column: x => x.EscrowId,
                        principalSchema: "data",
                        principalTable: "ItemStorage",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuctionSale",
                schema: "data",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SoldAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ListingId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemGroup = table.Column<byte>(type: "smallint", nullable: false),
                    ItemNumber = table.Column<short>(type: "smallint", nullable: false),
                    ItemLevel = table.Column<byte>(type: "smallint", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<long>(type: "bigint", nullable: false),
                    SellerCharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerName = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    BuyerCharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerName = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Gross = table.Column<long>(type: "bigint", nullable: false),
                    Fee = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuctionSale", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuctionCollectionEntry_EscrowId",
                schema: "data",
                table: "AuctionCollectionEntry",
                column: "EscrowId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuctionCollectionEntry_ListingId",
                schema: "data",
                table: "AuctionCollectionEntry",
                column: "ListingId");

            migrationBuilder.CreateIndex(
                name: "IX_AuctionCollectionEntry_OwnerCharacterId",
                schema: "data",
                table: "AuctionCollectionEntry",
                column: "OwnerCharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_AuctionListing_EscrowId",
                schema: "data",
                table: "AuctionListing",
                column: "EscrowId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuctionListing_SellerCharacterId",
                schema: "data",
                table: "AuctionListing",
                column: "SellerCharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_AuctionListing_State_ExpiresAt",
                schema: "data",
                table: "AuctionListing",
                columns: new[] { "State", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuctionSale_ItemGroup_ItemNumber_ItemLevel_SoldAt",
                schema: "data",
                table: "AuctionSale",
                columns: new[] { "ItemGroup", "ItemNumber", "ItemLevel", "SoldAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuctionSale_ListingId",
                schema: "data",
                table: "AuctionSale",
                column: "ListingId");

            // The escrow storages are the principals of the listings and entries, so the cascading foreign keys of the
            // character can't reach them. Deleting them deletes the listings, entries and items by their foreign keys.
            migrationBuilder.Sql(@"
CREATE FUNCTION data.""DeleteAuctionHouseEscrowOfCharacter""() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    DELETE FROM data.""ItemStorage"" WHERE ""Id"" IN (
        SELECT ""EscrowId"" FROM data.""AuctionListing"" WHERE ""SellerCharacterId"" = OLD.""Id""
        UNION
        SELECT ""EscrowId"" FROM data.""AuctionCollectionEntry"" WHERE ""OwnerCharacterId"" = OLD.""Id"");
    RETURN OLD;
END
$$;

CREATE TRIGGER ""DeleteAuctionHouseEscrow"" BEFORE DELETE ON data.""Character""
    FOR EACH ROW EXECUTE FUNCTION data.""DeleteAuctionHouseEscrowOfCharacter""();");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TRIGGER ""DeleteAuctionHouseEscrow"" ON data.""Character"";
DROP FUNCTION data.""DeleteAuctionHouseEscrowOfCharacter""();");

            // The escrow storages belong to the listings and entries; without their tables, they and their items would
            // be orphans. Deleting them cascades to the listings, entries and items.
            migrationBuilder.Sql(@"
DELETE FROM data.""ItemStorage"" WHERE ""Id"" IN (
    SELECT ""EscrowId"" FROM data.""AuctionListing""
    UNION
    SELECT ""EscrowId"" FROM data.""AuctionCollectionEntry"");");

            migrationBuilder.DropTable(
                name: "AuctionCollectionEntry",
                schema: "data");

            migrationBuilder.DropTable(
                name: "AuctionListing",
                schema: "data");

            migrationBuilder.DropTable(
                name: "AuctionSale",
                schema: "data");
        }
    }
}
