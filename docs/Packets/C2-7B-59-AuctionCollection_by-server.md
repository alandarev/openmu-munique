# C2 7B 59 - AuctionCollection (by server)

## Is sent when

As the answer to an AuctionCollectionRequest.

## Causes the following actions on the client side

The client shows the collection entries of the requested page.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |      | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0x7B  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x59  | Packet header - sub packet type identifier |
| 5 | 2 | ShortLittleEndian |  | RequestId; The request id of the answered request. |
| 7 | 1 | AuctionResult |  | Result; The result. Anything but Success and Partial means nothing was charged, moved or created. |
| 8 | 2 | ShortLittleEndian |  | Page; The page, starting at 0. |
| 10 | 2 | ShortLittleEndian |  | TotalCount; The total count of collection entries. |
| 12 | 1 | Byte |  | Count; The count of entries. |
| 13 | AuctionCollectionEntry.Length * Count | Array of AuctionCollectionEntry |  | Entries; The collection entries. Each entry has a variable length, because of its item data. |

### AuctionCollectionEntry Structure

An entry of the auction house collection.

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 16 | Binary |  | EntryId; The id of the entry. |
| 16 | 1 | AuctionCollectionEntryKind |  | Kind; The kind of the entry. |
| 17 | 2 | ShortLittleEndian |  | Type; The item type, group * 512 + number. |
| 19 | 1 | Byte |  | Level; The item level. |
| 20 | 4 | IntegerLittleEndian |  | Quantity; The quantity. |
| 24 | 4 | IntegerLittleEndian |  | Gross; The gross in Zen. |
| 28 | 4 | IntegerLittleEndian |  | Fee; The fee in Zen. |
| 32 | 4 | IntegerLittleEndian |  | Net; The Zen still to collect. |
| 36 | 1 | Byte |  | ItemDataLength; The length of the item data; 0 except for individual items. |
| 37 |  | Binary |  | ItemData; The item data, serialized by the item serializer of the connection. |

### AuctionCollectionEntryKind Enum

Defines the kind of an auction collection entry.

| Value | Name | Description |
|-------|------|-------------|
| 1 | Purchase | A purchase which did not fit into the inventory. |
| 2 | Proceeds | The proceeds of a listing. |
| 3 | Cancelled | The units of a cancelled listing. |
| 4 | Expired | The units of an expired listing. |

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