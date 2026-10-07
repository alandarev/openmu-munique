// <copyright file="AuctionSellerDelivery.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using MUnique.OpenMU.GameLogic.Views.AuctionHouse;
using MUnique.OpenMU.Persistence;

/// <summary>
/// Delivers a collection entry which a sale or expiry created for a seller, after it was committed: to an online seller
/// at a broker (any game context which shares the market) by a claim, otherwise as a notice that it waits in the
/// collection. An offline seller gets nothing now; the waiting deliveries are announced when the character enters the
/// game.
/// </summary>
/// <remarks>
/// The delivery runs on a detached task which doesn't inherit the flow of the request or task which created the entry,
/// so that no two players' locks are ever held at once: it takes the seller's locks in the seller's own order, first the
/// persistence lock, then the broker session's request lock, like the seller's own requests.
/// </remarks>
internal sealed class AuctionSellerDelivery
{
    private readonly ConditionalWeakTable<IGameContext, object> _gameContexts = new();
    private readonly ConcurrentDictionary<Task, byte> _pendingDeliveries = new();
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuctionSellerDelivery"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public AuctionSellerDelivery(ILogger logger)
    {
        this._logger = logger;
    }

    /// <summary>
    /// Registers a game context whose players may be sellers of the market.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    public void RegisterGameContext(IGameContext gameContext)
    {
        this._gameContexts.TryAdd(gameContext, gameContext);
    }

    /// <summary>
    /// Starts the delivery of a committed entry on a detached task.
    /// </summary>
    /// <param name="order">The delivery order.</param>
    public void Start(AuctionSellerDeliveryOrder order)
    {
        if (this.FindOnlineCharacter(order.CharacterId, order.CharacterName) is not { } seller)
        {
            return;
        }

        Task task;
        using (ExecutionContext.SuppressFlow())
        {
            task = Task.Run(() => this.DeliverAsync(seller, order));
        }

        this._pendingDeliveries.TryAdd(task, 0);
        _ = task.ContinueWith(t => this._pendingDeliveries.TryRemove(t, out _), TaskScheduler.Default);
    }

    /// <summary>
    /// Waits until all started deliveries completed.
    /// </summary>
    /// <returns>The task.</returns>
    public async Task WaitForPendingDeliveriesAsync()
    {
        while (!this._pendingDeliveries.IsEmpty)
        {
            await Task.WhenAll(this._pendingDeliveries.Keys).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Finds the player of an online character in the registered game contexts.
    /// </summary>
    /// <param name="characterId">The character identifier.</param>
    /// <param name="characterName">The character name.</param>
    /// <returns>The player, if the character is online.</returns>
    public Player? FindOnlineCharacter(Guid characterId, string characterName)
    {
        foreach (var (gameContext, _) in this._gameContexts)
        {
            if (gameContext.GetPlayerByCharacterName(characterName) is { } player
                && player.SelectedCharacter is { } character
                && character.GetId() == characterId)
            {
                return player;
            }
        }

        return null;
    }

    private async Task DeliverAsync(Player seller, AuctionSellerDeliveryOrder order)
    {
        try
        {
            await seller.RunPersistenceExclusiveAsync(async () =>
            {
                using var requestLock = await AuctionBrokerSession.Of(seller).RequestLock.LockAsync().ConfigureAwait(false);
                if (seller.SelectedCharacter is not { } character || character.GetId() != order.CharacterId)
                {
                    // The seller left the game meanwhile; the entry is announced when the character enters again.
                    return;
                }

                var deliveredInFull = false;
                if (AuctionBrokerSession.IsBrokerOpen(seller))
                {
                    try
                    {
                        deliveredInFull = await this.ClaimAsync(seller, order).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        // The claim is rolled back; the entry waits in the collection, which the notice says.
                        this._logger.LogError(ex, "Error while claiming the auction house collection entry {entry} for {seller}.", order.EntryId, order.CharacterName);
                    }
                }

                var pending = await seller.PersistenceContext.GetAuctionCollectionEntryIdsAsync(order.CharacterId).ConfigureAwait(false);
                var notice = new AuctionHouseNotice(
                    deliveredInFull ? order.DeliveredKind : order.WaitingKind,
                    order.ItemType,
                    order.Level,
                    order.Quantity,
                    order.Net,
                    (ushort)Math.Min(pending.Count, ushort.MaxValue));
                await seller.InvokeViewPlugInAsync<IShowAuctionNoticePlugIn>(p => p.ShowAuctionNoticeAsync(notice)).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error while delivering the auction house collection entry {entry} to {seller}.", order.EntryId, order.CharacterName);
        }
    }

    /// <summary>
    /// Claims the entry for the seller at the broker, in the seller's own context.
    /// </summary>
    /// <returns><see langword="true"/>, if the entry was delivered in full.</returns>
    private async ValueTask<bool> ClaimAsync(Player seller, AuctionSellerDeliveryOrder order)
    {
        AuctionCollectionClaim? claim = null;
        var deliveredInFull = await seller.PersistenceContext.RunInTransactionAsync(async transaction =>
        {
            var entry = (await transaction.LockAsync<AuctionCollectionEntry>([order.EntryId]).ConfigureAwait(false)).FirstOrDefault();
            if (entry is null || entry.OwnerCharacterId != order.CharacterId)
            {
                // Claimed meanwhile by the seller.
                return false;
            }

            claim = new AuctionCollectionClaim(seller, transaction);
            await claim.ClaimAsync(entry).ConfigureAwait(false);
            await claim.CompleteAsync().ConfigureAwait(false);
            return claim.CompletedEntries == 1;
        }).ConfigureAwait(false);

        if (claim is not null)
        {
            try
            {
                await claim.ShowChangesAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // The claim is committed; a failing view doesn't change that.
                this._logger.LogError(ex, "Error while showing the claimed auction house entry {entry} to {seller}.", order.EntryId, order.CharacterName);
            }
        }

        return deliveredInFull;
    }
}
