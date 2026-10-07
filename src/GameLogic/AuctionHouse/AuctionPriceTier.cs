// <copyright file="AuctionPriceTier.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// A price tier of a goods quotation.
/// </summary>
/// <param name="UnitPrice">The unit price in Zen.</param>
/// <param name="Quantity">The available quantity at this unit price.</param>
public sealed record AuctionPriceTier(
    uint UnitPrice,
    uint Quantity);
