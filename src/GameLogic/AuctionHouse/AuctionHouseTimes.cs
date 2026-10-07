// <copyright file="AuctionHouseTimes.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// Normalizes the points in time of market rows to UTC before they are compared.
/// </summary>
/// <remarks>
/// The market stores UTC. Rows which the entity framework persistence loads as JSON aggregates come back as local times
/// (<see cref="DateTimeKind.Local"/>), and <see cref="DateTime"/> comparisons ignore the kind.
/// </remarks>
public static class AuctionHouseTimes
{
    /// <summary>
    /// Gets a point in time as UTC: local times are converted, unspecified ones are taken as UTC.
    /// </summary>
    /// <param name="value">The point in time.</param>
    /// <returns>The point in time in UTC.</returns>
    public static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Local => value.ToUniversalTime(),
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        _ => value,
    };
}
