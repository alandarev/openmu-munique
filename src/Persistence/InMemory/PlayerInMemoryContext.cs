// <copyright file="PlayerInMemoryContext.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.InMemory;

using System.Threading;
using MUnique.OpenMU.Persistence.BasicModel;

/// <summary>
/// In-memory context implementation for <see cref="IPlayerContext"/>.
/// </summary>
public class PlayerInMemoryContext : InMemoryContext, IPlayerContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PlayerInMemoryContext"/> class.
    /// </summary>
    /// <param name="provider">The manager which holds the memory repositories.</param>
    public PlayerInMemoryContext(InMemoryRepositoryProvider provider)
        : base(provider)
    {
    }

    /// <inheritdoc/>
    public async ValueTask<MUnique.OpenMU.DataModel.Entities.LetterBody?> GetLetterBodyByHeaderIdAsync(Guid headerId, CancellationToken cancellationToken = default)
    {
        var allLetters = await this.Provider.GetRepository<LetterBody>().GetAllAsync(cancellationToken).ConfigureAwait(false);
        return allLetters.FirstOrDefault(body => body.Header.Id == headerId);
    }

    /// <inheritdoc/>
    public async ValueTask<MUnique.OpenMU.DataModel.Entities.AccountState?> AuthenticateAsync(string loginName, string password, CancellationToken cancellationToken = default)
    {
        var allAccounts = await this.Provider.GetRepository<Account>().GetAllAsync(cancellationToken).ConfigureAwait(false);
        var account = allAccounts.FirstOrDefault(a => a.LoginName == loginName && BCrypt.Net.BCrypt.Verify(password, a.PasswordHash));
        return account?.State;
    }

    /// <inheritdoc/>
    public async ValueTask<MUnique.OpenMU.DataModel.Entities.Account?> GetAccountByLoginNameAsync(string loginName, string password, CancellationToken cancellationToken = default)
    {
        var allAccounts = await this.Provider.GetRepository<Account>().GetAllAsync(cancellationToken).ConfigureAwait(false);
        return allAccounts.FirstOrDefault(account => account.LoginName == loginName && BCrypt.Net.BCrypt.Verify(password, account.PasswordHash));
    }

    /// <inheritdoc/>
    public async ValueTask<MUnique.OpenMU.DataModel.Entities.Account?> GetAccountByLoginNameAsync(string loginName, CancellationToken cancellationToken = default)
    {
        var allAccounts = await this.Provider.GetRepository<Account>().GetAllAsync(cancellationToken).ConfigureAwait(false);
        return allAccounts.FirstOrDefault(account => account.LoginName == loginName);
    }

    /// <inheritdoc/>
    public async ValueTask<IEnumerable<MUnique.OpenMU.DataModel.Entities.Account>> GetAccountsOrderedByLoginNameAsync(int skip, int count, CancellationToken cancellationToken = default)
    {
        var allAccounts = await this.Provider.GetRepository<Account>().GetAllAsync(cancellationToken).ConfigureAwait(false);
        return allAccounts.OrderBy(a => a.LoginName).Skip(skip).Take(count);
    }

    /// <inheritdoc/>
    public async ValueTask<IEnumerable<MUnique.OpenMU.DataModel.Entities.Account>> SearchAccountsAsync(string searchTerm, int skip, int count, CancellationToken cancellationToken = default)
    {
        var allAccounts = await this.Provider.GetRepository<Account>().GetAllAsync(cancellationToken).ConfigureAwait(false);
        return allAccounts
            .Where(a => a.LoginName.Contains(searchTerm, StringComparison.InvariantCultureIgnoreCase)
                        || a.Characters.Any(c => c.Name.Contains(searchTerm, StringComparison.InvariantCultureIgnoreCase)))
            .OrderBy(a => a.LoginName)
            .Skip(skip)
            .Take(count);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> CanSaveLetterAsync(Interfaces.LetterHeader letterHeader, CancellationToken cancellationToken = default)
    {
        return true;
    }

    /// <inheritdoc />
    public async ValueTask<DataModel.Entities.Account?> GetAccountByCharacterNameAsync(string characterName, CancellationToken cancellationToken = default)
    {
        var allAccounts = await this.Provider.GetRepository<Account>().GetAllAsync(cancellationToken).ConfigureAwait(false);
        return allAccounts.FirstOrDefault(account => account.Characters.Any(c => c.Name == characterName));
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<DataModel.Entities.CastleSiegePendingReward>> GetPendingCastleSiegeRewardsAsync(
        Guid characterId,
        CancellationToken cancellationToken = default)
    {
        var pendingRewards = await this.Provider.GetRepository<CastleSiegePendingReward>()
            .GetAllAsync(cancellationToken)
            .ConfigureAwait(false);
        return pendingRewards.Where(reward => reward.CharacterId == characterId).ToList();
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<DataModel.Entities.AuctionListing>> GetActiveAuctionListingsAsync(CancellationToken cancellationToken = default)
    {
        var listings = await this.Provider.GetRepository<AuctionListing>().GetAllAsync(cancellationToken).ConfigureAwait(false);
        return listings.Where(listing => listing.State == DataModel.Entities.AuctionListingState.Active).OrderBy(listing => listing.Id).ToList();
    }

    /// <inheritdoc />
    public async ValueTask<int> GetActiveAuctionListingCountAsync(Guid sellerCharacterId, CancellationToken cancellationToken = default)
    {
        var listings = await this.Provider.GetRepository<AuctionListing>().GetAllAsync(cancellationToken).ConfigureAwait(false);
        return listings.Count(listing => listing.SellerCharacterId == sellerCharacterId && listing.State == DataModel.Entities.AuctionListingState.Active);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<Guid>> GetAuctionCollectionEntryIdsAsync(Guid ownerCharacterId, Guid? listingId = null, DataModel.Entities.AuctionHouseCollectionEntryKind? kind = null, CancellationToken cancellationToken = default)
    {
        var entries = await this.Provider.GetRepository<AuctionCollectionEntry>().GetAllAsync(cancellationToken).ConfigureAwait(false);
        return entries
            .Where(entry => entry.OwnerCharacterId == ownerCharacterId
                            && (listingId is null || entry.ListingId == listingId)
                            && (kind is null || entry.Kind == kind))
            .Select(entry => entry.Id)
            .Order()
            .ToList();
    }

    /// <inheritdoc />
    public async ValueTask<T> RunInTransactionAsync<T>(Func<IPersistenceTransaction, ValueTask<T>> operation, CancellationToken cancellationToken = default)
    {
        await this.Provider.TransactionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var transaction = new InMemoryTransaction(this);
            try
            {
                return await operation(transaction).ConfigureAwait(false);
            }
            catch
            {
                await transaction.RestoreAsync().ConfigureAwait(false);
                throw;
            }
        }
        finally
        {
            this.Provider.TransactionLock.Release();
        }
    }
}
