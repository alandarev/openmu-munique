# C1 7B 55 - AuctionBuyGoodsRequest (by client)

## Is sent when

The player buys a quantity of goods, after reviewing its total.

## Causes the following actions on the server side

The server completes the purchase only for the full quantity at a current total not above the reviewed total, and answers with an AuctionBuyGoodsResult with the same request id. After QuoteChanged or InsufficientStock, a fresh AuctionGoodsQuote with the same request id follows. Every request without an open broker dialog is answered with NotOpen.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   17   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0x7B  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x55  | Packet header - sub packet type identifier |
| 4 | 2 | ShortLittleEndian |  | RequestId; The request id, chosen by the client (1 to 65535, 0 is never sent); the answer echoes it. |
| 6 | 2 | ShortLittleEndian |  | Type; The item type, group * 512 + number. |
| 8 | 1 | Byte |  | Level; The item level. |
| 9 | 4 | IntegerLittleEndian |  | Quantity; The quantity to buy. |
| 13 | 4 | IntegerLittleEndian |  | ReviewedTotal; The total in Zen which the player reviewed. |