# C1 7B 5C - AuctionSellCheckResult (by server)

## Is sent when

As the answer to an AuctionSellCheckRequest.

## Causes the following actions on the client side

The client shows whether and how the item can be listed.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   22   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0x7B  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x5C  | Packet header - sub packet type identifier |
| 4 | 2 | ShortLittleEndian |  | RequestId; The request id of the answered request. |
| 6 | 1 | AuctionResult |  | Result; The result. Anything but Success and Partial means nothing was charged, moved or created. |
| 7 | 1 | Byte |  | InventorySlot; The inventory slot. |
| 8 | 1 | AuctionSellCheckReason |  | Reason; Whether the item is eligible, and if not, why. |
| 9 | 1 | AuctionListingKind |  | Kind; The kind of listing which the item would be. |
| 10 | 2 | ShortLittleEndian |  | Type; The item type, group * 512 + number. |
| 12 | 1 | Byte |  | Level; The item level. |
| 13 | 4 | IntegerLittleEndian |  | Available; For goods, the count of identical loose units in the inventory. |
| 17 | 4 | IntegerLittleEndian |  | LowestUnitPrice; The lowest unit price in Zen of other sellers; 0 = none. |
| 21 | 1 | Byte |  | ActiveListings; The count of active listings of the seller. |

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
| 16 | PriceNotOnStep | A listing price has more significant digits than announced. |

### AuctionSellCheckReason Enum

Defines whether an item is eligible for listing, and if not, why.

| Value | Name | Description |
|-------|------|-------------|
| 0 | Eligible | The item is eligible. |
| 1 | NotTradable | The item is not tradable. |
| 2 | NoItem | There is no item in that slot. |
| 3 | NotAccepted | The item is not accepted by the market. |

### AuctionListingKind Enum

Defines the kind of an auction listing.

| Value | Name | Description |
|-------|------|-------------|
| 1 | Item | An individual item. |
| 2 | Goods | Goods: identical, interchangeable units. |