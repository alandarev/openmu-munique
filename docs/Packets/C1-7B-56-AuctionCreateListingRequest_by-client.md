# C1 7B 56 - AuctionCreateListingRequest (by client)

## Is sent when

The player lists an item or a quantity of goods of the inventory in the auction house.

## Causes the following actions on the server side

The server removes the listed units from the inventory, charges the posting fee and answers with an AuctionCreateListingResult with the same request id; or it refuses and changes nothing. Every request without an open broker dialog is answered with NotOpen.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   20   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0x7B  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x56  | Packet header - sub packet type identifier |
| 4 | 2 | ShortLittleEndian |  | RequestId; The request id, chosen by the client (1 to 65535, 0 is never sent); the answer echoes it. |
| 6 | 1 | Byte |  | InventorySlot; The inventory slot of the item; for goods, the identical loose units are taken starting with this slot. |
| 7 | 4 | IntegerLittleEndian |  | Quantity; The quantity, 1 for an item listing. |
| 11 | 4 | IntegerLittleEndian |  | UnitPrice; The price in Zen of one unit. |
| 15 | 1 | Byte |  | DurationHours; The duration in hours, one of the announced durations. |
| 16 | 4 | IntegerLittleEndian |  | ExpectedPostingFee; The posting fee in Zen which the player expects to pay. |