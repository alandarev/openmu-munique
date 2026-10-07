// <copyright file="AuctionCollectionClaim.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

using MUnique.OpenMU.GameLogic.Views.Inventory;
using MUnique.OpenMU.Persistence;

/// <summary>
/// Moves collection entries into the inventory of their owner, inside the owner's market transaction: items as far as
/// they fit, Zen up to the inventory Zen limit; the rest stays in the entry. This is the only place where the market
/// puts something into an inventory.
/// </summary>
/// <remarks>
/// Every in-memory change is registered for the restore of a failed transaction. Emptied entries are deleted at
/// <see cref="CompleteAsync"/>, which has to be the last change of the operation before its commit, because a deletion
/// isn't restored in memory.
/// </remarks>
internal sealed class AuctionCollectionClaim
{
    private readonly Player _player;
    private readonly IPersistenceTransaction _transaction;
    private readonly List<AuctionCollectionEntry> _emptiedEntries = [];
    private readonly List<Item> _addedItems = [];
    private bool _moneyChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuctionCollectionClaim"/> class.
    /// </summary>
    /// <param name="player">The owner of the entries, who has a broker open.</param>
    /// <param name="transaction">The market transaction of the owner.</param>
    public AuctionCollectionClaim(Player player, IPersistenceTransaction transaction)
    {
        this._player = player;
        this._transaction = transaction;
    }

    /// <summary>
    /// Gets the count of entries which were claimed completely.
    /// </summary>
    public ushort CompletedEntries => (ushort)this._emptiedEntries.Count;

    /// <summary>
    /// Gets the count of items which were moved into the inventory.
    /// </summary>
    public uint ItemsMoved => (uint)this._addedItems.Count;

    /// <summary>
    /// Gets the collected Zen.
    /// </summary>
    public uint ZenCollected { get; private set; }

    /// <summary>
    /// Gets what kept back a part of the claim.
    /// </summary>
    public AuctionHouseKeptBack KeptBack { get; private set; }

    /// <summary>
    /// Marks the Zen of the player as changed by the operation, so that <see cref="ShowChangesAsync"/> shows it.
    /// </summary>
    public void MarkMoneyChanged() => this._moneyChanged = true;

    /// <summary>
    /// Claims an entry as far as the inventory allows.
    /// </summary>
    /// <param name="entry">The entry, loaded in the owner's transaction.</param>
    /// <returns>The count of items of this entry which were moved into the inventory.</returns>
    public async ValueTask<uint> ClaimAsync(AuctionCollectionEntry entry)
    {
        var inventory = this._player.Inventory ?? throw new InvalidOperationException("The player has no inventory.");
        var wallet = this._player.SelectedCharacter?.Inventory ?? throw new InvalidOperationException("The player has no selected character.");
        uint moved = 0;
        if (entry.Escrow is { } escrow)
        {
            foreach (var item in escrow.Items.OrderBy(i => i.ItemSlot).ToList())
            {
                if (inventory.CheckInvSpace(item) is not { } slot)
                {
                    this.KeptBack |= AuctionHouseKeptBack.InventorySpace;
                    continue;
                }

                var escrowSlot = item.ItemSlot;

                // Registered before the mutation, so that it also repairs a mutation which failed half way: the
                // inventory is cleaned at the target slot and the item returns to the escrow.
                this._transaction.OnRollback(async () =>
                {
                    if (inventory.GetItem(slot) == item)
                    {
                        item.ItemSlot = slot;
                        await inventory.RemoveItemAsync(item).ConfigureAwait(false);
                    }

                    item.ItemSlot = escrowSlot;
                    if (!escrow.Items.Contains(item))
                    {
                        escrow.Items.Add(item);
                    }
                });
                escrow.Items.Remove(item);
                if (!await inventory.AddItemAsync(slot, item).ConfigureAwait(false))
                {
                    item.ItemSlot = escrowSlot;
                    escrow.Items.Add(item);
                    this.KeptBack |= AuctionHouseKeptBack.InventorySpace;
                    continue;
                }

                this._addedItems.Add(item);
                moved++;
            }

            if (moved > 0 && entry.Kind != AuctionHouseCollectionEntryKind.Proceeds)
            {
                var quantity = entry.Quantity;
                entry.Quantity = escrow.Items.Count;
                this._transaction.OnRollback(() => entry.Quantity = quantity);
            }
        }

        if (entry.UncollectedMoney > 0)
        {
            var limit = (long)this._player.GameContext.Configuration.MaximumInventoryMoney;
            var room = Math.Max(0, limit - wallet.Money);
            var zen = Math.Min(entry.UncollectedMoney, room);
            if (zen > 0)
            {
                wallet.Money += (int)zen;
                entry.UncollectedMoney -= zen;
                this._transaction.OnRollback(() =>
                {
                    entry.UncollectedMoney += zen;
                    wallet.Money -= (int)zen;
                });
                this.ZenCollected += (uint)zen;
                this._moneyChanged = true;
            }

            if (entry.UncollectedMoney > 0)
            {
                this.KeptBack |= AuctionHouseKeptBack.ZenLimit;
            }
        }

        if (entry.UncollectedMoney == 0 && (entry.Escrow?.Items.Count ?? 0) == 0)
        {
            this._emptiedEntries.Add(entry);
        }

        return moved;
    }

    /// <summary>
    /// Deletes the entries which were claimed completely; the last change of the operation before its commit.
    /// </summary>
    /// <returns>The task.</returns>
    public async ValueTask CompleteAsync()
    {
        foreach (var entry in this._emptiedEntries)
        {
            await this._player.PersistenceContext.DeleteAsync(entry).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Shows the moved items and the changed Zen to the player; after the commit.
    /// </summary>
    /// <returns>The task.</returns>
    public async ValueTask ShowChangesAsync()
    {
        foreach (var item in this._addedItems)
        {
            await this._player.InvokeViewPlugInAsync<IItemAppearPlugIn>(p => p.ItemAppearAsync(item)).ConfigureAwait(false);
        }

        if (this._moneyChanged)
        {
            await this._player.InvokeViewPlugInAsync<IUpdateMoneyPlugIn>(p => p.UpdateMoneyAsync()).ConfigureAwait(false);
        }
    }
}
