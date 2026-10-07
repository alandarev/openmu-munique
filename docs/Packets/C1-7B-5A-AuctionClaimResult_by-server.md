# C1 7B 5A - AuctionClaimResult (by server)

## Is sent when

As the answer to an AuctionClaimRequest.

## Causes the following actions on the client side

The client shows the result of the claim.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   20   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0x7B  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x5A  | Packet header - sub packet type identifier |
| 4 | 2 | ShortLittleEndian |  | RequestId; The request id of the answered request. |
| 6 | 1 | AuctionResult |  | Result; Success, or Partial when anything was kept back. |
| 7 | 2 | ShortLittleEndian |  | CompletedEntries; The count of completed entries. |
| 9 | 4 | IntegerLittleEndian |  | ItemsMoved; The count of items moved to the inventory. |
| 13 | 4 | IntegerLittleEndian |  | ZenCollected; The collected Zen. |
| 17 | 2 | ShortLittleEndian |  | RemainingEntries; The count of remaining entries. |
| 19 | 1 | Byte |  | KeptBack; Bit 0: inventory space kept something back; bit 1: the Zen limit kept something back. |

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