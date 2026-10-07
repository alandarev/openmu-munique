# C1 7B 57 - AuctionCancelListingRequest (by client)

## Is sent when

The player cancels an own active listing.

## Causes the following actions on the server side

The server ends the listing, returns the remaining units to the inventory as far as they fit, otherwise to the collection, and answers with an AuctionCancelListingResult with the same request id. Every request without an open broker dialog is answered with NotOpen.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   22   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0x7B  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x57  | Packet header - sub packet type identifier |
| 4 | 2 | ShortLittleEndian |  | RequestId; The request id, chosen by the client (1 to 65535, 0 is never sent); the answer echoes it. |
| 6 | 16 | Binary |  | ListingId; The id of the listing. |