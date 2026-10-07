// <copyright file="AuctionAncientSetMember.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// An ancient set member of an auction house search.
/// </summary>
/// <param name="ItemType">The item type, <c>group * 512 + number</c>.</param>
/// <param name="Discriminator">The ancient discriminator, 1 or 2.</param>
public sealed record AuctionAncientSetMember(
    ushort ItemType,
    byte Discriminator);
