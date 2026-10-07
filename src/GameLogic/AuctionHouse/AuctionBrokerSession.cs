// <copyright file="AuctionBrokerSession.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using MUnique.OpenMU.DataModel.Configuration;
using Nito.AsyncEx;

/// <summary>
/// The auction house broker state of one player: whether the broker is open, the request rate and
/// the lock which lets the requests of the player be processed one at a time.
/// </summary>
/// <remarks>
/// It's kept beside the player instead of on it, and lives as long as the player.
/// </remarks>
public sealed class AuctionBrokerSession
{
    private static readonly ConditionalWeakTable<Player, AuctionBrokerSession> Sessions = new();

    private readonly object _rateLock = new();

    private double _requestTokens = double.NaN;

    private long _lastRefillTimestamp;

    private int _isOpen;

    private int _isQuarantined;

    /// <summary>
    /// Gets a value indicating whether the broker dialog of the player is open.
    /// </summary>
    public bool IsOpen => Volatile.Read(ref this._isOpen) == 1;

    /// <summary>
    /// Gets a value indicating whether the auction house refuses the requests of the player, because the outcome of one
    /// of its transactions is unknown; the player gets disconnected.
    /// </summary>
    public bool IsQuarantined => Volatile.Read(ref this._isQuarantined) == 1;

    /// <summary>
    /// Gets the lock which lets the requests of the player be processed one at a time, in arrival order.
    /// </summary>
    public AsyncLock RequestLock { get; } = new();

    /// <summary>
    /// Gets the session of a player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The session of the player.</returns>
    public static AuctionBrokerSession Of(Player player) => Sessions.GetValue(player, _ => new AuctionBrokerSession());

    /// <summary>
    /// Determines whether the player has the auction house broker open: the broker was opened, the player
    /// still talks to an NPC with the <see cref="NpcWindow.AuctionHouse"/> window, and the NPC dialog is still open.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns><see langword="true"/>, if the player has the broker open; otherwise, <see langword="false"/>.</returns>
    public static bool IsBrokerOpen(Player player)
    {
        return Sessions.TryGetValue(player, out var session)
               && session.IsOpen
               && player.PlayerState.CurrentState == PlayerState.NpcDialogOpened
               && player.OpenedNpc?.Definition.NpcWindow == NpcWindow.AuctionHouse;
    }

    /// <summary>
    /// Refuses all further requests of the player.
    /// </summary>
    /// <returns><see langword="true"/>, if the player wasn't quarantined before.</returns>
    public bool Quarantine() => Interlocked.Exchange(ref this._isQuarantined, 1) == 0;

    /// <summary>
    /// Marks the broker as open.
    /// </summary>
    public void Open() => Volatile.Write(ref this._isOpen, 1);

    /// <summary>
    /// Marks the broker as closed.
    /// </summary>
    /// <returns><see langword="true"/>, if the broker was open before; otherwise, <see langword="false"/>.</returns>
    public bool TryClose() => Interlocked.Exchange(ref this._isOpen, 0) == 1;

    /// <summary>
    /// Tries to take a request within the rate: a token bucket which holds up to
    /// <paramref name="maximumRequestsPerSecond"/> requests and refills at that rate.
    /// </summary>
    /// <param name="maximumRequestsPerSecond">The maximum count of requests per second; 0 means no limit.</param>
    /// <returns><see langword="true"/>, if the request is within the rate; otherwise, <see langword="false"/>.</returns>
    public bool TryTakeRequest(byte maximumRequestsPerSecond)
    {
        if (maximumRequestsPerSecond == 0)
        {
            return true;
        }

        lock (this._rateLock)
        {
            var now = Stopwatch.GetTimestamp();
            if (double.IsNaN(this._requestTokens))
            {
                this._requestTokens = maximumRequestsPerSecond;
            }
            else
            {
                var elapsedSeconds = (double)(now - this._lastRefillTimestamp) / Stopwatch.Frequency;
                this._requestTokens = Math.Min(maximumRequestsPerSecond, this._requestTokens + (elapsedSeconds * maximumRequestsPerSecond));
            }

            this._lastRefillTimestamp = now;
            if (this._requestTokens < 1)
            {
                return false;
            }

            this._requestTokens -= 1;
            return true;
        }
    }
}
