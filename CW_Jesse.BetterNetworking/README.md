Now compatible with Mistlands!

This mod **greatly improves multiplayer performance.** If you find this mod works as advertised, please endorse it!

**Default settings should be fine for most people.**

_Settings (simple)_

* If your lag is fixed, change nothing. You're good to go. Have fun!
* If NPCs are skipping, reduce everyone's update rate to 75% and increase their queue size to 48 KB.
* If a player is skipping, reduce their queue size to 32 KB. If they're already at 32 KB, reduce their update rate.
* If your server bandwidth is fantastic, try increasing its queue size to 80 KB.

_Settings (advanced)_

* Queue size increase: Sends more data, but deprioritizes "important" objects and can overload a player's internet.
* Update rate reduction: Sends data less often; if a person's internet is overloaded, this adds a little bit of lag in exchange for getting rid of a lot of lag.
* Queue size->32 KB / update rate->100%: up to 256 KB/s per player. [Default]
* Queue size->80 KB / update rate->100%: up to 640 KB/s per player.
* Queue size->32 KB / update rate->50%: up to 128 KB/s per player.

_Useful information about Valheim_

* ALWAYS quit the game using the in-game menu (NOT Alt+F4, closing console, etc.) Doing otherwise can cause issues. This is a problem with vanilla Valheim.
* The player who arrives in an area first is responsible for lag there. Ideally, the people with the best internet should be ahead of the pack or first through a portal, at least until Better Networking 3.0.
* Valheim multiplayer performance appears to better with crossplay disabled.

_Keep in mind_

* Compression only works between computers with Better Networking.
* Likely incompatible with (and better than) other networking mods.
* Dedicated servers will not apply changes until restarted.
* You must use the join code or *external* IP when connecting to servers on the same network when using crossplay.

_Features_

* crossplay enabled: improves compression speed (55x faster) and compression ratio
* crossplay disabled: adds network compression
* new connection buffer (AKA "ZDO buffer", prevents data loss)
* change outgoing queue size
* change outgoing update rate
* change min/max send rates in Steamworks (AKA crossplay disabled)
* dedicated servers: ability to enable/disable crossplay through Better Networking
* compatible with Linux
* compatible with players not running the mod

_Better Networking links_

* Please [provide feedback!](https://www.nexusmods.com/valheim/mods/1570?tab=posts)
* Please [examine my code!](https://github.com/CW-Jesse/valheim-betternetworking)
* Please [report bugs!](https://github.com/CW-Jesse/valheim-betternetworking/issues)
* Please [endorse on Nexus Mods!](https://www.nexusmods.com/valheim/mods/1570)
* Please [endorse on ThunderStore!](https://valheim.thunderstore.io/package/CW_Jesse/BetterNetworking_Valheim/)

_Educational links_

* [Technical explanation of Valheim's networking](https://redd.it/mga1iw) by u/SleepMyLittleOnes
* [Technical information and discussions on desync/lag in Valheim](https://jamesachambers.com/revisiting-fixing-valheim-lag-modifying-send-receive-limits/) on the blog of James A. Chambers

_Thank you_

* [Joshua Woods (Cheb)](https://github.com/jpw1991) for [contributing code](https://github.com/CW-Jesse/valheim-betternetworking/pull/17) (Check out ChebsNecromancy!)
* [William Seligmann (jsza)](https://github.com/jsza) for [contributing code](https://github.com/CW-Jesse/valheim-betternetworking/pull/19)
* nightshade221 (Max) for being a patron and providing a community!
* Frostsorrow for QA and community assistance!
* Tquilarius for being a patron!
* ALo, ErDu, Tatsuya009, Alexander3054, Dskaggsgv, vi0lation, Ersan191, WhiteDingoX5, xdhara, ImpulsiveMind, nevcairiel, and doubletroubleswtor for valuable feedback and troubleshooting!
* [Oleg Stepanischev](https://github.com/oleg-st) for [ZstdSharp](https://github.com/oleg-st/ZstdSharp)
* [Smoothbrain](https://valheim.thunderstore.io/package/Smoothbrain/) for [Network](https://valheim.thunderstore.io/package/Smoothbrain/Network/)

**If you've benefited from this mod and want to see more features implemented, please endorse it! Or if you're feeling particularly generous, donate here or [on Patreon](https://www.patreon.com/CW_Jesse)!**