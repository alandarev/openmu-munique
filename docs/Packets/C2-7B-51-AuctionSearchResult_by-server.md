# C2 7B 51 - AuctionSearchResult (by server)

## Is sent when

As the answer to an AuctionSearchRequest.

## Causes the following actions on the client side

The client shows the item listings of the requested page.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |      | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0x7B  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x51  | Packet header - sub packet type identifier |
| 5 | 2 | ShortLittleEndian |  | RequestId; The request id of the answered request. |
| 7 | 1 | AuctionResult |  | Result; The result. Anything but Success and Partial means nothing was charged, moved or created. |
| 8 | 2 | ShortLittleEndian |  | Page; The page, starting at 0. |
| 10 | 4 | IntegerLittleEndian |  | TotalCount; The total count of matching listings. |
| 14 | 1 | Byte |  | Count; The count of rows. |
| 15 | AuctionSearchListing.Length * Count | Array of AuctionSearchListing |  | Listings; The listings. Each row has a variable length, because of its item data. |

### AuctionSearchListing Structure

An item listing of an auction search result.

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 16 | Binary |  | ListingId; The id of the listing. |
| 16 | 4 | IntegerLittleEndian |  | Price; The price in Zen. |
| 20 | 4 | IntegerLittleEndian |  | SecondsLeft; The remaining seconds of the listing. |
| 24 | 10 | String |  | Seller; The character name of the seller. |
| 34 | 1 | Byte |  | Flags; Bit 0: the requesting character meets the requirements of the item; bit 1: it is the own listing of the requester. |
| 35 | 1 | Byte |  | ItemDataLength; The length of the item data. |
| 36 |  | Binary |  | ItemData; The item data, serialized by the item serializer of the connection. |

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