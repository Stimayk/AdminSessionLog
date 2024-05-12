using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using DiscordUtilitiesAPI;
using DiscordUtilitiesAPI.Builders;
using Newtonsoft.Json;

namespace AdminSessionLog
{
    public class AdminSessionLog : BasePlugin
    {
        public override string ModuleName => "AdminSessionLog";
        public override string ModuleVersion => "v1.0.1";
        public override string ModuleAuthor => "E!N";

        private IDiscordUtilitiesAPI? DiscordUtilities;
        private AdminSessionLogConfig? _config;

        private readonly Dictionary<ulong, DateTime> sessionStartTimes = [];
        private ulong channelId;
        public bool connect;
        private readonly string serverName = ConVar.Find("hostname")?.StringValue ?? "Unknown Server";

        public override void OnAllPluginsLoaded(bool hotReload)
        {
            string configDirectory = GetConfigDirectory();
            EnsureConfigDirectory(configDirectory);
            string configPath = Path.Combine(configDirectory, "AdminSessionLogConfig.json");
            _config = AdminSessionLogConfig.Load(configPath);
            Console.WriteLine($"{ModuleName} | Configuration loaded successfully. DiscordChannelId = {_config.DiscordChannelId}, AdminFlag = {string.Join(", ", _config.AdminFlag ?? [])}, AllowConnectMessage = {_config.AllowConnectMessage}");

            DiscordUtilities = new PluginCapability<IDiscordUtilitiesAPI>("discord_utilities").Get();

            if (DiscordUtilities == null)
            {
                Console.WriteLine($"{ModuleName} | Error: Discord Utilities are not available.");
            }
            else
            {
                InitializeDiscord();
            }
        }

        private static string GetConfigDirectory()
        {
            return Path.Combine(Server.GameDirectory, "csgo/addons/counterstrikesharp/configs/plugins/AdminSessionLog/");
        }

        private void EnsureConfigDirectory(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
                Console.WriteLine($"{ModuleName} | Created configuration directory at: {directoryPath}");
            }
        }

        private void InitializeDiscord()
        {
            if (_config == null)
            {
                Console.WriteLine($"{ModuleName} | Error: Configuration is not loaded.");
                return;
            }

            channelId = _config.DiscordChannelId;
            connect = _config.AllowConnectMessage;

            if (channelId == 0)
            {
                Console.WriteLine($"{ModuleName} | Error: Some configuration settings - ChannelID: {channelId}");
                return;
            }

            Console.WriteLine($"{ModuleName} | Initialized successfully.");
        }

        [GameEventHandler]
        public HookResult PlayerConnect(EventPlayerConnectFull @event, GameEventInfo info)
        {
            if (_config?.AdminFlag == null || @event.Userid == null)
            {
                Console.WriteLine($"{ModuleName} | Error: Admin flags are missing or Userid is null");
                return HookResult.Continue;
            }

            if (_config.AdminFlag.Any(flag => AdminManager.PlayerHasPermissions(@event.Userid, flag)))
            {
                sessionStartTimes[@event.Userid.SteamID] = DateTime.Now;

                if (connect)
                {
                    try
                    {
                        var embedBuilder = new Embeds.Builder
                        {
                            Title = Localizer["asl.TitleConnect"],
                            Description = Localizer["asl.Server", serverName],
                            Color = Localizer["asl.EmbedColorConnect"],
                        };

                        embedBuilder.Fields.Add(new Embeds.FieldsData
                        {
                            Title = Localizer["asl.Administrator"],
                            Description = Localizer["asl.AdministratorDescription", @event.Userid.PlayerName, @event.Userid.SteamID],
                            Inline = false
                        });

                        DiscordUtilities?.SendCustomMessageToChannel("admin_session_log", channelId, null, embedBuilder, null);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"{ModuleName} | Error sending message to Discord: {ex.Message}");
                    }
                }
            }

            return HookResult.Continue;
        }

        [GameEventHandler]
        public HookResult PlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
        {
            if (@event.Userid == null)
            {
                return HookResult.Continue;
            }
            if (sessionStartTimes.TryGetValue(@event.Userid.SteamID, out DateTime startTime))
            {
                TimeSpan sessionLength = DateTime.Now - startTime;
                string formattedLength = FormatSessionLength(sessionLength);
                try
                {
                    var embedBuilder = new Embeds.Builder
                    {
                        Title = Localizer["asl.TitleDisconnect"],
                        Description = Localizer["asl.Server", serverName],
                        Color = Localizer["asl.EmbedColorDisconnect"],
                    };

                    embedBuilder.Fields.Add(new Embeds.FieldsData
                    {
                        Title = Localizer["asl.Administrator"],
                        Description = Localizer["asl.AdministratorDescription", @event.Name, @event.Xuid],
                        Inline = true
                    });
                    embedBuilder.Fields.Add(new Embeds.FieldsData
                    {
                        Title = Localizer["asl.SessionLength"],
                        Description = formattedLength,
                        Inline = true
                    });

                    DiscordUtilities?.SendCustomMessageToChannel("admin_session_log", channelId, null, embedBuilder, null);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{ModuleName} | Error sending message to Discord: {ex.Message}");
                }
            }
            return HookResult.Continue;
        }

        private string FormatSessionLength(TimeSpan sessionLength)
        {
            int hours = sessionLength.Hours;
            int minutes = sessionLength.Minutes;
            int seconds = sessionLength.Seconds;

            string hoursText = hours > 0 ? $"{hours} " + GetLocalizedPluralForm(hours, "Hours") + " " : "";
            string minutesText = minutes > 0 ? $"{minutes} " + GetLocalizedPluralForm(minutes, "Minutes") + " " : "";
            string secondsText = seconds > 0 ? $"{seconds} " + GetLocalizedPluralForm(seconds, "Seconds") : "";

            string formattedLength = hoursText + minutesText + secondsText;
            return string.IsNullOrWhiteSpace(formattedLength) ? Localizer["asl.LessThanSecond"] : formattedLength.Trim();
        }

        private string GetLocalizedPluralForm(int number, string type)
        {
            number %= 100;
            if (number >= 11 && number <= 19)
            {
                return Localizer[$"asl.{type}.Many"];
            }
            int lastDigit = number % 10;
            return lastDigit switch
            {
                1 => (string)Localizer[$"asl.{type}.One"],
                2 or 3 or 4 => (string)Localizer[$"asl.{type}.Few"],
                _ => (string)Localizer[$"asl.{type}.Many"],
            };
        }

        [ConsoleCommand("css_aslreload", "Reload Config")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
        [RequiresPermissions("@css/root")]
        public void OnReloadConfigCommand(CCSPlayerController? player, CommandInfo commandInfo)
        {
            string configPath = Path.Combine(GetConfigDirectory(), "AdminSessionLogConfig.json");
            _config = AdminSessionLogConfig.Load(configPath);
            Console.WriteLine($"{ModuleName} | Configuration reloaded successfully. DiscordChannelId = {_config.DiscordChannelId}, AdminFlag = {string.Join(", ", _config.AdminFlag ?? [])}, AllowConnectMessage = {_config.AllowConnectMessage}");

            InitializeDiscord();
        }
    }

    public class AdminSessionLogConfig
    {
        public ulong DiscordChannelId { get; set; } = 0;
        public string[]? AdminFlag { get; set; } = ["@css/reservation",
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
                                                    "@css/root"];
        public bool AllowConnectMessage { get; set; } = true;

        public static AdminSessionLogConfig Load(string configPath)
        {
            if (!File.Exists(configPath))
            {
                AdminSessionLogConfig defaultConfig = new();
                File.WriteAllText(configPath, JsonConvert.SerializeObject(defaultConfig, Formatting.Indented));
                return defaultConfig;
            }

            string json = File.ReadAllText(configPath);
            return JsonConvert.DeserializeObject<AdminSessionLogConfig>(json) ?? new AdminSessionLogConfig();
        }
    }
}
