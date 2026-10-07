# C1 7B 52 - AuctionGoodsListRequest (by client)

## Is sent when

The player opens the goods list of the auction house, or turns its page.

## Causes the following actions on the server side

The server answers with an AuctionGoodsList with the same request id: per goods identity the lowest unit price, the available quantity and the listing count, without the own listings of the requester. Every request without an open broker dialog is answered with NotOpen.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |      | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0x7B  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x52  | Packet header - sub packet type identifier |
| 4 | 2 | ShortLittleEndian |  | RequestId; The request id, chosen by the client (1 to 65535, 0 is never sent); the answer echoes it. |
| 6 | 2 | ShortLittleEndian |  | Page; The requested page, starting at 0. |
| 8 | 1 | Byte |  | TypeCount; The count of item types, at most 64; 0 = all goods. |
| 9 | AuctionItemType.Length * TypeCount | Array of AuctionItemType |  | Types; The item types to which the list is limited. |

### AuctionItemType Structure

An item type of an auction goods list request.

Length: 2 Bytes

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 2 | ShortLittleEndian |  | Type; The item type, group * 512 + number. |