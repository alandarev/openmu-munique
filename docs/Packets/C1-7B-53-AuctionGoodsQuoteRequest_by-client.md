# C1 7B 53 - AuctionGoodsQuoteRequest (by client)

## Is sent when

The player wants to know the price of a quantity of goods, before buying it.

## Causes the following actions on the server side

The server answers with an AuctionGoodsQuote with the same request id: the cheapest price tiers, the fillable quantity and the exact total, without the own listings of the requester. Every request without an open broker dialog is answered with NotOpen.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   13   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0x7B  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x53  | Packet header - sub packet type identifier |
| 4 | 2 | ShortLittleEndian |  | RequestId; The request id, chosen by the client (1 to 65535, 0 is never sent); the answer echoes it. |
| 6 | 2 | ShortLittleEndian |  | Type; The item type, group * 512 + number. |
| 8 | 1 | Byte |  | Level; The item level. |
| 9 | 4 | IntegerLittleEndian |  | Quantity; The requested quantity, at least 1. |