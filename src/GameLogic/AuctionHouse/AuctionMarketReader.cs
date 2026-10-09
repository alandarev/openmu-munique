// <copyright file="AuctionMarketReader.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

using MUnique.OpenMU.Persistence;

/// <summary>
/// Answers the reading requests of an <see cref="AuctionHouseMarket"/>: search, goods list, quote and own listings
/// from the index of active listings, the collection from the database. Listings whose duration ended are left out,
/// even before the expiry moved them.
/// </summary>
internal sealed class AuctionMarketReader
{
    /// <summary>
    /// The maximum count of price tiers of a quotation.
    /// </summary>
    internal const int MaximumPriceTiers = 8;

    private readonly AuctionListingIndex _index;
    private readonly Func<DateTime> _now;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuctionMarketReader"/> class.
    /// </summary>
    /// <param name="index">The index of the active listings.</param>
    /// <param name="now">Gets the current point in time (UTC).</param>
    public AuctionMarketReader(AuctionListingIndex index, Func<DateTime> now)
    {
        this._index = index;
        this._now = now;
    }

    /// <summary>
    /// Searches the item listings.
    /// </summary>
    /// <param name="player">The requesting player.</param>
    /// <param name="query">The query.</param>
    /// <param name="pageSize">The page size.</param>
    /// <returns>The requested page.</returns>
    public AuctionSearchPage Search(Player player, AuctionSearchQuery query, byte pageSize)
    {
        var characterId = player.SelectedCharacter!.GetId();
        var now = this._now();
        var matches = this._index.Where(listing => listing.Kind == AuctionHouseListingKind.Item
                                                   && listing.ExpiresAt > now
                                                   && AuctionSearchFilter.Matches(query, listing, player));
        matches.Sort((x, y) =>
        {
            var result = query.Sort switch
            {
                AuctionHouseSortOrder.PriceDescending => y.UnitPrice.CompareTo(x.UnitPrice),
                AuctionHouseSortOrder.EndingSoon => x.ExpiresAt.CompareTo(y.ExpiresAt),
                AuctionHouseSortOrder.Newest => y.CreatedAt.CompareTo(x.CreatedAt),
                _ => x.UnitPrice.CompareTo(y.UnitPrice),
            };

            // Ties are broken by listing time ascending, then by id, so that pages are stable.
            return result != 0 ? result : CompareOldestFirst(x, y);
        });

        var rows = GetPage(matches, query.Page, pageSize)
            .Select(listing => new AuctionListingRow(
                listing.Id,
                listing.UnitPrice,
                GetSecondsLeft(listing, now),
                listing.SellerName,
                AuctionSearchFilter.CanEquipNow(player, listing.Item!),
                listing.SellerCharacterId == characterId,
                listing.Item!))
            .ToList();
        return new AuctionSearchPage(AuctionHouseResult.Success, query.Page, (uint)matches.Count, rows);
    }

    /// <summary>
    /// Gets the goods list: the goods of other sellers, one row per item type and level.
    /// </summary>
    /// <param name="player">The requesting player.</param>
    /// <param name="query">The query.</param>
    /// <param name="pageSize">The page size.</param>
    /// <returns>The requested page.</returns>
    public AuctionGoodsPage GetGoodsList(Player player, AuctionGoodsListQuery query, byte pageSize)
    {
        var characterId = player.SelectedCharacter!.GetId();
        var now = this._now();
        var rows = this._index.Where(listing => listing.Kind == AuctionHouseListingKind.Goods
                                                && listing.ExpiresAt > now
                                                && listing.SellerCharacterId != characterId
                                                && (query.ItemTypes.Count == 0 || query.ItemTypes.Contains(listing.ItemType)))
            .GroupBy(listing => (listing.ItemType, listing.ItemLevel))
            .Select(goods => new AuctionGoodsRow(
                goods.Key.ItemType,
                goods.Key.ItemLevel,
                goods.Min(listing => listing.UnitPrice),
                (uint)Math.Min(uint.MaxValue, goods.Sum(listing => (long)listing.RemainingQuantity)),
                (ushort)Math.Min(ushort.MaxValue, goods.Count())))
            .OrderBy(row => row.ItemType)
            .ThenBy(row => row.Level)
            .ToList();
        return new AuctionGoodsPage(AuctionHouseResult.Success, query.Page, (uint)rows.Count, GetPage(rows, query.Page, pageSize).ToList());
    }

    /// <summary>
    /// Quotes a quantity of goods of other sellers, filled cheapest first.
    /// </summary>
    /// <param name="player">The requesting player.</param>
    /// <param name="query">The query.</param>
    /// <param name="maximumPrice">The maximum price or total.</param>
    /// <returns>The quotation.</returns>
    public AuctionGoodsQuotation Quote(Player player, AuctionGoodsQuoteQuery query, uint maximumPrice)
    {
        var (group, number) = AuctionHouseItemTypes.Split(query.ItemType);
        var candidates = this._index.GetGoodsInFillOrder(group, number, query.Level, player.SelectedCharacter!.GetId(), this._now());
        var fill = AuctionGoodsFill.Create(candidates, query.Quantity);
        var tiers = candidates
            .GroupBy(listing => listing.UnitPrice)
            .OrderBy(tier => tier.Key)
            .Take(MaximumPriceTiers)
            .Select(tier => new AuctionPriceTier(tier.Key, (uint)Math.Min(uint.MaxValue, tier.Sum(listing => (long)listing.RemainingQuantity))))
            .ToList();
        if (fill.Total > maximumPrice)
        {
            return new AuctionGoodsQuotation(AuctionHouseResult.PriceOutOfRange, query.ItemType, query.Level, query.Quantity, fill.Quantity, 0, tiers);
        }

        var result = fill.Quantity >= query.Quantity ? AuctionHouseResult.Success : AuctionHouseResult.InsufficientStock;
        return new AuctionGoodsQuotation(result, query.ItemType, query.Level, query.Quantity, fill.Quantity, (uint)fill.Total, tiers);
    }

    /// <summary>
    /// Gets the active listings of the player whose duration didn't end yet, oldest first.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The own listings.</returns>
    public AuctionOwnListingList GetOwnListings(Player player)
    {
        var characterId = player.SelectedCharacter!.GetId();
        var now = this._now();
        var listings = this._index.Where(listing => listing.SellerCharacterId == characterId && listing.ExpiresAt > now);
        listings.Sort(CompareOldestFirst);
        var rows = listings
            .Take(byte.MaxValue)
            .Select(listing => new AuctionOwnListingRow(
                listing.Id,
                listing.UnitPrice,
                listing.ListedQuantity,
                listing.RemainingQuantity,
                GetSecondsLeft(listing, now),
                listing.Kind,
                listing.ItemType,
                listing.ItemLevel,
                listing.Item))
            .ToList();
        return new AuctionOwnListingList(AuctionHouseResult.Success, rows);
    }

    /// <summary>
    /// Gets a page of the collection of the player, oldest entry first.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="query">The query.</param>
    /// <param name="rules">The rules.</param>
    /// <returns>The requested page.</returns>
    public async ValueTask<AuctionCollectionPage> GetCollectionAsync(Player player, AuctionCollectionQuery query, AuctionHouseRules rules)
    {
        var pageSize = rules.PageSize;
        var characterId = player.SelectedCharacter!.GetId();
        var context = player.PersistenceContext;
        var (total, entries) = await player.RunPersistenceExclusiveAsync(async () =>
        {
            var ids = await context.GetAuctionCollectionEntryIdsAsync(characterId).ConfigureAwait(false);
            var pageIds = GetPage(ids, query.Page, pageSize).ToList();
            var loaded = await context.GetAuctionCollectionEntriesAsync(pageIds).ConfigureAwait(false);
            return (ids.Count, loaded);
        }).ConfigureAwait(false);

        var rows = entries
            .Select(entry => new AuctionCollectionRow(
                entry.GetId(),
                entry.Kind,
                AuctionHouseItemTypes.GetItemType(entry.ItemGroup, entry.ItemNumber),
                entry.ItemLevel,
                (uint)entry.Quantity,
                ClampToUInt(entry.Gross),
                ClampToUInt(entry.Fee),
                ClampToUInt(entry.UncollectedMoney),
                GetRowItem(entry, rules)))
            .ToList();
        return new AuctionCollectionPage(AuctionHouseResult.Success, query.Page, (ushort)Math.Min(total, ushort.MaxValue), rows);
    }

    /// <summary>
    /// Gets the item whose data the collection shows for the entry: the individual item it holds, or for the proceeds
    /// of an item listing the display copy of the sold item; entries created before the copy existed have none.
    /// </summary>
    private static Item? GetRowItem(AuctionCollectionEntry entry, AuctionHouseRules rules)
        => entry.Kind == AuctionHouseCollectionEntryKind.Proceeds
            ? entry.SoldItem?.Items.FirstOrDefault()
            : IsIndividualItem(entry, rules) ? entry.Escrow!.Items.Single() : null;

    /// <summary>
    /// Determines whether the entry holds an individual item, whose data the collection shows: one item which isn't
    /// goods.
    /// </summary>
    private static bool IsIndividualItem(AuctionCollectionEntry entry, AuctionHouseRules rules)
        => entry.Kind != AuctionHouseCollectionEntryKind.Proceeds
           && entry.Escrow?.Items.Count == 1
           && entry.Escrow.Items.Single().Definition is { } definition
           && !rules.IsGoods(definition);

    private static uint ClampToUInt(long value) => (uint)Math.Clamp(value, 0, uint.MaxValue);

    private static uint GetSecondsLeft(AuctionIndexedListing listing, DateTime now)
        => (uint)Math.Clamp((listing.ExpiresAt - now).TotalSeconds, 0, uint.MaxValue);

    private static int CompareOldestFirst(AuctionIndexedListing x, AuctionIndexedListing y)
    {
        var result = x.CreatedAt.CompareTo(y.CreatedAt);
        return result != 0 ? result : x.Id.CompareTo(y.Id);
    }

    private static IEnumerable<T> GetPage<T>(IReadOnlyList<T> items, ushort page, byte pageSize)
    {
        var size = Math.Max(1, (int)pageSize);
        return items.Skip(page * size).Take(size);
    }
}
