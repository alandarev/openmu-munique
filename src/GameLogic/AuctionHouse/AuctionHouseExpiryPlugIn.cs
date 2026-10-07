// <copyright file="AuctionHouseExpiryPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Asks the market of the game context to expire the listings whose duration ended. The game context calls it every
/// second; the market runs the expiry at most every <see cref="AuctionHouseMarket.ExpiryInterval"/> and never twice at
/// once, also when several game contexts share it.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.AuctionHouseExpiryPlugIn_Name), Description = nameof(PlugInResources.AuctionHouseExpiryPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("7C1D3E52-9A4B-4F0E-8E2D-6B5A1C9F3D27")]
public class AuctionHouseExpiryPlugIn : IPeriodicTaskPlugIn
{
    private bool _forceStart;

    /// <inheritdoc />
    public async ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        if (AuctionHouseMarkets.GetMarket(gameContext) is not AuctionHouseMarket market)
        {
            return;
        }

        var force = this._forceStart;
        this._forceStart = false;
        try
        {
            await market.ExpireDueListingsAsync(force).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            gameContext.LoggerFactory.CreateLogger<AuctionHouseExpiryPlugIn>().LogError(ex, "Error while expiring the auction house listings.");
        }
    }

    /// <inheritdoc />
    public void ForceStart()
    {
        this._forceStart = true;
    }
}
