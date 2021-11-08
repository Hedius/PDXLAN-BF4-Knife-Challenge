/* PDXLANKnifeChallenge.cs
 *
 *  Copyright (C) 2021 [E4GL] H3dius https://github.com/Hedius
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with this program.  If not, see <https://www.gnu.org/licenses/>
 *
 * Credits:
 * Discord report posting by jbrunink
 * AdKats by ColColonCleaner for many ideas :) (especially the settings handling)
 *  - threading
 *  - discord
 * InsaneLimits by PapaCharlie for the messaging functions, weapon detection.
 * BasicPlugin by PapaCharlie.
 * 
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using PRoCon.Core;
using PRoCon.Core.Events;
using PRoCon.Core.Players;
using PRoCon.Core.Players.Items;
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
            return Version;
        }

        public string GetPluginAuthor() {
            return "Hedius";
        }

        public string GetPluginWebsite() {
            return "http://github.com/Hedius/PDXLAN-Knife-Challenge";
        }

        public string GetPluginDescription() {
            return @"
		<h2>Description</h2>
        <p>This plugin implements a weapon challenge for events.</p>
        <p>You can define allowed weapons, whitelisted players and a VIP. Whitelisted players have to protect the VIP from 
        all other players.</p> 
        <br />
		
		<h2>Commands</h2>
		<p>Prefixes: ! @ /</p>
        <p>A PRoCon account with a 100% name match is needed for using the accounts.</p>

        <p>
            <blockquote>
                <h4>!start</h4>
                Start the challenge. <br/>
            </blockquote>
        </p>
        <p>
            <blockquote>
                <h4>!stop</h4>
                Stop the challenge. <br/>
            </blockquote>
        </p>
		
		<h2>Settings</h2>
        <h4>Debug Level</h4>
        <p>Default 0. Use 2 for logging the plugin events.</p>
	    <br/>
	    <h4>Debug Mode (No Action)</h4>
        <p>The Plugin will not issue admin actions if this option is activated.</p>
        <br/>
        <h4>Whitelist</h4>
        <p>Whitelisted players have to protect the VIP and are allowed to use additional weapons.</p>
        <br/>
        <h4>VIP</h4>
        <p>The VIP.</p>
        <br/>
        <h4>Allowed weapons for everyone</h4>
        <p>REGEX or expression containing the allowed weapon codes. All players are allowed to use these weapons.</p>
        <br/>
        <h4>Allowed weapons for whitelisted players</h4>
        <p>REGEX or expression containing allowed weapons for whitelisted players. Whitelisted players are allowed to use weapons from both lists.</p>
        <br/>
        <h4>Warnings (kills) before punishment</h4>
        <p>How many kills should the plugin issue before kicking/banning players for violations? Default: 2</p>
        <br/>
        <h4>Temp-Ban duration (Minutes)</h4>
        <p>Duration of temp-bans in minutes. Default: 30</p>
        <br/>
        <h4>Enable Discord Webhook</h4>
        <p>Enable discord notifications.</p>
        <br/>
        <h4>Author</h4>
        <p>WebHook Author. Default: PDXLAN</p>
        <br/>
        <h4>WebHook URL</h4>
        <p>WebHook URL</p>
        <br/>
        <h4>Current Status (Display Only)</h4>
        <p>The current round status.</p>
        <br/>
        <h4>Current VIP (Display Only)</h4>
        <p>The current VIP.</p>
        <br/>       
	    <h4>Start Round</h4>
        <p>Start the round.</p>
        <br/>       	
		<h4>Reset Round</h4>
        <p>Reset/cancel the round.</p>
        <br/>       		
		
		<h2>Credits</h2>
		<p>AdKats, and InsaneLimits for several ideas. (Discord, weapon handling, threading, settings handling)</p>
		
        <h2>License</h2>
        <p>GPLv3</p>
        
		<h2>Changelog</h3>
		<blockquote><h4>1.0.1 (08-NOV-2021)</h4>
			- fix typo in log msgs<br/>
            - fix auto settings updates <br/>
		</blockquote>
		<blockquote><h4>1.0.0 (07-NOV-2021)</h4>
			- initial version<br/>
		</blockquote>
		";
        }

        public void OnPluginLoaded(string strHostName, string strPort, string strPRoConVersion) {
            RegisterEvents(GetType().Name, "OnServerInfo", "OnResponseError", "OnListPlayers", "OnPlayerJoin", "OnPlayerLeft", "OnPlayerKilled", "OnPlayerSpawned", "OnPlayerTeamChange", "OnGlobalChat", "OnTeamChat", "OnSquadChat", "OnRoundOverPlayers", "OnRoundOver", "OnRoundOverTeamScores", "OnLoadingLevel", "OnLevelStarted", "OnLevelLoaded");
        }

        public void OnPluginEnable() {
            _isEnabled = true;
            InitWeapons();

            _killProcessor = new Thread(KillProcessingLoop);
            _killProcessor.IsBackground = true;
            _killProcessor.Start();

            ConsoleWrite("Enabled!");
        }

        public void OnPluginDisable() {
            _isEnabled = false;

            // if (_killProcessor != null)
            //    _killProcessor.Abort();

            _isRunning = false;
            SetStatus("Not Running");
            _punishments.Clear();
            ConsoleWrite("Disabled!");
        }

        public void OnPluginLoadingEnv(List<string> lstPluginEnv) {
            foreach (var env in lstPluginEnv)
                DebugWrite("Got ^bOnPluginLoadingEnv: " + env, 8);
            _gameVersion = lstPluginEnv[1];
            ConsoleWrite("^2Game Version = " + lstPluginEnv[1]);
        }

        #region Commands

        public void HandleChat(string speaker, string message) {
            if (!_isEnabled || speaker.CompareTo("Server") == 0)
                return;

            var start = Regex.Match(message, @"^[!/@]start", RegexOptions.IgnoreCase).Success;
            var stop = Regex.Match(message, @"^[!/@]stop", RegexOptions.IgnoreCase).Success;
            var p = GetAccountPrivileges(speaker);

            if (p == null && (start || stop)) {
                PlayerSay(speaker, "You are not allowed to use this command!");
                return;
            }

            if (start)
                StartChallenge();
            else if (stop)
                ResetChallenge();
        }

        #endregion

        #region Settings

        public void UpdateSettingsPage() {
            // from AdKats
            ExecuteCommand("procon.protected.plugins.setVariable", "PDXLANKnifeChallenge", "UpdateSettings", "Update");
        }

        private string GetSettingSection(string number) {
            // From AdKats
            return number + ". " + _settingSections[number];
        }

        private string AddSettingSection(string number, string desc) {
            // From AdKats
            _settingSections[number] = desc;
            return GetSettingSection(number);
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
            lstReturn.Add(new CPluginVariable(GetSettingSection("0") + T + "Debug Mode (No Action)", _debugMode.GetType(), _debugMode));

            // 1 Player Management
            lstReturn.Add(new CPluginVariable(GetSettingSection("1") + T + "Whitelist", typeof(string[]), _whitelistedPlayers.ToArray()));
            if (!_automaticallySelectVip)
                lstReturn.Add(new CPluginVariable(GetSettingSection("1") + T + "VIP", _vip.GetType(), _vip));

            // 2 Weapons
            lstReturn.Add(new CPluginVariable(GetSettingSection("2") + T + "Allowed weapons for everyone", _regexAllowedWeaponsEveryone.GetType(), _regexAllowedWeaponsEveryone));
            lstReturn.Add(new CPluginVariable(GetSettingSection("2") + T + "Allowed weapons for whitelisted players", _regexAllowedWeaponsWhitelist.GetType(), _regexAllowedWeaponsWhitelist));

            // 3 Punishments
            lstReturn.Add(new CPluginVariable(GetSettingSection("3") + T + "Warnings (kills) before punishment", _chances.GetType(), _chances));
            lstReturn.Add(new CPluginVariable(GetSettingSection("3") + T + "Temp-Ban duration (Minutes)", _duration.GetType(), _duration));

            // 4 Announcements
            lstReturn.Add(new CPluginVariable(GetSettingSection("4") + T + "Enable Discord Webhook", _enableDiscordWebhook.GetType(), _enableDiscordWebhook));
            if (_enableDiscordWebhook) {
                lstReturn.Add(new CPluginVariable(GetSettingSection("4") + T + "Author", _webhookAuthor.GetType(), _webhookAuthor));
                lstReturn.Add(new CPluginVariable(GetSettingSection("4") + T + "Webhook URL", _webhookUrl.GetType(), _webhookUrl));
            }

            return lstReturn;
        }

        public void SetPluginVariable(string strVariable, string strValue) {
            if (strVariable.CompareTo("Debug level") == 0) {
                var tmp = 2;
                int.TryParse(strValue, out tmp);
                _debugLevel = tmp;
            }

            if (strVariable.CompareTo("Debug Mode (No Action)") == 0) {
                _debugMode = bool.Parse(strValue);
            }
            else if (strVariable.CompareTo("Whitelist") == 0) {
                _whitelistedPlayers = new List<string>(CPluginVariable.DecodeStringArray(strValue));
            }
            else if (strVariable.CompareTo("Automatically select VIP from Whitelist") == 0) {
                _automaticallySelectVip = bool.Parse(strValue);
                if (_automaticallySelectVip)
                    ConsoleWrite("The plugin will now automatically choose a random player from the whitelist as soon as the round starts!");
                else
                    ConsoleWrite("Switching to manually defined VIP!");
            }
            else if (strVariable.CompareTo("VIP") == 0) {
                if (strValue.Length == 0)
                    ConsoleError("Cannot set VIP to an empty string!");
                if (_isRunning && _vip.CompareTo(strValue) != 0)
                    foreach (var player in _whitelistedPlayers)
                        PlayerTell(player, "New VIP: " + strValue + " - Protect them!");
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
            else if (strVariable.CompareTo("Temp-Ban duration (Minutes)") == 0) {
                var value = int.Parse(strValue);
                if (value >= 1)
                    _duration = value;
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
                    ConsoleError("^b" + strValue + "^n is not a valid Discord webhook, was set to " + strValue + ", corrected to " + _webhookUrl);
                    return;
                }

                _webhookUrl = strValue;
            }
            else if (strVariable.CompareTo("Start Round") == 0) {
                var action = bool.Parse(strValue);
                if (action && !_resetRound && !_isRunning)
                    StartChallenge();
            }
            else if (strVariable.CompareTo("Reset Round") == 0) {
                var action = bool.Parse(strValue);
                if (action && !_startRound)
                    ResetChallenge();
            }
        }

        #endregion

        #region Tools

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

        //escape replacements
        public string E(string text) {
            // From InsaneLimits
            text = Regex.Replace(text, @"\\n", "\n");
            text = Regex.Replace(text, @"\\t", "\t");
            return text;
        }

        public string StripModifiers(string text) {
            // From InsaneLimits
            return Regex.Replace(text, @"\^[0-9a-zA-Z]", "");
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
            // From InsaneLimits
            var list = new List<string>();
            list.Add("procon.protected.send");
            list.AddRange(args);
            ExecuteCommand(list.ToArray());
        }

        #endregion

        #region KillReasons

        public interface KillReasonInterface {
            // From InsaneLimits
            string Name { get; } // weapon name or reason, like "Suicide"
            string Detail { get; } // BF4: ammo or attachment
            string AttachedTo { get; } // BF4: main weapon when Name is a secondary attachment, like M320
            string VehicleName { get; } // BF4: if Name is "Death", this is the vehicle's name
            string VehicleDetail { get; } // BF4: if Name is "Death", this is the vehicles detail (stuff after final slash)
        }

        public class KillReason : KillReasonInterface {
            // From InsaneLimits
            public string _attachedTo;
            public string _detail;
            public string _name = string.Empty;
            public string _vDetail;
            public string _vName;

            public string Name => _name; // weapon name or reason, like "Suicide"
            public string Detail => _detail; // BF4: ammo or attachment
            public string AttachedTo => _attachedTo; // BF4: main weapon when Name is a secondary attachment, like M320
            public string VehicleName => _vName; // BF4: if Name is "Death", this is the vehicle's name

            public string VehicleDetail => _vDetail; // BF4: if Name is "Death", this is the vehicle's detail (stuff after final slash)
        }

        private string GetCategory(Kill info) {
            // From InsaneLimits
            var category = DamageTypes.None;

            if (info == null || string.IsNullOrEmpty(info.DamageType))
                return "None";

            if (!WeaponsDict.TryGetValue(info.DamageType, out category))
                category = DamageTypes.None;

            return category.ToString();
        }

        public void InitWeapons() {
            // initialize values for all known weapons
            // From InsaneLimits

            var dic = GetWeaponDefines();
            WeaponsDict = new Dictionary<string, DamageTypes>();
            foreach (var weapon in dic)
                if (weapon != null && !WeaponsDict.ContainsKey(weapon.Name))
                    WeaponsDict.Add(weapon.Name, weapon.Damage);

            DebugWrite("^b" + WeaponsDict.Count + "^n weapons in dictionary", 5);
        }


        public KillReasonInterface FriendlyWeaponName(string killWeapon) {
            // From InsaneLimits
            var r = new KillReason();
            r._name = killWeapon;
            var category = DamageTypes.None;
            var hasCategory = false;

            if (WeaponsDict.TryGetValue(killWeapon, out category))
                hasCategory = true;

            if (_gameVersion == "BF3") {
                var m = Regex.Match(killWeapon, @"/([^/]+)$");
                r._name = killWeapon;
                if (m.Success) r._name = m.Groups[1].Value;
            }
            else if (killWeapon.StartsWith("U_")) // BF4 weapons // TBD BFH
            {
                var tParts = killWeapon.Split('_');

                if (tParts.Length == 2) {
                    // U_Name
                    r._name = tParts[1];
                }
                else if (tParts.Length == 3) {
                    // U_Name_Detail
                    r._name = tParts[1];
                    r._detail = tParts[2];
                }
                else if (tParts.Length >= 4) {
                    // U_AttachedTo_Name_Detail
                    r._name = tParts[2];
                    r._detail = tParts[3];
                    r._attachedTo = tParts[1];
                }
                else {
                    DebugWrite("Warning: unrecognized weapon code: " + killWeapon, 5);
                }
            }
            else if (killWeapon != "Death" && hasCategory) // BF4 vehicles?
            {
                if (category == DamageTypes.VehicleAir || category == DamageTypes.VehicleHeavy || category == DamageTypes.VehicleLight || category == DamageTypes.VehiclePersonal || category == DamageTypes.VehicleStationary || category == DamageTypes.VehicleTransport || category == DamageTypes.VehicleWater) {
                    r._name = "Death";
                    r._vName = killWeapon;
                    var m = Regex.Match(killWeapon, @"/([^/]+)/([^/]+)$");
                    if (m.Success) {
                        r._vName = m.Groups[1].Value;
                        r._vDetail = m.Groups[2].Value;
                    }

                    // Clean-up heuristics
                    var vn = r._vName;
                    if (vn.StartsWith("CH_"))
                        vn = vn.Replace("CH_", string.Empty);
                    else if (vn.StartsWith("Ch_"))
                        vn = vn.Replace("Ch_", string.Empty);
                    else if (vn.StartsWith("RU_"))
                        vn = vn.Replace("RU_", string.Empty);
                    else if (vn.StartsWith("US_"))
                        vn = vn.Replace("US_", string.Empty);

                    if (vn == "spec" && r._vDetail != null) {
                        if (r._vDetail.Contains("Z-11w"))
                            vn = "Z-11w";
                        else if (r._vDetail.Contains("DV15"))
                            vn = "DV15";
                        else vn = r._vDetail;
                    }

                    if (vn.StartsWith("FAC_"))
                        vn = vn.Replace("FAC_", "Boat ");
                    else if (vn.StartsWith("FAC-"))
                        vn = vn.Replace("FAC-", "Boat ");
                    else if (vn.StartsWith("JET_"))
                        vn = vn.Replace("JET_", "Jet ");
                    else if (vn.StartsWith("FJET_"))
                        vn = vn.Replace("FJET_", "Jet ");

                    if (vn == "LAV25" && r._vDetail != null) {
                        if (r._vDetail == "LAV_AD")
                            vn = "AA LAV_AD";
                        else
                            vn = "IFV LAV25";
                    }

                    switch (vn) {
                        case "9K22_Tunguska_M":
                            vn = "AA Tunguska";
                            break;
                        case "AC130":
                            vn = "AC130 Gunship";
                            break;
                        case "AH1Z":
                            vn = "Chopper AH1Z Viper";
                            break;
                        case "AH6":
                            vn = "Chopper AH6 Littlebird";
                            break;
                        case "BTR-90":
                            vn = "IFV BTR-90";
                            break;
                        case "F35":
                            vn = "Jet F35";
                            break;
                        case "HIMARS":
                            vn = "Artillery Truck M142 HIMARS";
                            break;
                        case "M1A2":
                            vn = "MBT M1A2";
                            break;
                        case "Mi28":
                            vn = "Chopper Mi28 Havoc";
                            break;
                        case "SU-25TM":
                            vn = "Jet SU-25TM";
                            break;
                        case "Venom":
                            vn = "Chopper Venom";
                            break;
                        case "Z-11w":
                            vn = "Chopper Z-11w";
                            break;
                        case "KLR650":
                            vn = "Bike KLR650";
                            break;
                        case "DPV":
                            vn = "Jeep DPV";
                            break;
                        case "LTHE_Z-9":
                            vn = "Chopper Z-9";
                            break;
                        case "FAV_LYT2021":
                            vn = "Jeep LYT2021";
                            break;
                        case "GrowlerITV":
                            vn = "Jeep Growler ITV";
                            break;
                        case "Ka-60":
                            vn = "Chopper Ka-60";
                            break;
                        case "VDV Buggy":
                            vn = "Jeep VDV Buggy";
                            break;
                        case "T90":
                            vn = "MBT T90";
                            break;
                        case "A-10_THUNDERBOLT":
                            vn = "Jet A-10 Thunderbolt";
                            break;
                        case "B1Lancer":
                            vn = "Jet B1 Lancer";
                            break;
                        case "H6K":
                            vn = "Jet H6K";
                            break;
                        case "Z-10w":
                            vn = "Chopper Z-10w";
                            break;
                        case "RHIB":
                            vn = "Boat RHIB";
                            break;
                    }

                    r._vName = vn.Replace('_', ' ');
                }
            }

            return r;
        }

        #endregion


        #region MemberChecks

        private bool IsVip(string name) {
            return _vip.ToLower().CompareTo(name.ToLower()) == 0;
        }

        private bool IsWhitelisted(string name) {
            foreach (var player in _whitelistedPlayers)
                if (player.ToLower().CompareTo(name.ToLower()) == 0)
                    return true;
            return false;
        }

        public void SetStatus(string status) {
            _curStatus = status;
            UpdateSettingsPage();
        }

        public void SetCurVip(string name) {
            _curVip = name;
            UpdateSettingsPage();
        }

        #endregion

        #region ServerCommands

        public void PRoConChat(string text) {
            ExecuteCommand("procon.protected.chat.write", E("^bPDXLANKnifeChallenge^n > " + text));
        }

        public void PlayerSay(string name, string text) {
            ServerCommand("admin.say", StripModifiers(E(text)), "player", name);
            PRoConChat(name + "> Say > " + text);
        }

        public void GlobalSay(string text) {
            ServerCommand("admin.say", StripModifiers(E(text)), "all");
            PRoConChat("Say > " + text);
        }

        public void GlobalSay(string text, int delay) {
            if (delay <= 0) {
                GlobalSay(text);
                return;
            }

            DebugWrite("Sending delayed msg\"" + text + "\" in " + delay + " seconds.", 2);
            var delayed = new Thread(new ThreadStart(delegate {
                Thread.Sleep(delay * 1000);
                GlobalSay(text);
            }));
            delayed.IsBackground = true;
            delayed.Name = "delay_global_say";
            delayed.Start();
        }

        public void PlayerYell(string name, string text) {
            ServerCommand("admin.yell", StripModifiers(E(text)), "10", "player", name);
            PRoConChat(name + "> Yell > " + text);
        }

        public void GlobalYell(string text) {
            ServerCommand("admin.yell", StripModifiers(E(text)), "10", "all");
            PRoConChat("Yell > " + text);
        }

        public void PlayerTell(string name, string text) {
            PlayerSay(name, text);
            PlayerYell(name, text);
        }

        public void GlobalTell(string text) {
            GlobalSay(text);
            GlobalYell(text);
        }

        public void KillPlayer(string name) {
            ServerCommand("admin.killPlayer", name);
            DebugWrite("Killing " + name + ".", 2);
        }

        public void KickPlayer(string name, string reason) {
            ServerCommand("admin.kickPlayer", name, reason);
            DebugWrite("Kicking " + name + " with reason " + reason + ".", 2);
        }

        public void TempBanPlayer(string name, string reason, int duration) {
            ServerCommand("banList.add", "name", name, "seconds", (duration * 60).ToString(), reason);
            ServerCommand("banList.save");
            DebugWrite("Temp banning " + name + " with reason " + reason + " for " + duration + " minutes.", 2);
        }

        public void PermaBanPlayer(string name, string reason) {
            ServerCommand("banList.add", "name", name, "perm", reason);
            ServerCommand("banList.save");
            DebugWrite("Perma banning " + name + " with reason " + reason + ".", 2);
        }

        public void PunishPlayer(string player, string weapon) {
            // increase counter
            // no lock needed -> 1 thread only
            if (_debugMode) {
                ConsoleWarn("NOT PUNISHING! DEBUG MODE ACTIVATED!");
                return;
            }

            if (!_punishments.ContainsKey(player))
                _punishments.Add(player, 0);
            _punishments[player] += 1;
            var punishments = _punishments[player];

            var weaponString = weapon != null ? " " + weapon : "";
            var reason = "Punished for using forbidden weapon" + weaponString + "!";
            PlayerTell(player, reason);

            var action = "";
            if (punishments <= _chances) {
                KillPlayer(player);
                action = "Killing";
            }
            else if (punishments == _chances + 1) {
                action = "Kicking";
                KickPlayer(player, reason);
            }
            else if (punishments == _chances + 2) {
                action = "Temp Banning";
                TempBanPlayer(player, reason + " [" + _duration + "m]", _duration);
            }
            else {
                action = "Banning";
                PermaBanPlayer(player, "Banned for ignoring weapon rules [perm]!");
            }

            var msg = action + " " + player + " for using forbidden weapon" + weaponString + "!";
            DebugWrite(msg, 1);
            GlobalSay(msg);
        }

        public void EndRound(int team) {
            ExecuteCommand("procon.protected.send", "mapList.endRound", team.ToString());
            DebugWrite("Triggering round end.", 1);
        }

        public void NextLevel() {
            ExecuteCommand("procon.protected.send", "mapList.runNextRound");
            DebugWrite("Loading next round.", 1);
        }

        #endregion

        #region Challenge

        public void AnnounceWinner(string winner, string weapon) {
            if (!_isRunning) {
                ConsoleWarn("Not Announcing winner ^b" + winner + "^n! Challenge not active!");
                return;
            }

            var msg = "^b " + winner + "^n has won the challenge by killing the VIP^b " + _vip + "^n!";
            ConsoleWrite(msg);
            GlobalTell(msg);

            // repeat
            GlobalSay(msg, 10);
            GlobalSay(msg, 20);

            if (_enableDiscordWebhook) {
                var hook = new DiscordWebhook(this, _webhookUrl, _webhookAuthor, DiscordWebhook.Green);
                hook.ThreadSendNotification(winner, _vip, weapon);
            }
        }

        public void WeaponDisclaimer() {
            GlobalSay("The usage of forbidden weapons leads to punishment! (See event description)");
        }

        public void StartChallenge() {
            if (_isRunning || _resetRound) {
                ConsoleError("Cannot start the challenge! Already running!");
                return;
            }
            _startRound = true;

            ConsoleWrite("Starting the challenge! VIP: " + _vip + " - " + _whitelistedPlayers.Count + " whitelisted players.");

            // Announce
            GlobalTell("Round Starting!");
            GlobalSay("Find and kill the VIP with one of the allowed weapons!");
            GlobalSay("The usage of forbidden weapons leads to punishment! (See event description)");

            foreach (var player in _whitelistedPlayers)
                PlayerSay(player, "Protect the VIP " + _vip);
            PlayerTell(_vip, "You are the VIP!");
            _isRunning = true;
            _startRound = false;
            SetStatus("Active and Running");
        }

        public void ResetChallenge() {
            _resetRound = false;
            if (!_isRunning || _startRound) {
                ConsoleError("Cannot reset the challenge! Not running!");
                return;
            }
            _isRunning = false;
            SetStatus("Not Running");
        }

        public void ProcessKill(Kill kKillerVictimDetails) {
            var isAllowedWeapon = Regex.Match(kKillerVictimDetails.DamageType, @"(?:" + _regexAllowedWeaponsEveryone + ")", RegexOptions.IgnoreCase).Success;
            var isWhitelistedWeapon = Regex.Match(kKillerVictimDetails.DamageType, @"(?:" + _regexAllowedWeaponsWhitelist + ")", RegexOptions.IgnoreCase).Success;

            var killer = kKillerVictimDetails.Killer.SoldierName;
            var victim = kKillerVictimDetails.Victim.SoldierName;
            var isKillerWhitelisted = IsWhitelisted(kKillerVictimDetails.Killer.SoldierName);
            var isVictimVip = IsVip(kKillerVictimDetails.Victim.SoldierName);
            var isKillerVip = IsVip(kKillerVictimDetails.Killer.SoldierName);
            
            // no killer = admin kill
            if (killer.Length == 0)
                return;

            // Suicide check
            if (killer.CompareTo(victim) == 0 || kKillerVictimDetails.IsSuicide) {
                if (isVictimVip)
                    ConsoleWarn("^b[VIP]" + victim + "^n killed themself! Doing nothing.");
                return;
            }

            var friendlyWeaponName = FriendlyWeaponName(kKillerVictimDetails.DamageType);
            var weapon = friendlyWeaponName.Name;
            if (friendlyWeaponName.Name == "Death")
                weapon = friendlyWeaponName.VehicleName;

            // Forbidden weapon usage?
            if ((isKillerWhitelisted || isKillerVip) && !isWhitelistedWeapon && !isAllowedWeapon || !isAllowedWeapon) {
                PunishPlayer(killer, weapon);
                return;
            }

            // Allowed weapon usage
            if (!isVictimVip) {
                // normal player killed
                DebugWrite("^b " + killer + "^n killed " + victim + " with allowed weapon ^b" + weapon + "^n! Doing nothing!", 2);
                return;
            }

            // victim killed
            // do nothing if a whitelisted player killed the VIP
            if (isKillerWhitelisted) {
                ConsoleWarn("^b [Whitelisted]" + killer + "^n killed [VIP]" + victim + " with ^b" + weapon + "^n.");
                return;
            }

            // non whitelisted player killed the victim with an allowed weapon!
            // announce and end round.
            AnnounceWinner(killer, weapon);

            // dirty, but we can do this... since we call this function in the KillProcessing thread
            Thread.Sleep(5 * 1000);
            EndRound(kKillerVictimDetails.Killer.TeamID);
        }

        public void KillProcessingLoop() {
            try {
                DebugWrite("Starting Kill Processing Thread", 1);
                Thread.CurrentThread.Name = "KillProcessing";
                while (true)
                    try {
                        if (!_isEnabled) {
                            DebugWrite("Detected plugin not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                            break;
                        }

                        //Get all unprocessed kills
                        Queue<Kill> inboundPlayerKills;
                        if (_killsToProcess.Count > 0) {
                            lock (_killsToProcess) {
                                inboundPlayerKills = new Queue<Kill>(_killsToProcess.ToArray());
                                // clear queue for next run
                                _killsToProcess.Clear();
                            }
                        }
                        else {
                            //Wait for input
                            // nothing to do...
                            Thread.Sleep(500);
                            continue;
                        }

                        // process all kills
                        while (inboundPlayerKills.Count > 0) {
                            if (!_isEnabled)
                                break;
                            //Dequeue the first/next kill
                            var playerKill = inboundPlayerKills.Dequeue();
                            ProcessKill(playerKill);
                        }
                    }
                    catch (Exception e) {
                        if (e is ThreadAbortException) {
                            ConsoleError("kill processing thread aborted. Exiting.");
                            break;
                        }

                        ConsoleException("Error occured in kill processing thread. " + e);
                    }

                DebugWrite("Ending Kill Processing Thread", 1);
            }
            catch (Exception e) {
                ConsoleException("Error occured in kill processing thread. " + e);
            }
        }

        #endregion

        #region Events

        public override void OnListPlayers(List<CPlayerInfo> players, CPlayerSubset subset) {
            var vipFound = false;
            foreach (var player in players)
                if (IsVip(player.SoldierName)) {
                    vipFound = true;
                    if (_curVip.CompareTo(player.SoldierName) != 0)
                        SetCurVip(player.SoldierName);
                    break;
                }

            if (!vipFound && _isEnabled)
                ConsoleWarn("VIP " + _vip + " is not online!");
        }

        public override void OnPlayerJoin(string soldierName) {
            if (IsVip(soldierName)) {
                ConsoleWarn("VIP " + _vip + " has joined the server!");
                SetCurVip(soldierName);
            }
        }

        public override void OnPlayerLeft(CPlayerInfo playerInfo) {
            if (IsVip(playerInfo.SoldierName)) {
                ConsoleWarn("VIP " + _vip + " has left the server!!! Choose a new VIP!");
                SetCurVip("NONE!!! SET A NEW VIP");
            }
        }

        public override void OnServerInfo(CServerInfo serverInfo) {
            // ConsoleWrite("Debug level = " + _debugLevel);
        }

        public override void OnResponseError(List<string> requestWords, string error) {
            // no error handling :kek: :)
        }

        public override void OnPlayerKilled(Kill kKillerVictimDetails) {
            if (!_isEnabled)
                return;
            lock (_killsToProcess) {
                _killsToProcess.Enqueue(kKillerVictimDetails);
            }
        }

        public override void OnPlayerSpawned(string soldierName, Inventory spawnedInventory) {
            if (!_isRunning)
                return;

            if (IsVip(soldierName))
                PlayerTell(soldierName, "You are the VIP!");
            else if (IsWhitelisted(soldierName))
                PlayerTell(soldierName, "Protect the VIP " + _vip + "!");
            else
                PlayerSay(soldierName, "Find and kill the VIP!");
        }

        public override void OnPlayerTeamChange(string soldierName, int teamId, int squadId) {
        }


        public override void OnGlobalChat(string speaker, string message) {
            HandleChat(speaker, message);
        }

        public override void OnTeamChat(string speaker, string message, int teamId) {
            HandleChat(speaker, message);
        }

        public override void OnSquadChat(string speaker, string message, int teamId, int squadId) {
            HandleChat(speaker, message);
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

        public override void OnLevelLoaded(string mapFileName, string gamemode, int roundsPlayed, int roundsTotal) {
            ResetChallenge();
        } // BF3

        #endregion

        #region members

        protected string Version = "1.0.1";
        private bool _isEnabled;

        // Section 1 - General
        private int _debugLevel = 2;

        // Section 2 - Players
        private List<string> _whitelistedPlayers = new List<string>();
        private bool _automaticallySelectVip;
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
        private string _webhookUrl = "https://discord.com/api/webhooks/xyz";

        // Section x - messages?

        // Section 6 - Controller
        private bool _isRunning;
        private string _curStatus = "Not Running";
        private string _curVip = "Not Online";
        private bool _startRound;
        private bool _resetRound;

        // MISC
        //Settings display
        private string _gameVersion = "BF4";
        private readonly Dictionary<string, string> _settingSections = new Dictionary<string, string>();

        public Dictionary<string, DamageTypes> WeaponsDict;
        private const string T = "|";

        private readonly Queue<Kill> _killsToProcess = new Queue<Kill>();
        private Thread _killProcessor;


        private readonly Dictionary<string, int> _punishments = new Dictionary<string, int>();
        private bool _debugMode;
        private int _duration = 30;

        # endregion
    } // end PDXLANKnifeChallenge

    // Extension: Discord Hook
    public class DiscordWebhook {
        public const int Green = 0x00FF00;
        public const int Blue = 0x0000FF;
        public const int Red = 0xFF0000;
        public const int Yellow = 0xFFFF00;

        private readonly PDXLANKnifeChallenge _plugin;
        public string Author;
        public string Avatar;
        public int Colour;
        public string Url;

        public bool UseCustomAvatar;

        public DiscordWebhook(PDXLANKnifeChallenge plugin, string url, string author, int colour) {
            _plugin = plugin;
            Url = url;
            Author = author;
            Avatar = Avatar;
            Colour = colour;
            UseCustomAvatar = UseCustomAvatar;
        }

        public void Post(string jsonBody) {
            try {
                if (string.IsNullOrEmpty(Url)) {
                    _plugin.ConsoleError("Discord WebHook URL empty! Unable to post message.");
                    return;
                }

                if (string.IsNullOrEmpty(jsonBody)) {
                    _plugin.ConsoleError("Discord JSON body empty! Unable to post message.");
                    return;
                }

                var request = WebRequest.Create(Url);
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
                _plugin.ConsoleError("Discord Webhook notification failed: " + new StreamReader(response.GetResponseStream()).ReadToEnd());
                _plugin.ConsoleException(e.ToString());
            }
            catch (Exception e) {
                _plugin.ConsoleError("Error while posting to Discord WebHook.");
                _plugin.ConsoleException(e.ToString());
            }
        }

        public void SendEmbed(Hashtable embed) {
            var embeds = new ArrayList {
                embed
            };

            var message = new Hashtable {
                { "content", null },
                { "embeds", embeds }
            };
            Post(JSON.JsonEncode(message));
        }

        public Hashtable GetEmbed() {
            return new Hashtable {
                { "timestamp", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ") }
            };
        }

        public void AnnounceWinner(string winner, string vip, string weapon) {
            // create embed
            var embed = GetEmbed();

            embed["title"] = "BF4 Knife Challenge: Winner";
            embed["description"] = "**" + winner + "** has won the VIP challenge.";
            embed["colour"] = Colour;
            embed["author"] = new Hashtable {
                { "name", Author }
            };

            embed["fields"] = new ArrayList {
                new Hashtable {
                    { "name", "Winner" },
                    { "value", winner },
                    { "inline", true }
                },
                new Hashtable {
                    { "name", "VIP" },
                    { "value", vip },
                    { "inline", true }
                }
            };

            if (weapon != null)
                ((ArrayList)embed["fields"]).Add(new Hashtable {
                    { "name", "Weapon" },
                    { "value", weapon },
                    { "inline", true }
                });

            // send embed
            SendEmbed(embed);
        }


        public void ThreadSendNotification(string winner, string vip, string weapon) {
            var discordWebhookThread = new Thread(delegate() { AnnounceWinner(winner, vip, weapon); });
            discordWebhookThread.IsBackground = true;
            discordWebhookThread.Name = "DiscordWebhookTrhead";
            discordWebhookThread.Start();
        }
    }
} // end namespace PRoConEvents