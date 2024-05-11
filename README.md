# AdminSessionLog
**Allows you to track the game duration of administrators and display it in Discord**

![image](https://github.com/Stimayk/AdminSessionLog/assets/51941742/fd98ae66-5100-4836-accb-e74265a02a62)

Features:
+ Specifying the flags by which an admin will be tracked
+ Translation support
+ Enable/disable connection message
+ Numerals declension rules are taken into account (at least in Russian).

Configuration file:
```
{
  "DiscordChannelId": 685796043270389843,
  "AdminFlag": [
    "@css/reservation",
    "@css/generic",
    "@css/kick",
    "@css/ban",
    "@css/unban",
    "@css/vip",
    "@css/slay",
    "@css/changemap",
    "@css/cvar",
    "@css/config",
    "@css/chat",
    "@css/vote",
    "@css/password",
    "@css/rcon",
    "@css/cheats",
    "@css/root"
  ],
  "AllowConnectMessage": true
}
```
