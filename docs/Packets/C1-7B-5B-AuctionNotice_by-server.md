# C1 7B 5B - AuctionNotice (by server)

## Is sent when

Sold or expired: while the seller is online, wherever the seller is. Deliveries waiting: when a character with collection entries enters the game.

## Causes the following actions on the client side

The client notifies the player.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   18   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0x7B  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x5B  | Packet header - sub packet type identifier |
| 4 | 1 | AuctionNoticeKind |  | Kind; The kind of the notice. |
| 5 | 2 | ShortLittleEndian |  | Type; The item type, group * 512 + number. 0 for deliveries waiting. |
| 7 | 1 | Byte |  | Level; The item level. 0 for deliveries waiting. |
| 8 | 4 | IntegerLittleEndian |  | Quantity; The quantity; 0 for deliveries waiting. |
| 12 | 4 | IntegerLittleEndian |  | Net; The net in Zen; 0 for deliveries waiting. |
| 16 | 2 | ShortLittleEndian |  | PendingCollection; The count of entries in the collection of the player. |

### AuctionNoticeKind Enum

Defines the kind of an auction notice.

| Value | Name | Description |
|-------|------|-------------|
| 1 | Sold | A listing sold; the proceeds are in the collection. |
| 2 | Expired | A listing expired; the units are in the collection. |
| 3 | DeliveriesWaiting | Deliveries are waiting in the collection. |
| 4 | SoldToInventory | A listing sold; the proceeds were added to the inventory Zen. |
| 5 | ExpiredToInventory | A listing expired; the units were returned to the inventory. |