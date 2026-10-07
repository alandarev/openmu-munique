// <copyright file="AuctionHouseFeaturePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

using System.Runtime.InteropServices;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Feature plugin of the auction house: its configuration are the <see cref="AuctionHouseRules"/>,
/// and it provides the <see cref="IAuctionHouseMarket"/> behind the broker.
/// </summary>
/// <remarks>
/// Without an active feature, an auction house NPC still opens its broker with the default rules, and every request
/// is answered with <see cref="AuctionHouseResult.Unavailable"/>. With an active feature, the requests go to the
/// <see cref="Market"/>, or when it isn't set, to the market which the game context shares with all game contexts of
/// the same database and game configuration (<see cref="AuctionHouseMarkets"/>).
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.AuctionHouseFeaturePlugIn_Name), Description = nameof(PlugInResources.AuctionHouseFeaturePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("0BC1F21C-97E4-4C69-9E8C-0C6A4E5D7A31")]
public class AuctionHouseFeaturePlugIn : IFeaturePlugIn, ISupportCustomConfiguration<AuctionHouseRules>, ISupportDefaultCustomConfiguration, IDisabledByDefault
{
    /// <inheritdoc/>
    public AuctionHouseRules? Configuration { get; set; }

    /// <summary>
    /// Gets or sets a market which replaces the shared market of the game context, e.g. for tests;
    /// <see langword="null"/> to use the shared market.
    /// </summary>
    public IAuctionHouseMarket? Market { get; set; }

    /// <inheritdoc />
    public object CreateDefaultConfig() => AuctionHouseRules.Default;
}
