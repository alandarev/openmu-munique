// <copyright file="AddAuctionHouseBrokerUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.AuctionHouse;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// This update makes Wandering Merchant Zyro (568) the broker of the auction house and activates the auction house:
/// its feature with the default rules, the expiry of listings and the notice about waiting deliveries.
/// </summary>
/// <remarks>
/// Zyro has no merchant store, so talking to him opens the broker. The feature gets the default rules unless it was
/// active already: a configuration which the server created while the feature was disabled by default holds the rules
/// of that time, which never applied. An active feature keeps the rules of its operator. Missing plug-in
/// configurations are created; all are activated.
/// </remarks>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("5B9E2D41-7C3A-4E86-B0F5-1A2D8C6E4F97")]
public class AddAuctionHouseBrokerUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>
    /// The plug in name.
    /// </summary>
    internal const string PlugInName = "Add auction house broker";

    /// <summary>
    /// The plug in description.
    /// </summary>
    internal const string PlugInDescription = "Makes Wandering Merchant Zyro (568) the auction house broker and activates the auction house with its default rules.";

    private const short ZyroNpcNumber = 568;

    private static readonly Type[] AuctionHousePlugInTypes =
    [
        typeof(AuctionHouseFeaturePlugIn),
        typeof(AuctionHouseExpiryPlugIn),
        typeof(AuctionHouseLoginNoticePlugIn),
    ];

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.AddAuctionHouseBroker;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        if (gameConfiguration.Monsters.FirstOrDefault(m => m.Number == ZyroNpcNumber) is { } zyro)
        {
            zyro.NpcWindow = NpcWindow.AuctionHouse;
        }

        foreach (var plugInType in AuctionHousePlugInTypes)
        {
            var configuration = gameConfiguration.PlugInConfigurations.FirstOrDefault(c => c.TypeId == plugInType.GUID);
            if (configuration is null)
            {
                configuration = context.CreateNew<PlugInConfiguration>();
                configuration.TypeId = plugInType.GUID;
                gameConfiguration.PlugInConfigurations.Add(configuration);
            }

            if (plugInType == typeof(AuctionHouseFeaturePlugIn)
                && (!configuration.IsActive || string.IsNullOrWhiteSpace(configuration.CustomConfiguration)))
            {
                configuration.SetConfiguration(AuctionHouseRules.Default, null);
            }

            configuration.IsActive = true;
        }

        return default;
    }
}
