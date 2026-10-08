# C2 7B 58 - AuctionOwnListings (by server)

## Is sent when

As the answer to an AuctionOwnListingsRequest.

## Causes the following actions on the client side

The client shows the own listings.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |      | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0x7B  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x58  | Packet header - sub packet type identifier |
| 5 | 2 | ShortLittleEndian |  | RequestId; The request id of the answered request. |
| 7 | 1 | AuctionResult |  | Result; The result. Anything but Success and Partial means nothing was charged, moved or created. |
| 8 | 1 | Byte |  | Count; The count of rows. |
| 9 | AuctionOwnListing.Length * Count | Array of AuctionOwnListing |  | Listings; The listings. Each row has a variable length, because of its item data. |

### AuctionOwnListing Structure

An own listing of the auction house.

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 16 | Binary |  | ListingId; The id of the listing. |
| 16 | 4 | IntegerLittleEndian |  | UnitPrice; The unit price in Zen. |
| 20 | 4 | IntegerLittleEndian |  | Listed; The listed quantity. |
| 24 | 4 | IntegerLittleEndian |  | Remaining; The remaining quantity. |
| 28 | 4 | IntegerLittleEndian |  | SecondsLeft; The remaining seconds of the listing. |
| 32 | 1 | AuctionListingKind |  | Kind; The kind of the listing. |
| 33 | 2 | ShortLittleEndian |  | Type; The item type, group * 512 + number. |
| 35 | 1 | Byte |  | Level; The item level. |
| 36 | 1 | Byte |  | ItemDataLength; The length of the item data; 0 for goods. |
| 37 |  | Binary |  | ItemData; The item data, serialized by the item serializer of the connection. |

### AuctionListingKind Enum

Defines the kind of an auction listing.

| Value | Name | Description |
|-------|------|-------------|
| 1 | Item | An individual item. |
| 2 | Goods | Goods: identical, interchangeable units. |

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