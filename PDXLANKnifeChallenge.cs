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
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Collections;
using System.Net;
using System.Web;
using System.Data;
using System.Threading;
using System.Timers;
using System.Diagnostics;
using System.Reflection;

using PRoCon.Core;
using PRoCon.Core.Plugin;
using PRoCon.Core.Plugin.Commands;
using PRoCon.Core.Players;
using PRoCon.Core.Players.Items;
using PRoCon.Core.Battlemap;
using PRoCon.Core.Maps;


namespace PRoConEvents
{

	//Aliases
	using EventType = PRoCon.Core.Events.EventType;
	using CapturableEvent = PRoCon.Core.Events.CapturableEvents;
	
	public class PDXLANKnifeChallenge : PRoConPluginAPI, IPRoConPluginInterface
	{
	
		/* Inherited:
		    this.PunkbusterPlayerInfoList = new Dictionary<String, CPunkbusterInfo>();
		    this.FrostbitePlayerInfoList = new Dictionary<String, CPlayerInfo>();
		*/
		
		#region members
		protected string Version = "0.0.1";
		private bool _isEnabled = false;
		
		// Section 1 - General
		private int _debugLevel = 2;
		
		// Section 2 - Players
		private List<string> _whitelistedPlayers = new List<string>();
		private bool _automaticallySelectVIP = false;
		private string _vip = "Player1";
		
		// Section 3 - Weapons
		private string _regexAllowedWeaponsEveryone = "Melee|BallisticShield";
		private string _regexAllowedWeaponsWhitelist = "ABC|ABC|ABC";
		
		// Section 4 - Punishment
		// chances -> 2 kills -> kick -> tban 30 -> ban
		// only affects the amount of kills (warnings)
		private int _chances = 2;
		
		// Section 5 - Announcements
		private bool _enableDiscordWebhook = false;
		private String _webhookAuthor;
		private bool _useCustomWebhookAvatar;
		private String _webhookAvatarURL;
		private String _webhookURL;
		
		// Section x - messages?
		
		// Section 6 - Controller
		private bool _isRunning = false;
		private string _curStatus = "Not Running";
		private string _curVIP = "None";
		private bool _startRound = false;
		private bool _resetRound = false;
		
		// MISC
		//Settings display
		private Dictionary<String, String> _SettingSections = new Dictionary<String, String>();

		private const string T = "|";
		# endregion


		public PDXLANKnifeChallenge() {
			_isEnabled = false;
			
			AddSettingSection("0", "General");
			AddSettingSection("1", "Player Management");
			AddSettingSection("2", "Weapon Limitations");
			AddSettingSection("3", "Punishment");
			AddSettingSection("4", "Announcements");
			AddSettingSection("5", "Controller");
		}

		public enum MessageType { Warning, Error, Exception, Normal };

		public String FormatMessage(String msg, MessageType type) {
			String prefix = "[^b" + GetPluginName() + "^n] ";
		
			if (type.Equals(MessageType.Warning))
				prefix += "^1^bWARNING^0^n: ";
			else if (type.Equals(MessageType.Error))
				prefix += "^1^bERROR^0^n: ";
			else if (type.Equals(MessageType.Exception))
				prefix += "^1^bEXCEPTION^0^n: ";
		
			return prefix + msg;
		}


		public void LogWrite(String msg)
		{
			this.ExecuteCommand("procon.protected.pluginconsole.write", msg);
		}
		
		public void ConsoleWrite(String msg, MessageType type)
		{
			LogWrite(FormatMessage(msg, type));
		}
		
		public void ConsoleWrite(String msg)
		{
			ConsoleWrite(msg, MessageType.Normal);
		}
		
		public void ConsoleWarn(String msg)
		{
			ConsoleWrite(msg, MessageType.Warning);
		}
		
		public void ConsoleError(String msg)
		{
			ConsoleWrite(msg, MessageType.Error);
		}
		
		public void ConsoleException(String msg)
		{
			ConsoleWrite(msg, MessageType.Exception);
		}
		
		public void DebugWrite(String msg, int level)
		{
			if (_debugLevel >= level) ConsoleWrite(msg, MessageType.Normal);
		}
		
		
		public void ServerCommand(params String[] args)
		{
			List<String> list = new List<String>();
			list.Add("procon.protected.send");
			list.AddRange(args);
			this.ExecuteCommand(list.ToArray());
		}
		
		
		public String GetPluginName() {
			return "PDXLAN - Knife Challenge";
		}
		
		public String GetPluginVersion() {
			return "0.0.0.1";
		}
		
		public String GetPluginAuthor() {
			return "Hedius";
		}
		
		public String GetPluginWebsite() {
			return "http://github.com/Hedius/PDXLAN-Knife-Challenge";
		}
		
		public String GetPluginDescription() {
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
		
		private String AddSettingSection(String number, String desc)
		{
			_SettingSections[number] = desc;
			return GetSettingSection(number);
		}
		
		private String GetSettingSection(String number)
		{
			return number + ". " + _SettingSections[number];
		}
		
		public List<CPluginVariable> GetDisplayPluginVariables() {
		
			List<CPluginVariable> lstReturn = new List<CPluginVariable>();
			
			// 0 General
			lstReturn.Add(new CPluginVariable(GetSettingSection("0") + T + "Debug level", _debugLevel.GetType(), _debugLevel));
			
			// 1 Player Management
			lstReturn.Add(new CPluginVariable(GetSettingSection("1") + T + "Whitelist", typeof(string[]), _whitelistedPlayers.ToArray()));
			lstReturn.Add(new CPluginVariable(GetSettingSection("1") + T + "Automatically select VIP from Whitelist", _automaticallySelectVIP.GetType(), _automaticallySelectVIP));
			if (!_automaticallySelectVIP) {
				lstReturn.Add(new CPluginVariable(GetSettingSection("1") + T + "VIP", _vip.GetType(), _vip));
			}
			
			// 2 Weapons
			lstReturn.Add(new CPluginVariable(GetSettingSection("2") + T + "Allowed weapons for everyone", _regexAllowedWeaponsEveryone.GetType(), _regexAllowedWeaponsEveryone));
			lstReturn.Add(new CPluginVariable(GetSettingSection("2") + T + "Allowed weapons for whitelisted players", _regexAllowedWeaponsWhitelist.GetType(), _regexAllowedWeaponsWhitelist));
			
			// 3 Punishments
			lstReturn.Add(new CPluginVariable(GetSettingSection("3") + T + "Warnings (kills) before punishment", _chances.GetType(), _chances));
			
			// 4 Announcements
			lstReturn.Add(new CPluginVariable(GetSettingSection("4") + T + "Enable Discord Webhook", _enableDiscordWebhook.GetType(), _enableDiscordWebhook));
			if (_enableDiscordWebhook) {
				lstReturn.Add(new CPluginVariable(GetSettingSection("4") + T + "Author", _webhookAuthor.GetType(), _webhookAuthor));
				lstReturn.Add(new CPluginVariable(GetSettingSection("4") + T + "Use Custom Avatar", _useCustomWebhookAvatar.GetType(), _useCustomWebhookAvatar));
				if (_useCustomWebhookAvatar) {
					lstReturn.Add(new CPluginVariable(GetSettingSection("4") + T + "Avatar URL", _webhookAvatarURL.GetType(), _webhookAvatarURL));
				}
				lstReturn.Add(new CPluginVariable(GetSettingSection("4") + T + "Webhook URL", _webhookURL.GetType(), _webhookURL));
			}
			
			// 5 Controller
			lstReturn.Add(new CPluginVariable(GetSettingSection("5") + T + "Current Status (Display Only)", _curStatus.GetType(), _curStatus));
			lstReturn.Add(new CPluginVariable(GetSettingSection("5") + T + "Current VIP (Display Only)", _curVIP.GetType(), _curVIP));
			lstReturn.Add(new CPluginVariable(GetSettingSection("5") + T + "Start Round", _startRound.GetType(), _startRound));
			lstReturn.Add(new CPluginVariable(GetSettingSection("5") + T + "Reset Round", _resetRound.GetType(), _resetRound));
		
			return lstReturn;
		}
		
		public List<CPluginVariable> GetPluginVariables() {
			return GetDisplayPluginVariables();
		}
		
		public void SetPluginVariable(String strVariable, String strValue) {
			if (Regex.Match(strVariable, @"Debug level").Success) {
				int tmp = 2;
				int.TryParse(strValue, out tmp);
				_debugLevel = tmp;
			}
		}
		
		
		public void OnPluginLoaded(String strHostName, String strPort, String strPRoConVersion) {
			this.RegisterEvents(this.GetType().Name, "OnVersion", "OnServerInfo", "OnResponseError", "OnListPlayers", "OnPlayerJoin", "OnPlayerLeft", "OnPlayerKilled", "OnPlayerSpawned", "OnPlayerTeamChange", "OnGlobalChat", "OnTeamChat", "OnSquadChat", "OnRoundOverPlayers", "OnRoundOver", "OnRoundOverTeamScores", "OnLoadingLevel", "OnLevelStarted", "OnLevelLoaded");
		}
		
		public void OnPluginEnable() {
			_isEnabled = true;
			ConsoleWrite("Enabled!");
		}
		
		public void OnPluginDisable() {
			_isEnabled = false;
			ConsoleWrite("Disabled!");
		}
		
		
		public override void OnVersion(String serverType, String version) { }
		
		public override void OnServerInfo(CServerInfo serverInfo) {
			ConsoleWrite("Debug level = " + _debugLevel);
		}
		
		public override void OnResponseError(List<String> requestWords, String error) { }
		
		public override void OnListPlayers(List<CPlayerInfo> players, CPlayerSubset subset) {
		}
		
		public override void OnPlayerJoin(String soldierName) {
		}
		
		public override void OnPlayerLeft(CPlayerInfo playerInfo) {
		}
		
		public override void OnPlayerKilled(Kill kKillerVictimDetails) { }
		
		public override void OnPlayerSpawned(String soldierName, Inventory spawnedInventory) { }
		
		public override void OnPlayerTeamChange(String soldierName, int teamId, int squadId) { }
		
		public override void OnGlobalChat(String speaker, String message) { }
		
		public override void OnTeamChat(String speaker, String message, int teamId) { }
		
		public override void OnSquadChat(String speaker, String message, int teamId, int squadId) { }
		
		public override void OnRoundOverPlayers(List<CPlayerInfo> players) { }
		
		public override void OnRoundOverTeamScores(List<TeamScore> teamScores) { }
		
		public override void OnRoundOver(int winningTeamId) { }
		
		public override void OnLoadingLevel(String mapFileName, int roundsPlayed, int roundsTotal) { }
		
		public override void OnLevelStarted() { }
		
		public override void OnLevelLoaded(String mapFileName, String Gamemode, int roundsPlayed, int roundsTotal) { } // BF3
		
		
	} // end PDXLANKnifeChallenge

} // end namespace PRoConEvents



