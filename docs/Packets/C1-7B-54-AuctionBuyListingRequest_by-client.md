# C1 7B 54 - AuctionBuyListingRequest (by client)

## Is sent when

The player buys an item listing, after reviewing its price.

## Causes the following actions on the server side

The server completes the purchase if the listing is still active at the reviewed price, is not the own listing of the buyer and the buyer has enough Zen, and answers with an AuctionBuyListingResult with the same request id. Every request without an open broker dialog is answered with NotOpen.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   26   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0x7B  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x54  | Packet header - sub packet type identifier |
| 4 | 2 | ShortLittleEndian |  | RequestId; The request id, chosen by the client (1 to 65535, 0 is never sent); the answer echoes it. |
| 6 | 16 | Binary |  | ListingId; The id of the listing. |
| 22 | 4 | IntegerLittleEndian |  | ReviewedPrice; The price in Zen which the player reviewed. |