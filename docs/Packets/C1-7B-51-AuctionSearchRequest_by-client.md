# C1 7B 51 - AuctionSearchRequest (by client)

## Is sent when

The player searches the item listings of the auction house with the given filters, or turns the page of a search result.

## Causes the following actions on the server side

The server answers with an AuctionSearchResult with the same request id: the matching item listings of the requested page, sorted by price, and the total count of matches. Every request without an open broker dialog is answered with NotOpen.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |      | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0x7B  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x51  | Packet header - sub packet type identifier |
| 4 | 2 | ShortLittleEndian |  | RequestId; The request id, chosen by the client (1 to 65535, 0 is never sent); the answer echoes it. |
| 6 | 2 | ShortLittleEndian |  | Page; The requested page, starting at 0. |
| 8 | 1 | AuctionSortOrder |  | Sort; The sort order of the results. |
| 9 | 2 | ShortLittleEndian |  | SlotMask; The equipment slot kinds, 0 = any. Bit 0 weapon, 1 shield, 2 helm, 3 armor, 4 pants, 5 gloves, 6 boots, 7 wings, 8 pendant, 9 ring, 10 pet. |
| 11 | 2 | ShortLittleEndian |  | GroupMask; The native item groups, 0 = any. Bit n = item group n. |
| 13 | 1 | AuctionClassLine |  | ClassLine; The class line which must be able to use the item. |
| 14 | 1 | Byte |  | Flags; Bit 0: the requesting character can equip the item now, by the equip rules of the server; bit 1: luck required; bit 2: skill required. |
| 15 | 1 | AuctionItemFamily |  | Family; The item family. |
| 16 | 1 | Byte |  | UpgradeMin; The minimum item level, 0 to 15. |
| 17 | 1 | Byte |  | UpgradeMax; The maximum item level, 0 to 15, at least UpgradeMin. |
| 18 | 1 | Byte |  | ExcellentMask; The required excellent options, as bits of the excellent option byte of the item; every set bit is required. |
| 19 | 1 | Byte |  | MinExcellentCount; The minimum count of excellent options, 0 to 6. |
| 20 | 1 | Byte |  | MinSockets; The minimum count of sockets, 0 to 5. |
| 21 | 4 | IntegerLittleEndian |  | MaxPrice; The maximum price in Zen, 0 = none. |
| 25 | 1 | Byte |  | EntryCount; The count of entries, at most 64. |
| 26 | AuctionSearchEntry.Length * EntryCount | Array of AuctionSearchEntry |  | Entries; The item type entries: up to 48 name matches and up to 16 ancient set members. Both kinds given: both apply. |

### AuctionSearchEntry Structure

An item type entry of an auction search.

Length: 3 Bytes

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 2 | ShortLittleEndian |  | Type; The item type, group * 512 + number. |
| 2 | 1 | Byte |  | Discriminator; 0 = a name match, the type of the item must be one of these entries; 1 or 2 = an ancient set member, the item must be ancient with this discriminator and of one of these types. |

### AuctionSortOrder Enum

Defines the sort order of an auction search result.

| Value | Name | Description |
|-------|------|-------------|
| 0 | PriceAscending | Sorted by price, the cheapest first. |
| 1 | PriceDescending | Sorted by price, the most expensive first. |

### AuctionClassLine Enum

Defines the class line of an auction search: a base class, including its evolutions.

| Value | Name | Description |
|-------|------|-------------|
| 0 | DarkWizard | Dark Wizard. |
| 1 | DarkKnight | Dark Knight. |
| 2 | FairyElf | Fairy Elf. |
| 3 | MagicGladiator | Magic Gladiator. |
| 4 | DarkLord | Dark Lord. |
| 5 | Summoner | Summoner. |
| 6 | RageFighter | Rage Fighter. |
| 255 | Any | Any class line. |

### AuctionItemFamily Enum

Defines the item family of an auction search. It tests the item, so one item can match several families.

| Value | Name | Description |
|-------|------|-------------|
| 0 | Any | Any item. |
| 1 | Normal | An item without excellent option, not ancient, without socket. |
| 2 | Excellent | An item with any excellent option. |
| 3 | Ancient | An item of an ancient set. |
| 4 | Socket | An item with at least one socket. |