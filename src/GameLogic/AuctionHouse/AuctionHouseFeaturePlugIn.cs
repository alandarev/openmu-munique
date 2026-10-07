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
/// Without an active feature or without a market, an auction house NPC still opens its broker with the
/// default rules, and every request is answered with <see cref="AuctionHouseResult.Unavailable"/>.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.AuctionHouseFeaturePlugIn_Name), Description = nameof(PlugInResources.AuctionHouseFeaturePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("0BC1F21C-97E4-4C69-9E8C-0C6A4E5D7A31")]
public class AuctionHouseFeaturePlugIn : IFeaturePlugIn, ISupportCustomConfiguration<AuctionHouseRules>, ISupportDefaultCustomConfiguration, IDisabledByDefault
{
    /// <inheritdoc/>
    public AuctionHouseRules? Configuration { get; set; }

    /// <summary>
    /// Gets or sets the market behind the broker; <see langword="null"/> when no market is available.
    /// </summary>
    public IAuctionHouseMarket? Market { get; set; }

    /// <inheritdoc />
    public object CreateDefaultConfig() => AuctionHouseRules.Default;
}
