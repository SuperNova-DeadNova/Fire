﻿/*
    Copyright 2011 MCForge
        
    Dual-licensed under the    Educational Community License, Version 2.0 and
    the GNU General Public License, Version 3 (the "Licenses"); you may
    not use this file except in compliance with the Licenses. You may
    obtain a copy of the Licenses at
    
    https://opensource.org/license/ecl-2-0/
    https://www.gnu.org/licenses/gpl-3.0.html
    
    Unless required by applicable law or agreed to in writing,
    software distributed under the Licenses are distributed on an "AS IS"
    BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express
    or implied. See the Licenses for the specific language governing
    permissions and limitations under the Licenses.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Flames.DB;
using Flames.Events.ServerEvents;

namespace Flames.Modules.Relay
{
    public class RelayUser
    {
        public string ID, Nick;

        public virtual string GetMessagePrefix() 
        { 
            return ""; 
        }
    }

    /// <summary> Manages a connection to an external communication service </summary>
    public abstract class RelayBot
    {
        /// <summary> List of commands that cannot be used by relay bot controllers. </summary>
        public List<string> BannedCommands;

        /// <summary> List of channels to send public chat messages to </summary>
        public string[] Channels;

        /// <summary> List of channels to send staff only messages to </summary>
        public string[] OpChannels;

        /// <summary> List of user IDs that all chat from is ignored </summary>
        public string[] IgnoredUsers;

        public Player fakeGuest = new Player("RelayBot");
        public Player fakeStaff = new Player("RelayBot");
        public DateTime lastWho, lastOpWho, lastWarn;

        public bool canReconnect;
        public byte retries;
        public Thread worker;
        /// <summary> Whether this relay bot can automatically reconnect </summary>
        public abstract bool CanReconnect { get; }


        /// <summary> The name of the service this relay bot communicates with </summary>
        /// <example> IRC, Discord </example>
        public abstract string RelayName { get; }

        /// <summary> Whether this relay bot is currently enabled </summary>
        public abstract bool Enabled { get; }

        /// <summary> Whether this relay bot is connected to the external communication service </summary>
        public bool Connected { get { return worker != null; } }

        /// <summary> List of users allowed to run in-game commands from the external communication service </summary>
        public PlayerList Controllers;

        /// <summary> The ID of the user associated with this relay bot </summary>
        /// <remarks> Do not cache this ID as it can change </remarks>
        public abstract string UserID { get; }


        /// <summary> Sends a message to all channels setup for general public chat </summary>
        public void SendPublicMessage(string message)
        {
            foreach (string chan in Channels)
            {
                SendMessage(chan, message);
            }
        }

        /// <summary> Sends a message to all channels setup for staff chat only </summary>
        public void SendStaffMessage(string message)
        {
            foreach (string chan in OpChannels)
            {
                SendMessage(chan, message);
            }
        }

        /// <summary> Sends a message to the given channel </summary>
        /// <remarks> Channels can specify either group chat or direct messages </remarks>
        public void SendMessage(string channel, string message)
        {
            if (!Enabled || !Connected) return;
            DoSendMessage(channel, message);
        }

        public abstract void DoSendMessage(string channel, string message);

        public string ConvertMessageCommon(string message)
        {
            message = EmotesHandler.Replace(message);
            message = ChatTokens.ApplyCustom(message);
            return message;
        }


        /// <summary> Attempts to connect to the external communication service </summary>
        /// <returns> null if connecting succeeded, otherwise the reason why connecting failed </returns>
        /// <remarks> e.g. is not enabled, is already connected, server shutting down </remarks>
        public string Connect()
        {
            if (!Enabled) return "is not enabled";
            if (Connected) return "is already connected";
            if (Server.shuttingDown) return "cannot connect as server shutting down";
            canReconnect = true;
            retries = 0;

            try
            {
                UpdateConfig();
                RunAsync();
            }
            catch (Exception e)
            {
                Logger.Log(LogType.RelayActivity, "Failed to connect to {0}!", RelayName);
                Logger.LogError(e);
                return "failed to connect - " + e.Message;
            }
            return null;
        }

        /// <summary> Forcefully disconnects from the external communication service </summary>
        /// <remarks> Does nothing if not connected </remarks>
        public void Disconnect(string reason)
        {
            if (!Connected) return;
            canReconnect = false;

            // silent, as otherwise it'll duplicate disconnect messages with IOThread
            try 
            { 
                DoDisconnect(reason); 
            } 
            catch 
            { 
            }
            // wait for worker to completely finish
            try 
            { 
                worker.Join(); 
            } 
            catch 
            { 
            }
        }

        /// <summary> Disconnects from the external communication service and then connects again </summary>
        public void Reset()
        {
            Disconnect(RelayName + " Bot resetting...");
            Connect();
        }

        public void OnReady()
        {
            Logger.Log(LogType.RelayActivity, "Connected to {0}!", RelayName);
            retries = 0;
        }


        public void IOThreadCore()
        {
            OnStart();

            while (CanReconnect && retries < 3)
            {
                try
                {
                    Logger.Log(LogType.RelayActivity, "Connecting to {0}...", RelayName);
                    DoConnect();
                    DoReadLoop();
                }
                catch (SocketException ex)
                {
                    Logger.Log(LogType.Warning, "Disconnected from {0} ({1}), retrying in {2} seconds..",
                               RelayName, ex.Message, 30);

                    // SocketException is usually due to complete connection dropout
                    retries = 0;
                    Thread.Sleep(30 * 1000);
                }
                catch (IOException ex)
                {
                    // IOException is an expected error, so don't log full details
                    Logger.Log(LogType.Warning, "{0} read error ({1})", RelayName, ex.Message);
                }
                catch (ObjectDisposedException ex)
                {
                    // ObjectDisposedException is an expected error, so don't log full details
                    Logger.Log(LogType.Warning, "{0} read error ({1})", RelayName, ex.Message);
                }
                catch (Exception ex)
                {
                    Logger.LogError(RelayName + " relay error", ex);
                }
                retries++;

                try
                {
                    DoDisconnect("Reconnecting");
                }
                catch (Exception ex)
                {
                    Logger.LogError("Disconnecting from " + RelayName, ex);
                }
                Logger.Log(LogType.RelayActivity, "Disconnected from {0}!", RelayName);
            }
            OnStop();
        }

        public void IOThread()
        {
            try
            {
                IOThreadCore();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
            }
            worker = null;
        }

        /// <summary> Starts the read loop in a background thread </summary>
        public void RunAsync()
        {
            Server.StartThread(out worker, RelayName + "_RelayBot",
                               IOThread);
            Utils.SetBackgroundMode(worker);
        }

        public abstract void DoConnect();
        public abstract void DoReadLoop();
        public abstract void DoDisconnect(string reason);


        /// <summary> Loads the list of controller users from disc </summary>
        public abstract void LoadControllers();

        /// <summary> Reloads all configuration (including controllers list) </summary>
        public virtual void ReloadConfig()
        {
            UpdateConfig();
            LoadControllers();
        }

        public abstract void UpdateConfig();

        public void LoadBannedCommands()
        {
            BannedCommands = new List<string>() 
            { 
                "IRCBot", "DiscordBot", "OpRules", "IRCControllers", "DiscordControllers" 
            };

            if (!File.Exists("text/irccmdblacklist.txt"))
            {
                File.WriteAllLines("text/irccmdblacklist.txt", new string[] {
                                       "# Here you can put commands that cannot be used from the IRC bot.",
                                       "# Lines starting with \"#\" are ignored." });
            }

            foreach (string line in Utils.ReadAllLinesList("text/irccmdblacklist.txt"))
            {
                if (!line.IsCommentLine()) BannedCommands.Add(line);
            }
        }


        public virtual void OnStart()
        {
            OnChatEvent.Register(OnChat, Priority.Low);
            OnChatSysEvent.Register(OnChatSys, Priority.Low);
            OnChatFromEvent.Register(OnChatFrom, Priority.Low);
            OnShuttingDownEvent.Register(OnShutdown, Priority.Low);
        }

        public virtual void OnStop()
        {
            OnChatEvent.Unregister(OnChat);
            OnChatSysEvent.Unregister(OnChatSys);
            OnChatFromEvent.Unregister(OnChatFrom);
            OnShuttingDownEvent.Unregister(OnShutdown);
        }


        public static bool FilterIRC(Player pl, object arg)
        {
            return !pl.Ignores.IRC && !pl.Ignores.IRCNicks.Contains((string)arg);
        }
        public static ChatMessageFilter filterIRC = FilterIRC;

        public static void MessageInGame(string srcNick, string message)
        {
            Chat.Message(ChatScope.Global, message, srcNick, filterIRC);
        }

        public string Unescape(Player p, string msg)
        {
            return msg
                .Replace("λFULL", UnescapeFull(p))
                .Replace("λNICK", UnescapeNick(p));
        }

        public virtual string UnescapeFull(Player p)
        {
            return Server.Config.IRCShowPlayerTitles ? p.FullName : p.group.Prefix + p.ColoredName;
        }

        public virtual string UnescapeNick(Player p) 
        { 
            return p.ColoredName; 
        }
        public virtual string PrepareMessage(string msg) 
        { 
            return msg; 
        }


        public void MessageToRelay(ChatScope scope, string msg, object arg, ChatMessageFilter filter)
        {
            ChatMessageFilter scopeFilter = Chat.scopeFilters[(int)scope];
            fakeGuest.group = Group.DefaultRank;

            if (scopeFilter(fakeGuest, arg) && (filter == null || filter(fakeGuest, arg)))
            {
                SendPublicMessage(msg); 
                return;
            }

            fakeStaff.group = GetControllerRank();
            if (scopeFilter(fakeStaff, arg) && (filter == null || filter(fakeStaff, arg)))
            {
                SendStaffMessage(msg);
            }
        }

        public void OnChatSys(ChatScope scope, string msg, object arg,
                           ref ChatMessageFilter filter, bool relay)
        {
            if (!relay) return;

            string text = PrepareMessage(msg);
            MessageToRelay(scope, text, arg, filter);
        }

        public void OnChatFrom(ChatScope scope, Player source, string msg,
                            object arg, ref ChatMessageFilter filter, bool relay)
        {
            if (!relay) return;

            string text = PrepareMessage(msg);
            MessageToRelay(scope, Unescape(source, text), arg, filter);
        }

        public void OnChat(ChatScope scope, Player source, string msg,
                        object arg, ref ChatMessageFilter filter, bool relay)
        {
            if (!relay) return;

            string text = PrepareMessage(msg);
            MessageToRelay(scope, Unescape(source, text), arg, filter);
        }

        public void OnShutdown(bool restarting, string message)
        {
            Disconnect(restarting ? "Server is restarting" : "Server is shutting down");
        }


        /// <summary> Simplifies some fancy characters (e.g. simplifies ” to ") </summary>
        public void SimplifyCharacters(StringBuilder sb)
        {
            // simplify fancy quotes
            sb.Replace("“", "\"");
            sb.Replace("”", "\"");
            sb.Replace("‘", "'");
            sb.Replace("’", "'");
        }
        public abstract string ParseMessage(string message);

        /// <summary> Handles a direct message written by the given user </summary>
        public void HandleDirectMessage(RelayUser user, string channel, string message)
        {
            if (IgnoredUsers.CaselessContains(user.ID)) return;
            message = ParseMessage(message).TrimEnd();
            if (message.Length == 0) return;

            bool cancel = false;
            OnDirectMessageEvent.Call(this, channel, user, message, ref cancel);
            if (cancel) return;

            string[] parts = message.SplitSpaces(2);
            string cmdName = parts[0].ToLower();
            string cmdArgs = parts.Length > 1 ? parts[1] : "";

            if (HandleListPlayers(user, channel, cmdName, false)) return;
            Command.Search(ref cmdName, ref cmdArgs);

            string error;
            if (!CanUseCommand(user, cmdName, out error))
            {
                if (error != null) SendMessage(channel, error);
                return;
            }

            ExecuteCommand(user, channel, cmdName, cmdArgs);
        }

        /// <summary> Handles a message written by the given user on the given channel </summary>
        public void HandleChannelMessage(RelayUser user, string channel, string message)
        {
            if (IgnoredUsers.CaselessContains(user.ID)) return;
            message = ParseMessage(message).TrimEnd();
            if (message.Length == 0) return;

            bool cancel = false;
            OnChannelMessageEvent.Call(this, channel, user, message, ref cancel);
            if (cancel) return;

            string[] parts = message.SplitSpaces(3);
            string rawCmd = parts[0].ToLower();
            bool chat = Channels.CaselessContains(channel);
            bool opchat = OpChannels.CaselessContains(channel);

            // Only reply to .who on channels configured to listen on
            if ((chat || opchat) && HandleListPlayers(user, channel, rawCmd, opchat)) return;
            if ((chat || opchat) && HandleLogo(user, channel, rawCmd)) return;
            if ((chat || opchat) && HandleURL(user, channel, rawCmd)) return;

            if (rawCmd.CaselessEq(Server.Config.IRCCommandPrefix))
            {
                if (!HandleCommand(user, channel, message, parts)) return;
            }
            string msg = user.GetMessagePrefix() + message;

            if (opchat)
            {
                Logger.Log(LogType.RelayChat, "(OPs): ({0}) {1}: {2}", RelayName, user.Nick, msg);
                Chat.MessageOps(string.Format("To Ops &f-&I({0}) {1}&f- {2}", RelayName, user.Nick,
                                              Server.Config.ProfanityFiltering ? ProfanityFilter.Parse(msg) : msg));
            }
            else if (chat)
            {
                Logger.Log(LogType.RelayChat, "({0}) {1}: {2}", RelayName, user.Nick, msg);
                MessageInGame(user.Nick, string.Format("&I({0}) {1}: &f{2}", RelayName, user.Nick,
                                                       Server.Config.ProfanityFiltering ? ProfanityFilter.Parse(msg) : msg));
            }
        }

        public bool HandleListPlayers(RelayUser user, string channel, string cmd, bool opchat)
        {
            bool isWho = cmd.ToLower() == ".who" || cmd.ToLower() == ".players" 
                      || cmd.ToLower() == "!who" || cmd.ToLower() == "!players";
            DateTime last = opchat ? lastOpWho : lastWho;
            if (!isWho || (DateTime.UtcNow - last).TotalSeconds <= 5) return false;

            try
            {
                RelayPlayer p = new RelayPlayer(channel, user, this)
                {
                    group = Group.DefaultRank
                };
                MessagePlayers(p);
            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }

            if (opchat) lastOpWho = DateTime.UtcNow;
            else lastWho = DateTime.UtcNow;
            return true;
        }
        public bool HandleLogo(RelayUser user, string channel, string cmd)
        {
            bool isLogo = cmd.ToLower() == ".serverlogo" || cmd.ToLower() == ".logo"
                       || cmd.ToLower() == "!serverlogo" || cmd.ToLower() == "!logo"; 
            if (!isLogo) return false;
            try
            {
                RelayPlayer p = new RelayPlayer(channel, user, this)
                {
                    group = Group.DefaultRank
                };
                MessageLogo(p);
            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }
            return true;
        }
        public bool HandleURL(RelayUser user, string channel, string cmd)
        {
            bool isURL = cmd.ToLower() == ".serverurl" || cmd.ToLower() == ".url"
                      || cmd.ToLower() == "!serverurl" || cmd.ToLower() == "!url";
            if (!isURL) return false;
            try
            {
                RelayPlayer p = new RelayPlayer(channel, user, this)
                {
                    group = Group.DefaultRank
                };
                MessageURL(p);
            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }
            return true;
        }
        /// <summary> Outputs the list of online players to the given user </summary>
        public virtual void MessagePlayers(RelayPlayer p)
        {
            Command.Find("Players").Use(p, "", p.DefaultCmdData);
        }
        public virtual void MessageLogo(RelayPlayer p)
        {
            Command.Find("ServerLogo").Use(p, "", p.DefaultCmdData);
        }
        public virtual void MessageURL(RelayPlayer p)
        {
            Command.Find("ServerUrl").Use(p, "", p.DefaultCmdData);
        }

        public bool HandleCommand(RelayUser user, string channel, string message, string[] parts)
        {
            string cmdName = parts.Length > 1 ? parts[1].ToLower() : "";
            string cmdArgs = parts.Length > 2 ? parts[2].Trim() : "";
            Command.Search(ref cmdName, ref cmdArgs);

            string error;
            if (!CanUseCommand(user, cmdName, out error))
            {
                if (error != null) SendMessage(channel, error);
                return false;
            }

            return ExecuteCommand(user, channel, cmdName, cmdArgs);
        }

        public bool ExecuteCommand(RelayUser user, string channel, string cmdName, string cmdArgs)
        {
            Command cmd = Command.Find(cmdName);
            Player p = new RelayPlayer(channel, user, this);
            if (cmd == null) 
            { 
                p.Message("Unknown command \"{0}\"", cmdName);
                return false; 
            }

            string logCmd = cmdArgs.Length == 0 ? cmdName : cmdName + " " + cmdArgs;
            Logger.Log(LogType.CommandUsage, "/{0} (by {1} from {2})", logCmd, user.Nick, RelayName);

            try
            {
                if (!p.CanUse(cmd))
                {
                    cmd.Permissions.MessageCannotUse(p);
                    return false;
                }
                if (!cmd.SuperUseable)
                {
                    p.Message(cmd.name + " can only be used in-game.");
                    return false;
                }
                cmd.Use(p, cmdArgs);
            }
            catch (Exception ex)
            {
                p.Message("CMD Error: " + ex);
                Logger.LogError(ex);
            }
            return true;
        }

        /// <summary> Returns whether the given relay user is allowed to execute the given command </summary>
        public bool CanUseCommand(RelayUser user, string cmd, out string error)
        {
            error = null;

            if (!Controllers.Contains(user.ID))
            {
                // Intentionally show no message to non-controller users to avoid spam
                if ((DateTime.UtcNow - lastWarn).TotalSeconds <= 60) return false;

                lastWarn = DateTime.UtcNow;
                error = "Only " + RelayName + " Controllers are allowed to use in-game commands from " + RelayName;
                return false;
            }

            // Make sure controller is actually allowed to execute commands right now
            if (!CheckController(user.ID, ref error)) return false;

            if (BannedCommands.CaselessContains(cmd))
            {
                error = "You are not allowed to use this command from " + RelayName + ".";
                return false;
            }
            return true;
        }

        /// <summary> Returns whether the given controller is currently allowed to execute commands </summary>
        /// <remarks> e.g. a user may have to login before they are allowed to execute commands </remarks>
        public abstract bool CheckController(string userID, ref string error);

        public Group GetControllerRank()
        {
            LevelPermission perm = Server.Config.IRCControllerRank;

            // find highest rank <= IRC controller rank
            for (int i = Group.GroupList.Count - 1; i >= 0; i--)
            {
                Group grp = Group.GroupList[i];
                if (grp.Permission <= perm) return grp;
            }
            return Group.DefaultRank;
        }

        public class RelayPlayer : Player
        {
            public string ChannelID;
            public RelayUser User;
            public RelayBot Bot;

            public RelayPlayer(string channel, RelayUser user, RelayBot bot) : base(bot.RelayName)
            {
                group = bot.GetControllerRank();

                ChannelID = channel;
                User = user;
                color = "&a";
                Bot = bot;

                if (user != null)
                {
                    string nick = "(" + bot.RelayName + " " + user.Nick + ")";
                    DatabaseID = NameConverter.InvalidNameID(nick);
                }
                SuperName = bot.RelayName;
            }

            public override void Message(string message)
            {
                Bot.SendMessage(ChannelID, message);
            }
        }
    }
}
