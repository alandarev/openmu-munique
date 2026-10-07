# C2 7B 52 - AuctionGoodsList (by server)

## Is sent when

As the answer to an AuctionGoodsListRequest.

## Causes the following actions on the client side

The client shows the goods of the requested page.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |      | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0x7B  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x52  | Packet header - sub packet type identifier |
| 5 | 2 | ShortLittleEndian |  | RequestId; The request id of the answered request. |
| 7 | 1 | AuctionResult |  | Result; The result. Anything but Success and Partial means nothing was charged, moved or created. |
| 8 | 2 | ShortLittleEndian |  | Page; The page, starting at 0. |
| 10 | 4 | IntegerLittleEndian |  | TotalCount; The total count of goods identities. |
| 14 | 1 | Byte |  | Count; The count of rows. |
| 15 | AuctionGoodsEntry.Length * Count | Array of AuctionGoodsEntry |  | Goods; The goods, ordered by type and level. |

### AuctionGoodsEntry Structure

A goods identity of an auction goods list.

Length: 13 Bytes

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 2 | ShortLittleEndian |  | Type; The item type, group * 512 + number. |
| 2 | 1 | Byte |  | Level; The item level. |
| 3 | 4 | IntegerLittleEndian |  | LowestUnitPrice; The lowest unit price in Zen. |
| 7 | 4 | IntegerLittleEndian |  | Available; The available quantity. |
| 11 | 2 | ShortLittleEndian |  | ListingCount; The count of listings. |

### AuctionResult Enum

Defines the result of an auction house request. Anything but Success and Partial means nothing was charged, moved or created; list answers then carry a count of 0.

| Value | Name | Description |
|-------|------|-------------|
| 0 | Success | The request succeeded. |
| 1 | Unavailable | The auction house is unavailable. |
| 2 | NotOpen | No broker dialog is open. |
| 3 | Busy | The server is busy with another request of the player. |
| 4 | InvalidRequest | The request is invalid. |
| 5 | NotFound | The listing or entry was not found. |
| 6 | QuoteChanged | The price or total changed since it was reviewed. |
| 7 | InsufficientStock | Fewer units are available than requested. |
| 8 | InsufficientZen | The player has not enough Zen. |
| 9 | NotEligible | The item is not eligible for listing. |
| 10 | ListingLimitReached | The seller has reached the maximum count of active listings. |
| 11 | PriceOutOfRange | A price or total is out of range. |
| 12 | RulesChanged | The rules changed; a new AuctionHouseState follows. |
| 13 | OwnListing | The listing is the own listing of the player. |
| 14 | Partial | The request succeeded partially, something was kept back. |
| 15 | InternalError | An internal error occurred. |
| 16 | PriceNotOnStep | A goods unit price has more significant digits than announced. |