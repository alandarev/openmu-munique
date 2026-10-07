# C1 7B 5A - AuctionClaimRequest (by client)

## Is sent when

The player claims one entry or all entries of the collection.

## Causes the following actions on the server side

The server moves items as far as they fit into the inventory and Zen as far as the inventory Zen limit allows, keeps the remainder in the collection, and answers with an AuctionClaimResult with the same request id. Every request without an open broker dialog is answered with NotOpen.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   22   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0x7B  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x5A  | Packet header - sub packet type identifier |
| 4 | 2 | ShortLittleEndian |  | RequestId; The request id, chosen by the client (1 to 65535, 0 is never sent); the answer echoes it. |
| 6 | 16 | Binary |  | EntryId; The id of the collection entry; all zero = every entry. |