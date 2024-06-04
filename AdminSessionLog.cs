using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text;
using System.Text.Json.Serialization;

namespace AdminSessionLog
{
    public class AdminSessionLog : BasePlugin
    {
        public override string ModuleName => "AdminSessionLog";
        public override string ModuleVersion => "v1.1.0";
        public override string ModuleAuthor => "E!N";

        private AdminSessionLogConfig? _config;

        private readonly Dictionary<ulong, DateTime> sessionStartTimes = [];

        private readonly HttpClient _httpClient = new();

        public override void OnAllPluginsLoaded(bool hotReload)
        {
            string configDirectory = GetConfigDirectory();
            EnsureConfigDirectory(configDirectory);
            string configPath = Path.Combine(configDirectory, "AdminSessionLogConfig.json");
            _config = AdminSessionLogConfig.Load(configPath);
            Logger.LogInformation($"Configuration loaded successfully. DiscordWebhookUrl = {_config.DiscordWebhookUrl}, AdminFlag = {string.Join(", ", _config.AdminFlag ?? [])}, AllowConnectMessage = {_config.AllowConnectMessage}");
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
                Logger.LogInformation($"Created configuration directory at: {directoryPath}");
            }
        }

        [GameEventHandler]
        public HookResult PlayerConnect(EventPlayerConnectFull @event, GameEventInfo info)
        {
            if (_config?.AdminFlag == null || @event.Userid == null)
            {
                Logger.LogError($"Admin flags are missing or Userid is null");
                return HookResult.Continue;
            }
            if (_config.AdminFlag.Any(flag => AdminManager.PlayerHasPermissions(@event.Userid, flag)))
            {
                sessionStartTimes[@event.Userid.SteamID] = DateTime.Now;

                if (_config.AllowConnectMessage)
                {
                    SendDiscordMessage(CreateEmbedMessage(
                        Localizer["asl.TitleConnect"],
                        Localizer["asl.EmbedColorConnect"],
                        Localizer["asl.Administrator"],
                        Localizer["asl.AdministratorDescription", @event.Userid.PlayerName, @event.Userid.SteamID]
                    ));
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
                SendDiscordMessage(CreateEmbedMessage(
                    Localizer["asl.TitleDisconnect"],
                    Localizer["asl.EmbedColorDisconnect"],
                    Localizer["asl.Administrator"],
                    Localizer["asl.AdministratorDescription", @event.Name, @event.Xuid],
                    Localizer["asl.SessionLength"],
                    formattedLength
                ));
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

        private string CreateEmbedMessage(string title, string hexColor, string field1Title, string field1Description, string? field2Title = null, string? field2Description = null)
        {
            hexColor = hexColor.TrimStart('#');
            if (!int.TryParse(hexColor, System.Globalization.NumberStyles.HexNumber, null, out int colorInt))
            {
                Logger.LogError($"Ошибка преобразования цвета: {hexColor}");
                return string.Empty;
            }

            var fields = new List<Dictionary<string, object>>
            {
                new() {
                    { "name", field1Title },
                    { "value", field1Description },
                    { "inline", false }
                }
            };

            if (field2Title != null && field2Description != null)
            {
                fields.Add(new Dictionary<string, object>
                {
                    { "name", field2Title },
                    { "value", field2Description },
                    { "inline", true }
                });
            }

            var embed = new Dictionary<string, object>
            {
                { "title", title },
                { "color", colorInt },
                { "fields", fields }
            };

            string? hostname = ConVar.Find("hostname")?.StringValue;
            if (!string.IsNullOrEmpty(hostname))
            {
                embed["description"] = $"{Localizer["asl.Server", hostname]}";
            }

            return JsonConvert.SerializeObject(new { embeds = new[] { embed } }, Formatting.Indented);
        }

        private async void SendDiscordMessage(string jsonPayload)
        {
            try
            {
                if (_config!.DiscordWebhookUrl != null)
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, _config.DiscordWebhookUrl);
                    request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                    using var response = await _httpClient.SendAsync(request);

                    if (!response.IsSuccessStatusCode)
                    {
                        Logger.LogError($"Ошибка отправки сообщения в Discord: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ошибка отправки сообщения в Discord: {ex.Message}");
            }
        }

        [ConsoleCommand("css_aslreload", "Reload Config")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
        [RequiresPermissions("@css/root")]
        public void OnReloadConfigCommand(CCSPlayerController? player, CommandInfo commandInfo)
        {
            string configPath = Path.Combine(GetConfigDirectory(), "AdminSessionLogConfig.json");
            _config = AdminSessionLogConfig.Load(configPath);
            Logger.LogInformation($"Configuration reloaded successfully. DiscordWebhookUrl = {_config.DiscordWebhookUrl}, AdminFlag = {string.Join(", ", _config.AdminFlag ?? [])}, AllowConnectMessage = {_config.AllowConnectMessage}");
        }
    }

    public class AdminSessionLogConfig
    {
        [JsonPropertyName("DiscordWebhookUrl")]
        public string? DiscordWebhookUrl { get; set; }

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
