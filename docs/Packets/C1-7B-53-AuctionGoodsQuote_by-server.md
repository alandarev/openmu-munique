# C1 7B 53 - AuctionGoodsQuote (by server)

## Is sent when

As the answer to an AuctionGoodsQuoteRequest; and after an AuctionBuyGoodsResult with QuoteChanged or InsufficientStock, with the request id of the purchase.

## Causes the following actions on the client side

The client shows the price tiers and the total of the quantity.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |      | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0x7B  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x53  | Packet header - sub packet type identifier |
| 4 | 2 | ShortLittleEndian |  | RequestId; The request id of the answered request. |
| 6 | 1 | AuctionResult |  | Result; Success when the requested quantity is fillable, InsufficientStock when only less is available, PriceOutOfRange when the total would exceed the maximum price. |
| 7 | 2 | ShortLittleEndian |  | Type; The item type, group * 512 + number. |
| 9 | 1 | Byte |  | Level; The item level. |
| 10 | 4 | IntegerLittleEndian |  | RequestedQuantity; The requested quantity. |
| 14 | 4 | IntegerLittleEndian |  | FillableQuantity; The fillable quantity, at most the requested quantity. |
| 18 | 4 | IntegerLittleEndian |  | Total; The total in Zen for the fillable quantity, cheapest first; 0 with PriceOutOfRange. |
| 22 | 1 | Byte |  | TierCount; The count of price tiers, at most 8. |
| 23 | AuctionGoodsPriceTier.Length * TierCount | Array of AuctionGoodsPriceTier |  | Tiers; The price tiers, cheapest first. |

### AuctionGoodsPriceTier Structure

A price tier of an auction goods quote.

Length: 8 Bytes

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 4 | IntegerLittleEndian |  | UnitPrice; The unit price in Zen. |
| 4 | 4 | IntegerLittleEndian |  | Quantity; The available quantity at this unit price. |

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
| 16 | PriceNotOnStep | A listing price is not a multiple of its price step, see PriceStepPrecision. |