// <copyright file="AuctionHouseSerialization.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.AuctionHouse;

using System.Text;
using MUnique.OpenMU.DataModel.Entities;

/// <summary>
/// Helpers which write the common fields of the auction house packets.
/// </summary>
internal static class AuctionHouseSerialization
{
    /// <summary>
    /// The length of an id field.
    /// </summary>
    public const int IdLength = 16;

    /// <summary>
    /// The length of a character name field.
    /// </summary>
    public const int NameLength = 10;

    /// <summary>
    /// The maximum count of rows of a list, because the count field is a byte.
    /// </summary>
    public const int MaximumRowCount = byte.MaxValue;

    /// <summary>
    /// Writes an id into its 16 byte field.
    /// </summary>
    /// <param name="target">The id field.</param>
    /// <param name="id">The id.</param>
    public static void WriteId(Span<byte> target, Guid id)
    {
        if (!id.TryWriteBytes(target[..IdLength]))
        {
            throw new ArgumentException("The target is too small for an id.", nameof(target));
        }
    }

    /// <summary>
    /// Cuts a character name to at most <see cref="NameLength"/> bytes of UTF-8, on a character boundary.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>The cut name.</returns>
    public static string CutName(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return string.Empty;
        }

        var byteCount = 0;
        var builder = new StringBuilder(name.Length);
        foreach (var rune in name.EnumerateRunes())
        {
            byteCount += rune.Utf8SequenceLength;
            if (byteCount > NameLength)
            {
                break;
            }

            builder.Append(rune.ToString());
        }

        return builder.ToString();
    }

    /// <summary>
    /// Writes the item data of a row: the length byte, followed by the serialized item.
    /// </summary>
    /// <param name="target">The target, starting at the item data length field.</param>
    /// <param name="item">The item; <see langword="null"/> writes an empty item data.</param>
    /// <param name="itemSerializer">The item serializer of the connection.</param>
    /// <returns>The count of written item data bytes, without the length byte.</returns>
    public static int WriteItemData(Span<byte> target, Item? item, IItemSerializer itemSerializer)
    {
        if (item is null)
        {
            target[0] = 0;
            return 0;
        }

        var length = itemSerializer.SerializeItem(target[1..], item);
        if (length > byte.MaxValue)
        {
            throw new ArgumentException($"The item data of {item} is longer than {byte.MaxValue} bytes.", nameof(item));
        }

        target[0] = (byte)length;
        return length;
    }
}
