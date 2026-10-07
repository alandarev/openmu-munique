// <copyright file="ShowAuctionHouseStatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.AuctionHouse;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.AuctionHouse;
using MUnique.OpenMU.GameLogic.Views.AuctionHouse;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using MUnique.OpenMU.Network.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IShowAuctionHouseStatePlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
/// <remarks>
/// The game client has to support the <see cref="AuctionHouseState"/> packet of the auction house.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.ShowAuctionHouseStatePlugIn_Name), Description = nameof(PlugInResources.ShowAuctionHouseStatePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("CED2CB04-70C6-49B9-95B4-F0A01DFCA858")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class ShowAuctionHouseStatePlugIn : IShowAuctionHouseStatePlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowAuctionHouseStatePlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ShowAuctionHouseStatePlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public ValueTask ShowAuctionHouseOpenedAsync(AuctionBrokerState state) => this.SendAsync(state);

    /// <inheritdoc/>
    public ValueTask ShowAuctionHouseClosedAsync() => this.SendAsync(null);

    /// <summary>
    /// Writes the <see cref="AuctionHouseState"/> packet.
    /// </summary>
    /// <param name="target">The target, of <see cref="AuctionHouseStateRef.Length"/>.</param>
    /// <param name="state">The announced state of the open broker; <see langword="null"/> for the closed broker.</param>
    /// <returns>The written length.</returns>
    internal static int Write(Span<byte> target, AuctionBrokerState? state)
    {
        target = target[..AuctionHouseStateRef.Length];
        target.Clear();
        var packet = new AuctionHouseStateRef(target);
        if (state is null)
        {
            packet.State = AuctionHouseWindowState.Closed;
            return target.Length;
        }

        packet.State = AuctionHouseWindowState.Open;
        packet.PageSize = state.PageSize;
        packet.MaxActiveListings = state.MaximumActiveListings;
        packet.ActiveListings = state.ActiveListings;
        packet.PendingCollection = state.PendingCollection;
        packet.ZenLimit = state.ZenLimit;
        packet.MaxPrice = state.MaximumPrice;
        packet.PostingFeeFlat = state.PostingFeeFlat;
        packet.PostingFeeBasisPoints = state.PostingFeeBasisPoints;
        packet.SaleFeeBasisPoints = state.SaleFeeBasisPoints;
        packet.Flags = (byte)(state.RefundPostingFeeOnCancel ? 0x01 : 0x00);
        var durations = state.DurationHours.Take(AuctionHouseRules.MaximumDurationCount).ToArray();
        packet.DurationCount = (byte)durations.Length;
        durations.CopyTo(packet.Durations);
        packet.GoodsPriceDigits = state.GoodsPriceSignificantDigits;
        return target.Length;
    }

    private async ValueTask SendAsync(AuctionBrokerState? state)
    {
        if (this._player.Connection is not { } connection)
        {
            return;
        }

        int Write() => ShowAuctionHouseStatePlugIn.Write(connection.Output.GetSpan(AuctionHouseStateRef.Length), state);

        await connection.SendAsync(Write).ConfigureAwait(false);
    }
}
