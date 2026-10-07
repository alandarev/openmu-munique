// <copyright file="AuctionHouseTestHelper.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using Moq;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.AuctionHouse;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlayerActions;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.GameLogic.Views;

/// <summary>
/// Creates players for the auction house tests.
/// </summary>
internal static class AuctionHouseTestHelper
{
    /// <summary>
    /// Creates the rules of the tests, which match the golden fixtures: a posting fee of 1,000 Zen plus 1 %,
    /// so listing 18 units at 1,000,000 Zen costs 181,000 Zen.
    /// </summary>
    /// <param name="maximumRequestsPerSecond">The maximum requests per second; 0 means no limit.</param>
    /// <returns>The rules.</returns>
    public static AuctionHouseRules CreateRules(byte maximumRequestsPerSecond = 0) => new()
    {
        PageSize = 20,
        MaximumActiveListings = 30,
        MaximumPrice = 2_000_000_000,
        PostingFeeFlat = 1_000,
        PostingFeeBasisPoints = 100,
        SaleFeeBasisPoints = 500,
        RefundPostingFeeOnCancel = true,
        DurationHours = [12, 24, 48, 72],
        MaximumRequestsPerSecond = maximumRequestsPerSecond,
    };

    /// <summary>
    /// Creates a player in the world, with the auction house feature and, optionally, a market behind it.
    /// </summary>
    /// <param name="market">The market; <see langword="null"/> for none.</param>
    /// <param name="rules">The rules; <see langword="null"/> for <see cref="CreateRules"/>.</param>
    /// <param name="activateFeature">If set to <c>true</c>, the auction house feature is active.</param>
    /// <returns>The player.</returns>
    public static async ValueTask<Player> CreatePlayerAsync(IAuctionHouseMarket? market, AuctionHouseRules? rules = null, bool activateFeature = true)
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.GameContext.Configuration.MaximumInventoryMoney = int.MaxValue;
        if (activateFeature)
        {
            player.GameContext.FeaturePlugIns.AddPlugIn(new AuctionHouseFeaturePlugIn { Configuration = rules ?? CreateRules(), Market = market }, true);
        }

        player.GameContext.PlugInManager.RegisterPlugInAtPlugInPoint<IPlayerStateChangedPlugIn>(new AuctionBrokerClosingPlugIn());
        return player;
    }

    /// <summary>
    /// Creates an NPC with the auction house window.
    /// </summary>
    /// <returns>The NPC.</returns>
    public static NonPlayerCharacter CreateAuctionHouseNpc()
    {
        return new NonPlayerCharacter(null!, new Persistence.BasicModel.MonsterDefinition { Number = 250, NpcWindow = NpcWindow.AuctionHouse }, null!);
    }

    /// <summary>
    /// Lets the player talk to an auction house NPC, which opens its broker.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The task.</returns>
    public static async ValueTask OpenBrokerAsync(Player player)
    {
        await new TalkNpcAction().TalkToNpcAsync(player, CreateAuctionHouseNpc()).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the mock of a view plugin of the player.
    /// </summary>
    /// <typeparam name="T">The type of the view plugin.</typeparam>
    /// <param name="player">The player.</param>
    /// <returns>The mock.</returns>
    public static Mock<T> View<T>(this Player player)
        where T : class, IViewPlugIn
        => Mock.Get(player.ViewPlugIns.GetPlugIn<T>()!);
}
