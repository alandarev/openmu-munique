// <copyright file="PlayerContext.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework;

using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Persistence.EntityFramework.Model;

/// <summary>
/// Persistence context which is used by in-game players.
/// </summary>
internal class PlayerContext : CachingEntityFrameworkContext, IPlayerContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PlayerContext" /> class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="repositoryProvider">The repository provider.</param>
    /// <param name="logger">The logger.</param>
    public PlayerContext(DbContext context, IContextAwareRepositoryProvider repositoryProvider, ILogger<PlayerContext> logger)
        : base(context, repositoryProvider, null, logger)
    {
    }

    /// <inheritdoc/>
    public async ValueTask<DataModel.Entities.LetterBody?> GetLetterBodyByHeaderIdAsync(Guid headerId, CancellationToken cancellationToken = default)
    {
        using var context = this.RepositoryProvider.ContextStack.UseContext(this);
        if (this.RepositoryProvider.GetRepository<LetterBody, LetterBodyRepository>() is { } repository)
        {
            return await repository.GetBodyByHeaderIdAsync(headerId, cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> CanSaveLetterAsync(Interfaces.LetterHeader letterHeader, CancellationToken cancellationToken = default)
    {
        if (letterHeader is not Model.LetterHeader persistentHeader)
        {
            return false;
        }

        persistentHeader.Receiver = await this.Context.Set<Character>().FirstOrDefaultAsync(c => c.Name == letterHeader.ReceiverName, cancellationToken).ConfigureAwait(false);
        return persistentHeader.Receiver != null;
    }

    /// <inheritdoc />
    public async ValueTask<DataModel.Entities.AccountState?> AuthenticateAsync(string loginName, string password, CancellationToken cancellationToken = default)
    {
        using (this.RepositoryProvider.ContextStack.UseContext(this))
        {
            if (this.RepositoryProvider.GetRepository<Account, AccountRepository>() is { } accountRepository)
            {
                return await accountRepository.AuthenticateAsync(loginName, password, cancellationToken).ConfigureAwait(false);
            }
        }

        return null;
    }

    /// <inheritdoc />
    public async ValueTask<DataModel.Entities.Account?> GetAccountByLoginNameAsync(string loginName, string password, CancellationToken cancellationToken = default)
    {
        using (this.RepositoryProvider.ContextStack.UseContext(this))
        {
            if (this.RepositoryProvider.GetRepository<Account, AccountRepository>() is { } accountRepository)
            {
                return await accountRepository.GetAccountByLoginNameAsync(loginName, password, cancellationToken).ConfigureAwait(false);
            }
        }

        return null;
    }

    /// <inheritdoc />
    public async ValueTask<DataModel.Entities.Account?> GetAccountByLoginNameAsync(string loginName, CancellationToken cancellationToken = default)
    {
        using (this.RepositoryProvider.ContextStack.UseContext(this))
        {
            if (this.RepositoryProvider.GetRepository<Account, AccountRepository>() is { } accountRepository)
            {
                return await accountRepository.GetAccountByLoginNameAsync(loginName, cancellationToken).ConfigureAwait(false);
            }
        }

        return null;
    }

    /// <inheritdoc />
    public async ValueTask<IEnumerable<DataModel.Entities.Account>> GetAccountsOrderedByLoginNameAsync(int skip, int count, CancellationToken cancellationToken = default)
    {
        using (this.RepositoryProvider.ContextStack.UseContext(this))
        {
            return await this.Context.Set<Account>().AsNoTracking().OrderBy(a => a.LoginName).Skip(skip).Take(count).ToListAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask<IEnumerable<DataModel.Entities.Account>> SearchAccountsAsync(string searchTerm, int skip, int count, CancellationToken cancellationToken = default)
    {
        using (this.RepositoryProvider.ContextStack.UseContext(this))
        {
            // Invariant: this one runs in .NET, so it must not depend on the server's locale - in a
            // Turkish one, "I".ToLower() is a dotless "ı" and the term would match nothing. The
            // ToLower() calls inside the query below are a different matter: they are translated to
            // the database's own lower(), which is why they cannot take a culture.
            var term = searchTerm.ToLowerInvariant();
            return await this.Context.Set<Account>().AsNoTracking()
                .Where(a => a.LoginName.ToLower().Contains(term)
                            || a.RawCharacters.Any(c => c.Name.ToLower().Contains(term)))
                .OrderBy(a => a.LoginName)
                .Skip(skip)
                .Take(count)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask<DataModel.Entities.Account?> GetAccountByCharacterNameAsync(string characterName, CancellationToken cancellationToken = default)
    {
        using (this.RepositoryProvider.ContextStack.UseContext(this))
        {
            if (this.RepositoryProvider.GetRepository<Account, AccountRepository>() is { } accountRepository)
            {
                return await accountRepository.GetAccountByCharacterNameAsync(characterName, cancellationToken).ConfigureAwait(false);
            }
        }

        return null;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<DataModel.Entities.CastleSiegePendingReward>> GetPendingCastleSiegeRewardsAsync(
        Guid characterId,
        CancellationToken cancellationToken = default)
    {
        using (this.RepositoryProvider.ContextStack.UseContext(this))
        {
            return await this.Context.Set<CastleSiegePendingReward>()
                .Where(reward => reward.CharacterId == characterId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<DataModel.Entities.AuctionListing>> GetActiveAuctionListingsAsync(CancellationToken cancellationToken = default)
    {
        System.Collections.Generic.List<Guid> ids;
        using (this.RepositoryProvider.ContextStack.UseContext(this))
        {
            ids = await this.Context.Set<AuctionListing>()
                .Where(listing => listing.State == DataModel.Entities.AuctionListingState.Active)
                .OrderBy(listing => listing.Id)
                .Select(listing => listing.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        return await this.LoadAggregatesAsync<DataModel.Entities.AuctionListing>(typeof(AuctionListing), ids, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<DataModel.Entities.AuctionCollectionEntry>> GetAuctionCollectionEntriesAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default)
    {
        return await this.LoadAggregatesAsync<DataModel.Entities.AuctionCollectionEntry>(typeof(AuctionCollectionEntry), ids, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<DataModel.Entities.AuctionListing>> GetAuctionListingsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default)
    {
        return await this.LoadAggregatesAsync<DataModel.Entities.AuctionListing>(typeof(AuctionListing), ids, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<Guid>> GetActiveGoodsListingIdsAsync(byte itemGroup, short itemNumber, byte itemLevel, Guid excludedSellerCharacterId, long maximumUnitPrice, DateTime now, CancellationToken cancellationToken = default)
    {
        using (this.RepositoryProvider.ContextStack.UseContext(this))
        {
            return await this.Context.Set<AuctionListing>()
                .Where(listing => listing.State == DataModel.Entities.AuctionListingState.Active
                                  && listing.Kind == DataModel.Entities.AuctionHouseListingKind.Goods
                                  && listing.ItemGroup == itemGroup
                                  && listing.ItemNumber == itemNumber
                                  && listing.ItemLevel == itemLevel
                                  && listing.SellerCharacterId != excludedSellerCharacterId
                                  && listing.UnitPrice <= maximumUnitPrice
                                  && listing.RemainingQuantity > 0
                                  && listing.ExpiresAt > now)
                .OrderBy(listing => listing.Id)
                .Select(listing => listing.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<Guid>> GetDueAuctionListingIdsAsync(DateTime now, int maximumCount, CancellationToken cancellationToken = default)
    {
        using (this.RepositoryProvider.ContextStack.UseContext(this))
        {
            return await this.Context.Set<AuctionListing>()
                .Where(listing => listing.State == DataModel.Entities.AuctionListingState.Active && listing.ExpiresAt <= now)
                .OrderBy(listing => listing.ExpiresAt)
                .Select(listing => listing.Id)
                .Take(maximumCount)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask<int> GetActiveAuctionListingCountAsync(Guid sellerCharacterId, CancellationToken cancellationToken = default)
    {
        using (this.RepositoryProvider.ContextStack.UseContext(this))
        {
            return await this.Context.Set<AuctionListing>()
                .CountAsync(listing => listing.SellerCharacterId == sellerCharacterId && listing.State == DataModel.Entities.AuctionListingState.Active, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<Guid>> GetAuctionCollectionEntryIdsAsync(Guid ownerCharacterId, Guid? listingId = null, DataModel.Entities.AuctionHouseCollectionEntryKind? kind = null, CancellationToken cancellationToken = default)
    {
        using (this.RepositoryProvider.ContextStack.UseContext(this))
        {
            var query = this.Context.Set<AuctionCollectionEntry>().Where(entry => entry.OwnerCharacterId == ownerCharacterId);
            if (listingId is { } listing)
            {
                query = query.Where(entry => entry.ListingId == listing);
            }

            if (kind is { } entryKind)
            {
                query = query.Where(entry => entry.Kind == entryKind);
            }

            return await query.OrderBy(entry => entry.UpdatedAt).ThenBy(entry => entry.Id).Select(entry => entry.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask<IReadOnlyList<T>> LoadAggregatesAsync<T>(Type modelType, IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
        where T : class
    {
        var result = new System.Collections.Generic.List<T>(ids.Count);
        await this.Context.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var id in ids)
            {
                if (await this.LoadAggregateAsync(modelType, id, cancellationToken).ConfigureAwait(false) is T aggregate)
                {
                    result.Add(aggregate);
                }
            }
        }
        finally
        {
            await this.Context.Database.CloseConnectionAsync().ConfigureAwait(false);
        }

        return result;
    }
}
