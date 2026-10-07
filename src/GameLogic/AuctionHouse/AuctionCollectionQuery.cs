// <copyright file="AuctionCollectionQuery.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// A request for a page of the auction house collection.
/// </summary>
/// <param name="Page">The requested page, starting at 0.</param>
public sealed record AuctionCollectionQuery(
    ushort Page);
