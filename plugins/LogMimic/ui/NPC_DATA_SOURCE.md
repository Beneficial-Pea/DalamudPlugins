# English NPC name index

`BNpcNames.en.json.gz` contains English `BNpcName` row IDs and names from
[xivapi/ffxiv-datamining](https://github.com/xivapi/ffxiv-datamining).

Pinned source: https://github.com/xivapi/ffxiv-datamining/blob/52a72b38aa2f967cab36867f34cd9bee9debb88b/csv/en/BNpcName.csv

The file is a gzip-compressed JSON object mapping decimal row IDs to the nonempty
Singular column. The index is used to compare logged English NPC names against
live NPC NameId values. Display names come from the installed client's BNpcName
sheet. Multiple matching live NPCs require explicit binding.

FINAL FANTASY XIV game data and names belong to Square Enix. This index does not
contain runtime object IDs or player data.
