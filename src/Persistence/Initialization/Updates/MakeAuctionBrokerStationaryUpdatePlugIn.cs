// <copyright file="MakeAuctionBrokerStationaryUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// This update makes Wandering Merchant Zyro (568), the auction house broker, stationary: he stands in Lorencia, Devias
/// and Elbeland at the same time, instead of being shown in one of these towns at a time by the wandering merchants.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("8D3B6E27-4F1A-4C95-9E2B-7A6C1D5F0B38")]
public class MakeAuctionBrokerStationaryUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>
    /// The plug in name.
    /// </summary>
    internal const string PlugInName = "Make auction house broker stationary";

    /// <summary>
    /// The plug in description.
    /// </summary>
    internal const string PlugInDescription = "Makes Wandering Merchant Zyro (568), the auction house broker, stand in each of his towns, instead of wandering between them.";

    private const short ZyroNpcNumber = 568;

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.MakeAuctionBrokerStationary;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        foreach (var spawn in gameConfiguration.Maps
                     .SelectMany(map => map.MonsterSpawns)
                     .Where(spawn => spawn.MonsterDefinition?.Number == ZyroNpcNumber && spawn.SpawnTrigger == SpawnTrigger.Wandering))
        {
            spawn.SpawnTrigger = SpawnTrigger.Automatic;
        }

        return default;
    }
}
