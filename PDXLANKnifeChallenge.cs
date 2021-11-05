/* PDXLANKnifeChallenge.cs
 * by [E4GL] H3dius https://github.com/Hedius
 *
 * Licensed under the GPLv3
 *
 * Credits:
 * Discord report posting by jbrunink
 * AdKats by ColColonCleaner for many ideas :) (especially the settings handling)
 * 
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using PRoCon.Core;
using PRoCon.Core.Events;
using PRoCon.Core.Players;
using PRoCon.Core.Plugin;

namespace PRoConEvents {
    //Aliases
    using CapturableEvent = CapturableEvents;

    public class Player {
        public bool IsVip = false;
        public bool IsWhitelisted = false;
        public string SoldierName = "";
    }

    public class PDXLANKnifeChallenge : PRoConPluginAPI, IPRoConPluginInterface {
        public enum MessageType {
            Warning,
            Error,
            Exception,
            Normal
        }


        public PDXLANKnifeChallenge() {
            _isEnabled = false;

            AddSettingSection("0", "General");
            AddSettingSection("1", "Player Management");
            AddSettingSection("2", "Weapon Limitations");
            AddSettingSection("3", "Punishment");
            AddSettingSection("4", "Announcements");
            AddSettingSection("5", "Controller");
        }


        public string GetPluginName() {
            return "PDXLAN - Knife Challenge";
        }

        public string GetPluginVersion() {
            return "0.0.0.1";
        }

        public string GetPluginAuthor() {
            return "Hedius";
        }

        public string GetPluginWebsite() {
            return "http://github.com/Hedius/PDXLAN-Knife-Challenge";
        }

        public string GetPluginDescription() {
            return @"
		<h1>Your Title Here</h1>
		<p>TBD</p>
		
		<h2>Description</h2>
		<p>TBD</p>
		
		<h2>Commands</h2>
		<p>TBD</p>
		
		<h2>Settings</h2>
		<p>TBD</p>
		
		<h2>Development</h2>
		<p>TBD</p>
		<h3>Changelog</h3>
		<blockquote><h4>1.0.0.0 (15-SEP-2012)</h4>
			- initial version<br/>
		</blockquote>
		";
        }

        public List<CPluginVariable> GetDisplayPluginVariables() {
            var lstReturn = GetPluginVariables();

            // 5 Controller
            lstReturn.Add(new CPluginVariable(GetSettingSection("5") + T + "Current Status (Display Only)", _curStatus.GetType(), _curStatus));
            lstReturn.Add(new CPluginVariable(GetSettingSection("5") + T + "Current VIP (Display Only)", _curVip.GetType(), _curVip));
            lstReturn.Add(new CPluginVariable(GetSettingSection("5") + T + "Start Round", _startRound.GetType(), _startRound));
            lstReturn.Add(new CPluginVariable(GetSettingSection("5") + T + "Reset Round", _resetRound.GetType(), _resetRound));

            return lstReturn;
        }

        public List<CPluginVariable> GetPluginVariables() {
            var lstReturn = new List<CPluginVariable>();

            // 0 General
            lstReturn.Add(new CPluginVariable(GetSettingSection("0") + T + "Debug level", _debugLevel.GetType(), _debugLevel));

            // 1 Player Management
            lstReturn.Add(new CPluginVariable(GetSettingSection("1") + T + "Whitelist", typeof(string[]), _whitelistedPlayers.ToArray()));
            lstReturn.Add(new CPluginVariable(GetSettingSection("1") + T + "Automatically select VIP from Whitelist", _automaticallySelectVIP.GetType(), _automaticallySelectVIP));
            if (!_automaticallySelectVIP)
                lstReturn.Add(new CPluginVariable(GetSettingSection("1") + T + "VIP", _vip.GetType(), _vip));

            // 2 Weapons
            lstReturn.Add(new CPluginVariable(GetSettingSection("2") + T + "Allowed weapons for everyone", _regexAllowedWeaponsEveryone.GetType(), _regexAllowedWeaponsEveryone));
            lstReturn.Add(new CPluginVariable(GetSettingSection("2") + T + "Allowed weapons for whitelisted players", _regexAllowedWeaponsWhitelist.GetType(), _regexAllowedWeaponsWhitelist));

            // 3 Punishments
            lstReturn.Add(new CPluginVariable(GetSettingSection("3") + T + "Warnings (kills) before punishment", _chances.GetType(), _chances));

            // 4 Announcements
            lstReturn.Add(new CPluginVariable(GetSettingSection("4") + T + "Enable Discord Webhook", _enableDiscordWebhook.GetType(), _enableDiscordWebhook));
            if (_enableDiscordWebhook) {
                lstReturn.Add(new CPluginVariable(GetSettingSection("4") + T + "Author", _webhookAuthor.GetType(), _webhookAuthor));
                lstReturn.Add(new CPluginVariable(GetSettingSection("4") + T + "Webhook URL", _webhookURL.GetType(), _webhookURL));
            }

            return lstReturn;
        }

        public void SetPluginVariable(string strVariable, string strValue) {
            if (strVariable.CompareTo("Debug level") == 0) {
                var tmp = 2;
                int.TryParse(strValue, out tmp);
                _debugLevel = tmp;
            }
            else if (strVariable.CompareTo("Whitelist") == 0) {
                _whitelistedPlayers = new List<string>(CPluginVariable.DecodeStringArray(strValue));
            }
            else if (strVariable.CompareTo("Automatically select VIP from Whitelist") == 0) {
                _automaticallySelectVIP = bool.Parse(strValue);
                if (_automaticallySelectVIP)
                    ConsoleWrite("The plugin will now automatically choose a random player from the whitelist as soon as the round starts!");
                else
                    ConsoleWrite("Switching to manually defined VIP!");
            }
            else if (strVariable.CompareTo("VIP") == 0) {
                if (strValue.Length == 0)
                    ConsoleError("Cannot set VIP to an empty string!");
                _vip = strValue;
            }
            else if (strVariable.CompareTo("Allowed weapons for everyone") == 0) {
                if (strValue.Length == 0)
                    ConsoleError("Cannot set REGEX to an empty string!");
                _regexAllowedWeaponsEveryone = strValue;
            }
            else if (strVariable.CompareTo("Allowed weapons for whitelisted players") == 0) {
                if (strValue.Length == 0)
                    ConsoleError("Cannot set REGEX to an empty string!");
                _regexAllowedWeaponsWhitelist = strValue;
            }
            else if (strVariable.CompareTo("Warnings (kills) before punishment") == 0) {
                var value = int.Parse(strValue);
                if (value >= 0)
                    _chances = value;
            }
            else if (strVariable.CompareTo("Enable Discord Webhook") == 0) {
                _enableDiscordWebhook = bool.Parse(strValue);
            }
            else if (strVariable.CompareTo("Author") == 0) {
                if (strValue.Length <= 2)
                    ConsoleError("Invalid author name. (Too short)");
                _webhookAuthor = strValue;
            }
            else if (strVariable.CompareTo("Webhook URL") == 0) {
                if (!strValue.Contains("https://discord") && strValue.CompareTo(string.Empty) != 0) {
                    ConsoleError("^b" + strValue + "^n is not a valid Discord webhook, was set to " + strValue + ", corrected to " + _webhookURL);
                    return;
                }
                _webhookURL = strValue;
            }
            else if (strVariable.CompareTo("Start Round") == 0) {
                var action = bool.Parse(strValue);
                if (action && !_resetRound && !_isRunning)
                    _startRound = true;
                    // ToDo CALL START ROUTINE // TRIGGER THREAD
            }
            else if (strVariable.CompareTo("Reset Round") == 0) {
                var action = bool.Parse(strValue);
                if (action && !_startRound)
                    _resetRound = true;
                    // ToDo CALL RESET ROUTINE // TRIGGER THREAD
            }
        }


        public void OnPluginLoaded(string strHostName, string strPort, string strPRoConVersion) {
            RegisterEvents(GetType().Name, "OnVersion", "OnServerInfo", "OnResponseError", "OnListPlayers", "OnPlayerJoin", "OnPlayerLeft", "OnPlayerKilled", "OnPlayerSpawned", "OnPlayerTeamChange", "OnGlobalChat", "OnTeamChat", "OnSquadChat", "OnRoundOverPlayers", "OnRoundOver", "OnRoundOverTeamScores", "OnLoadingLevel", "OnLevelStarted", "OnLevelLoaded");
        }

        public void OnPluginEnable() {
            _isEnabled = true;
            ConsoleWrite("Enabled!");
        }

        public void OnPluginDisable() {
            _isEnabled = false;
            ConsoleWrite("Disabled!");
        }

        public string FormatMessage(string msg, MessageType type) {
            var prefix = "[^b" + GetPluginName() + "^n] ";

            if (type.Equals(MessageType.Warning))
                prefix += "^1^bWARNING^0^n: ";
            else if (type.Equals(MessageType.Error))
                prefix += "^1^bERROR^0^n: ";
            else if (type.Equals(MessageType.Exception))
                prefix += "^1^bEXCEPTION^0^n: ";

            return prefix + msg;
        }


        public void LogWrite(string msg) {
            ExecuteCommand("procon.protected.pluginconsole.write", msg);
        }

        public void ConsoleWrite(string msg, MessageType type) {
            LogWrite(FormatMessage(msg, type));
        }

        public void ConsoleWrite(string msg) {
            ConsoleWrite(msg, MessageType.Normal);
        }

        public void ConsoleWarn(string msg) {
            ConsoleWrite(msg, MessageType.Warning);
        }

        public void ConsoleError(string msg) {
            ConsoleWrite(msg, MessageType.Error);
        }

        public void ConsoleException(string msg) {
            ConsoleWrite(msg, MessageType.Exception);
        }

        public void DebugWrite(string msg, int level) {
            if (_debugLevel >= level) ConsoleWrite(msg, MessageType.Normal);
        }


        public void ServerCommand(params string[] args) {
            var list = new List<string>();
            list.Add("procon.protected.send");
            list.AddRange(args);
            ExecuteCommand(list.ToArray());
        }

        private string AddSettingSection(string number, string desc) {
            _SettingSections[number] = desc;
            return GetSettingSection(number);
        }

        private string GetSettingSection(string number) {
            return number + ". " + _SettingSections[number];
        }


        public override void OnVersion(string serverType, string version) {
        }

        public override void OnServerInfo(CServerInfo serverInfo) {
            // ConsoleWrite("Debug level = " + _debugLevel);
        }

        public override void OnResponseError(List<string> requestWords, string error) {
        }

        public override void OnListPlayers(List<CPlayerInfo> players, CPlayerSubset subset) {
            DebugWrite("Entering OnListPlayers", 4);
            var vipFound = false;
            foreach (var player in players)
                if (player.SoldierName.ToLower().CompareTo(_vip.ToLower()) == 0) {
                    vipFound = true;
                    if (_curVip.CompareTo(player.SoldierName) != 0)
                        _curVip = player.SoldierName;
                    break;
                }

            if (!vipFound && _isEnabled)
                ConsoleWarn("VIP " + _vip + " is not online!");
            DebugWrite("Exiting OnListPlayers", 4);
        }

        public override void OnPlayerJoin(string soldierName) {
            if (soldierName.ToLower().CompareTo(_vip.ToLower()) == 0) {
                ConsoleWarn("VIP " + _vip + " has joined the server!");
                _curVip = soldierName;
            }
        }

        public override void OnPlayerLeft(CPlayerInfo playerInfo) {
            if (playerInfo.SoldierName.ToLower().CompareTo(_vip.ToLower()) == 0)
                ConsoleWarn("VIP " + _vip + " has left the server!!! Choose a new VIP!");
            _curVip = "NONE!!! SET A NEW VIP";
        }

        public override void OnPlayerKilled(Kill kKillerVictimDetails) {
        }

        public override void OnPlayerSpawned(string soldierName, Inventory spawnedInventory) {
        }

        public override void OnPlayerTeamChange(string soldierName, int teamId, int squadId) {
        }

        public override void OnGlobalChat(string speaker, string message) {
        }

        public override void OnTeamChat(string speaker, string message, int teamId) {
        }

        public override void OnSquadChat(string speaker, string message, int teamId, int squadId) {
        }

        public override void OnRoundOverPlayers(List<CPlayerInfo> players) {
        }

        public override void OnRoundOverTeamScores(List<TeamScore> teamScores) {
        }

        public override void OnRoundOver(int winningTeamId) {
        }

        public override void OnLoadingLevel(string mapFileName, int roundsPlayed, int roundsTotal) {
        }

        public override void OnLevelStarted() {
        }

        public override void OnLevelLoaded(string mapFileName, string Gamemode, int roundsPlayed, int roundsTotal) {
        } // BF3

        /* Inherited:
            this.PunkbusterPlayerInfoList = new Dictionary<String, CPunkbusterInfo>();
            this.FrostbitePlayerInfoList = new Dictionary<String, CPlayerInfo>();
        */

        #region members

        protected string Version = "0.0.1";
        private bool _isEnabled;

        // Section 1 - General
        private int _debugLevel = 2;

        // Section 2 - Players
        private List<string> _whitelistedPlayers = new List<string>();
        private bool _automaticallySelectVIP;
        private string _vip = "Player1";

        // Section 3 - Weapons
        private string _regexAllowedWeaponsEveryone = "Melee|BallisticShield";
        private string _regexAllowedWeaponsWhitelist = "ABC|ABC|ABC";

        // Section 4 - Punishment
        // chances -> 2 kills -> kick -> tban 30 -> ban
        // only affects the amount of kills (warnings)
        private int _chances = 2;

        // Section 5 - Announcements
        private bool _enableDiscordWebhook;
        private string _webhookAuthor = "PDXLAN";
        private string _webhookURL = "https://discord.com/api/webhooks/xyz";

        // Section x - messages?

        // Section 6 - Controller
        private bool _isRunning = false;
        private readonly string _curStatus = "Not Running";
        private string _curVip = "None";
        private bool _startRound = false;
        private bool _resetRound = false;

        // MISC
        //Settings display
        private readonly Dictionary<string, string> _SettingSections = new Dictionary<string, string>();

        private List<Player> _players = new List<Player>();

        private const string T = "|";

        # endregion
    } // end PDXLANKnifeChallenge

    // Extension: Discord Hook
    public class DiscordWebhook {
        public string author;
        public string avatar;
        public int colour;
        private readonly PDXLANKnifeChallenge plugin;
        public string URL;

        public bool useCustomAvatar;

        public DiscordWebhook(PDXLANKnifeChallenge plugin, string URL, string author, string avatar, int colour, bool useCustomAvatar) {
            this.plugin = plugin;
            this.URL = URL;
            this.author = author;
            this.avatar = avatar;
            this.colour = colour;
            this.useCustomAvatar = useCustomAvatar;
        }

        public void ThreadSendNotification(string title, string content) {
            var discordWebhookThread = new Thread(delegate() { SendNotification(title, content); });
            discordWebhookThread.IsBackground = true;
            discordWebhookThread.Name = "DiscordWebhookTrhead";
            discordWebhookThread.Start();
        }

        public void SendNotification(string title, string content) {
            if (title == null || content == null) {
                plugin.ConsoleError("Unable to send FailLog to Discord. Title/Content empty.");
                return;
            }

            // POST Body
            // Doc: https://discordapp.com/developers/docs/resources/channel#embed-object
            var embed = new Hashtable {
                { "title", title },
                { "description", content },
                { "color", colour },
                { "timestamp", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ") }
            };
            var embeds = new ArrayList {
                embed
            };

            var jsonTable = new Hashtable();
            jsonTable["username"] = author;
            jsonTable["embeds"] = embeds;

            if (useCustomAvatar)
                jsonTable["avatar_url"] = avatar;

            var jsonBody = JSON.JsonEncode(jsonTable);

            // Send request
            post(jsonBody);
        }

        public void post(string jsonBody) {
            try {
                if (string.IsNullOrEmpty(URL)) {
                    plugin.ConsoleError("Discord WebHook URL empty! Unable to post message.");
                    return;
                }

                if (string.IsNullOrEmpty(jsonBody)) {
                    plugin.ConsoleError("Discord JSON body empty! Unable to post message.");
                    return;
                }

                var request = WebRequest.Create(URL);
                request.Method = "POST";
                request.ContentType = "application/json";
                var byteArray = Encoding.UTF8.GetBytes(jsonBody);
                request.ContentLength = byteArray.Length;
                var requestStream = request.GetRequestStream();
                requestStream.Write(byteArray, 0, byteArray.Length);
                requestStream.Close();
            }
            catch (WebException e) {
                var response = e.Response;
                plugin.ConsoleError("Discord Webhook notification failed: " + new StreamReader(response.GetResponseStream()).ReadToEnd());
                plugin.ConsoleException(e.ToString());
            }
            catch (Exception e) {
                plugin.ConsoleError("Error while posting to Discord WebHook.");
                plugin.ConsoleException(e.ToString());
            }
        }
    }
} // end namespace PRoConEvents