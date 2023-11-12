### 2.3.2
- compatibility with Valheim 0.217.28
### 2.3.1
- compatibility with Valheim 0.216.9
### 2.3.0
- allow changing player limit for dedicated servers
### 2.2.1
- bugfix: more stable when some people have compression/BN disabled
- bugfix: no longer spams log when unexpected messages received
### 2.2.0
- PlayFab: fixed failing to work in some cases
- PlayFab: better handling of disconnections
- PlayFab: simplified compression code
- PlayFab: fixed error in vanilla Valheim where data could be lost while connecting
- Steamworks: fixed failing to work in some cases
### 2.1.3
- reverted to 2.1.1; for now, avoid using Alt+F4 to close the game
### 2.1.2
- better handling of unexpected disconnections
### 2.1.1
- improved startup time
- better config documentation
### 2.1.0
- buffer new connections ("ZDO buffer")
- better default queue size
- better queue size options
- PlayFab: compression ratio info messages
- Steamworks: simplified compression code (less likely to break in future Valheim patches)
- Steamworks: higher maximum send rate
- force crossplay option only shows for dedicated servers
- bugfix: queue size changes apply to PlayFab
- bugfix: can no longer set queue size too high for Steamworks
- bugfix: compression/update rate once again changeable without restart
### 2.0.4
- lower file size
- fixed incompatibility with mods that read/write from current directory
- bugfix: Linux supported again
### 2.0.2
- crossplay no longer forced enabled by default
- option to force crossplay disabled
- fixed misleading severity of some log messages
### 2.0.1
- increased default queue size (might prevent desyncs)
- increased max queue size (might induce lag - enable at own risk)
### 2.0.0
- now compatible with Mistlands
- now supports new PlayFab (crossplay) networking stack
- improved speed and effectiveness of network compression
- now forces PlayFab (crossplay) by default (can be disabled in config file)
### 1.2.2
- bugfix: compression handshake wasn't completing properly
- many small improvements
### 1.2.1
- breaks down multiple large packages into separate messages
- can now disable non-error/non-warning log messages
- fewer config options (based on feedback/testing)
- better config descriptions
### 1.2.0
- compression now optional
- defaults changed
- better log messages
- bugfix: certain messages could ignore increased queue size
- bugfix: can no longer set minimum send rate to 0
### 1.1.3
- increased send buffer size
- higher default network queue size and queue size options (made reasonable by compression)
- better default options
- better option descriptions
- possible fix for unusual servers