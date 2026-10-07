// <copyright file="CustomGroupHandlerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Network.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Packet handler for the packets of features which the original game client doesn't have (0x7B identifier).
/// </summary>
/// <remarks>
/// These packets share one head code which no original packet uses, so they can't collide with the
/// packets of the original protocol which may be added later. Each feature owns a block of 16 sub codes.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.CustomGroupHandlerPlugIn_Name), Description = nameof(PlugInResources.CustomGroupHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("9F58AD09-CBF6-4759-AF83-1CE3E88195BA")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
internal class CustomGroupHandlerPlugIn : GroupPacketHandlerPlugIn
{
    /// <summary>
    /// The group key. It's the <see cref="Network.Packets.ClientToServer.AuctionSearchRequest.Code"/>,
    /// which can't be used here directly because it's not a compile time constant.
    /// </summary>
    internal const byte GroupKey = 0x7B;

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomGroupHandlerPlugIn" /> class.
    /// </summary>
    /// <param name="clientVersionProvider">The client version provider.</param>
    /// <param name="manager">The manager.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    public CustomGroupHandlerPlugIn(IClientVersionProvider clientVersionProvider, PlugInManager manager, ILoggerFactory loggerFactory)
        : base(clientVersionProvider, manager, loggerFactory)
    {
    }

    /// <inheritdoc/>
    public override bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public override byte Key => GroupKey;
}
