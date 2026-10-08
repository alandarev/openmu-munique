# C1 7B 50 - AuctionHouseState (by server)

## Is sent when

Open: instead of the NPC window response, when a player talks to an NPC with the auction-house window, and again after a RulesChanged answer. Closed: whenever the broker dialog ends (the close request of the client, warp, death, logout, another dialog).

## Causes the following actions on the client side

Open: the client shows the auction house window with these rules. Closed: the client closes the auction house window.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   33   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0x7B  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x50  | Packet header - sub packet type identifier |
| 4 | 1 | AuctionHouseWindowState |  | State; The state of the auction house window. When closed, all following fields are 0. |
| 5 | 1 | Byte |  | PageSize; The count of rows of a page. |
| 6 | 1 | Byte |  | MaxActiveListings; The maximum count of active listings of a seller. |
| 7 | 1 | Byte |  | ActiveListings; The count of active listings of the player. |
| 8 | 2 | ShortLittleEndian |  | PendingCollection; The count of entries in the collection of the player. |
| 10 | 4 | IntegerLittleEndian |  | ZenLimit; The Zen limit of the inventory, at most 2,147,483,647. |
| 14 | 4 | IntegerLittleEndian |  | MaxPrice; The maximum price or total in Zen. |
| 18 | 4 | IntegerLittleEndian |  | PostingFeeFlat; The flat part of the posting fee in Zen; 0 is allowed. |
| 22 | 2 | ShortLittleEndian |  | PostingFeeBasisPoints; The posting fee in basis points of quantity * unit price. |
| 24 | 2 | ShortLittleEndian |  | SaleFeeBasisPoints; The sale fee in basis points of the gross of a sale. |
| 26 | 1 | Byte |  | Flags; Bit 0: the posting fee is refunded on cancel. |
| 27 | 1 | Byte |  | DurationCount; The count of offered durations, 1 to 4. |
| 28 | 4 | Binary |  | Durations; The offered listing durations in hours; unused ones are 0. |
| 32 | 1 | Byte |  | PriceDigits; The count of significant digits which every listing price (goods unit price and item price) may have; 0 = any. |

### AuctionHouseWindowState Enum

Defines the state of the auction house window.

| Value | Name | Description |
|-------|------|-------------|
| 0 | Closed | The window is closed. |
| 1 | Open | The window is open. |