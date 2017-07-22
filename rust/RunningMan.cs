﻿using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Libraries;
using Random = System.Random;

namespace Oxide.Plugins
{
    [Info("RunningMan", "sami37 - Мизантроп", "1.4.2")]
    [Description("Running Man is a short plugin where you have to kill the runner.")]
    class RunningMan : RustPlugin
    {
        private Timer stillRunnerTimer;
        private Command command = Interface.Oxide.GetLibrary<Command>();
        private Dictionary<string, int> SavedReward = new Dictionary<string, int>();
        private BasePlayer runningman;
        private Timer eventstart;
        private Timer eventpause;
        private double time1;
        private double time2;
        private Random rnd = new Random();

        [PluginReference]
        Plugin Economics;
        
        [PluginReference]
        Plugin KarmaSystem;
        
        void SetConfig(params object[] args)
        {
            List<string> stringArgs = (from arg in args select arg.ToString()).ToList();
            stringArgs.RemoveAt(args.Length - 1);

            if (Config.Get(stringArgs.ToArray()) == null) Config.Set(args);
        }

        T GetConfig<T>(T defaultVal, params object[] args)
        {
            List<string> stringArgs = (from arg in args select arg.ToString()).ToList();
            if (Config.Get(stringArgs.ToArray()) == null)
            {
                PrintError($"The plugin failed to read something from the config: {ListToString(stringArgs, 0, "/")}{Environment.NewLine}Please reload the plugin and see if this message is still showing. If so, please post this into the support thread of this plugin.");
                return defaultVal;
            }

            return (T)Convert.ChangeType(Config.Get(stringArgs.ToArray()), typeof(T));
        }

        string ListToString<T>(List<T> list, int first = 0, string seperator = ", ") => string.Join(seperator, (from val in list select val.ToString()).Skip(first).ToArray());

        void Loaded()
        {
            LoadDefaultConfig();
            command.AddChatCommand("eventon", this, "cmdEvent");
            command.AddChatCommand("eventoff", this, "cmdEventOff");
            command.AddChatCommand("run", this, "cmdRun");
            command.AddConsoleCommand("eventon", this, "ccmdEvent");
            command.AddConsoleCommand("eventoff", this, "cmdEventOf");
            if (!Economics)
            {
                Puts("Economics not found!");
            }
            if ((string) Config["Default", "On"] == "true")
            {
                eventpause = timer.Once(60*(int) Config["Default", "PauseeventTime"], Startevent);
                time1 = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
            }
            LoadSavedData();
        }

        void Init()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                 {"StartEventRunner", "<color=#C4FF00>{0}</color>: Running man {1}\nKill him and get the reward!\nCommand: /run - to know the distance to the target."}, 
                 {"NotEnoughPlayers", "<color=#C4FF00>{0}</color>: There aren't enough players to start the event."},
                 {"RunnerSaved", "<color=#C4FF00>{0}</color>: {1} ran away from the chase and received a reward!"},
                 {"StillRunner", "<color=#C4FF00>{0}</color>: {1} your are still the runner."},
                 {"RunnerBackOnline", "<color=#C4FF00>{0}</color>: {1} is back online.\nKill him and get the reward!"},
                 {"RunnerKilled", "<color=#C4FF00>{0}</color>: Player - {1} kill {2} and received a reward!"},
                 {"RunnerDistance", "<color=#C4FF00>{0}</color>: Player - {1},\n is at a distance of {2}\nKill him and get the reward!"},
                 {"UntilEndOfEvent", "<color=#C4FF00>{0}</color>: Until the end of event left: {1} minutes"},
                 {"NotRunningEvent", "<color=#C4FF00>{0}</color>: At the moment the event is not running"},
                 {"UntilStartOfEvent", "<color=#C4FF00>{0}</color>: Before the start of the event remained: {1} minutes"},
                 {"RunCommandHelp", "Use \"/run\" to find out information about the running man"},
                 {"AdminCommandHelp", "Use \"/eventon\" for start event Running Man\nUse \"/eventoff\" for start event Running Man"},
                 {"NobodyOnline", "<color=#C4FF00>{0}</color>: You can't run event while there is nobody online"},
                 {"NoPerm", "<color=#C4FF00>{0}</color>: You have no rights to do this!"},
                 {"RunnerLeaved", "<color=#C4FF00>{0}</color>: {1} got scared and ran away!"},
                 {"EventStopped", "<color=#C4FF00>{0}</color>: Event has stopped!"}
            }, this);
        }

        protected override void LoadDefaultConfig()
        {
            SetConfig("Default", "ChatName", "EVENT");
            SetConfig("Default", "authLevel", 1);
            SetConfig("Default", "On", "true");
            SetConfig("Default", "Count", 2);
            SetConfig("Default", "StarteventTime", 30);
            SetConfig("Default", "PauseeventTime", 30);
            SetConfig("Default", "DisconnectPendingTimer", 30);
            SetConfig("Config", "Reward", "Random", "true");
            SetConfig("Config", "Reward", "RewardFixing", "wood");
            SetConfig("Config", "Reward", "RewardFixingAmount", 10000);
            SetConfig("Config", "Reward", "KarmaSystem", "PointToRemove", 0);
            SetConfig("Config", "Reward", "KarmaSystem", "PointToAdd", 1);
            SaveConfig();
        }

        void LoadSavedData()
        {
            SavedReward = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, int>>(nameof(RunningMan));
            if (SavedReward.Count == 0)
            {
                SavedReward = new Dictionary<string, int>
                {
                    {"Karma", 1},
                    {"wood", 10000},
                    {"stones", 10000},
                    {"metal.ore", 15000},
                    {"sulfur.ore", 15000},
                    {"smg.2", 1},
                    {"ammo.pistol.hv", 150},
                    {"rocket.launcher", 1},
                    {"money", 10000}
                };
                PrintWarning("Failed to load data file, generating a new one...");
            }
            SaveLoadedData();
        }

        void SaveLoadedData()
        {
            try
            {
                Interface.Oxide.DataFileSystem.WriteObject(nameof(RunningMan), SavedReward);
            }
            catch (Exception)
            {
                PrintWarning("Failed to save data file.");
            }
        }

        void Unload()
        {
            eventpause?.Destroy();
            eventstart?.Destroy();
            runningman = null;
            eventpause = null;
            eventstart = null;
        }

        void Reload()
        {
            eventpause?.Destroy();
            eventstart?.Destroy();
            runningman = null;
            eventpause = null;
            eventstart = null;
        }

        private void Startevent()
        {
            if (eventpause != null)
            {
                eventpause.Destroy();
                runningman = null;
            }
            if (eventstart != null)
            {
                eventstart.Destroy();
                runningman = null;
            }
            if (BasePlayer.activePlayerList != null && BasePlayer.activePlayerList.Count >= (int) Config["Default", "Count"])
            {
                var t = BasePlayer.activePlayerList;
                if (t == null)
                    return;
                var randI = rnd.Next(1, t.Count);
                runningman = t[randI];
                Runlog("Running man: " + runningman.displayName);
                BroadcastChat(string.Format(lang.GetMessage("StartEventRunner", this), (string) Config["Default", "ChatName"], runningman.displayName));
                eventstart = timer.Once(60*(int) Config["Default", "StarteventTime"], Runningstop);
                time1 = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
            }
            else
            {
                BroadcastChat(string.Format(lang.GetMessage("NotEnoughPlayers", this), (string) Config["Default", "ChatName"]));
                eventpause?.Destroy();
                eventpause = timer.Once(60*(int) Config["Default", "PauseeventTime"], Startevent);
                time1 = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
            }
        }

        void Runningstop()
        {
            Runlog("Running man - " + runningman.displayName + " ran away from the chase and received as a reward!");

            BroadcastChat(string.Format(lang.GetMessage("RunnerSaved", this), (string) Config["Default", "ChatName"], runningman.displayName));
            var inv = runningman.inventory;
            if ((string) Config["Config", "Reward", "Random"] == "true")
            {
                if (SavedReward == null)
                {
                    PrintWarning("Reward list is empty, please add items");
                    inv?.GiveItem(ItemManager.CreateByName((string) Config["Reward", "RewardFixing"],
                        (int) Config["Reward", "RewardFixingAmount"]), inv.containerMain);
                    return;
                }
                Runlog("random");
                var rand = SavedReward.ElementAt(rnd.Next(0, SavedReward.Count));
                switch (rand.Key)
                {
                    case "karma":
                        if (KarmaSystem != null && KarmaSystem.IsLoaded)
                        {
                            IPlayer player = covalence.Players.FindPlayerById(runningman.UserIDString);
                            KarmaSystem.Call("AddKarma", player, GetConfig<double>(1, "Config", "Reward", "KarmaSystem", "PointToAdd"));
                        }
                        else
                        {
                            inv?.GiveItem(ItemManager.CreateByName((string) Config["Config", "Reward", "RewardFixing"],
                                (int) Config["Config", "Reward", "RewardFixingAmount"]), inv.containerMain);
                        }
                        break;
                    case "money":
                        if (Economics != null && Economics.IsLoaded)
                        {
                            Economics?.CallHook("Deposit", runningman.userID,
                                rand.Value);
                        }
                        else
                        {
                            inv?.GiveItem(
                                ItemManager.CreateByName((string) Config["Config", "Reward", "RewardFixing"],
                                    (int) Config["Config", "Reward", "RewardFixingAmount"]), inv.containerMain);
                        }
                        break;
                    default:
                        Item item = ItemManager.CreateByName(rand.Key,
                            rand.Value);
                        if(item != null)
                            inv?.GiveItem(item, inv.containerMain);
                        else
                            PrintError($"Failed to create item...{rand.Key}");
                        break;
                }
            }
            else
            {
                inv?.GiveItem(ItemManager.CreateByName((string) Config["Config", "Reward", "RewardFixing"],
                    (int) Config["Config", "Reward", "RewardFixingAmount"]), inv.containerMain);
            }
            eventstart.Destroy();
            eventstart = null;
            runningman = null;
            Runlog("timer eventstart stopped");
            eventpause?.Destroy();
            eventpause = timer.Once(60*(int) Config["Default", "PauseeventTime"], Startevent);
            time1 = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        }

        void BroadcastChat(string msg = null) => rust.BroadcastChat(msg ?? " ") ;

        private void Runlog(string text)
        {
            Puts("[EVENT] +--------------- RUNNING MAN -----------------");
            Puts("[EVENT] | "+text);
            Puts("[EVENT] +---------------------------------------------");
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            if (runningman != null)
                if (player == runningman)
                {
                    stillRunnerTimer = timer.Once(60*(int) Config["Default", "DisconnectPendingTimer"], DestroyLeaveEvent);
                }
        }

        void OnPlayerInit(BasePlayer player)
        {
            if (runningman != null)
            {
                if (runningman == player)
                {
                    SendReply(player,
                        string.Format(lang.GetMessage("StillRunner", this, player.UserIDString), (string) Config["Default", "ChatName"], runningman.displayName));
                    BroadcastChat(string.Format(lang.GetMessage("RunnerBackOnline", this), (string) Config["Default", "ChatName"], runningman.displayName));
                    stillRunnerTimer?.Destroy();
                }
                else
                {
                    SendHelpText(player);
                }
            }
        }

        void PlayerKilled(BasePlayer victim, HitInfo hitinfo)
        {
            var attacker = hitinfo?.Initiator?.ToPlayer();
            if (attacker == null) return;
            if (victim == null) return;
            if (attacker == victim) return;
            if (runningman == null) return;
            if(victim != runningman) return;
            Runlog(string.Format(lang.GetMessage("RunnerKilled", this), (string) Config["Default", "ChatName"], attacker.displayName, runningman.displayName));
            BroadcastChat(string.Format(lang.GetMessage("RunnerKilled", this), (string) Config["Default", "ChatName"], attacker.displayName, runningman.displayName));
            var inv = attacker.inventory;
            if ((string) Config["Config", "Reward", "Random"] == "true")
            {
                if (SavedReward == null)
                {
                    PrintWarning("Reward list is empty, please add items, using FixingReward option...");
                    inv?.GiveItem(ItemManager.CreateByName((string) Config["Reward", "RewardFixing"],
                        (int) Config["Reward", "RewardFixingAmount"]), inv.containerMain);
                    return;
                }
                var rand = SavedReward.ElementAt(rnd.Next(0, SavedReward.Count));
                switch (rand.Key)
                {
                    case "karma":
                        if (KarmaSystem != null && KarmaSystem.IsLoaded)
                        {
                            IPlayer player = covalence.Players.FindPlayerById(attacker.UserIDString);
                            KarmaSystem.Call("AddKarma", player, GetConfig<double>(1, "Config", "Reward", "KarmaSystem", "PointToAdd"));
                        }
                        else
                        {
                            inv?.GiveItem(ItemManager.CreateByName((string) Config["Config", "Reward", "RewardFixing"],
                                (int) Config["Config", "Reward", "RewardFixingAmount"]), inv.containerMain);
                        }
                        break;
                    case "money":
                        if (Economics != null && Economics.IsLoaded)
                        {
                            Economics?.CallHook("Deposit", runningman.userID,
                                rand.Value);
                        }
                        else
                        {
                            inv?.GiveItem(
                                ItemManager.CreateByName((string) Config["Config", "Reward", "RewardFixing"],
                                    (int) Config["Config", "Reward", "RewardFixingAmount"]), inv.containerMain);
                        }
                        break;
                    default:
                        Runlog(rand.Key);
                        Item item = ItemManager.CreateByName(rand.Key,
                            rand.Value);
                        if(item != null)
                            inv?.GiveItem(item, inv.containerMain);
                        else
                            PrintError($"Failed to create item...{rand.Key}");
                        break;
                }
            }
            else
            {
                Puts(Config["Reward", "RewardFixing"].ToString());
                inv?.GiveItem(ItemManager.CreateByName((string) Config["Reward", "RewardFixing"],
                    (int) Config["Reward", "RewardFixingAmount"]), inv.containerMain);
            }
            eventstart?.Destroy();
            eventstart = null;
            runningman = null;
            Runlog("timer eventstart stopped");
            eventpause?.Destroy();
            eventpause = timer.Once(60*(int) Config["Default", "PauseeventTime"], Startevent);
            time1 = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        }

        void OnEntityDeath(BaseEntity entity, HitInfo hitinfo)
        {
            if (entity.ToPlayer())
                PlayerKilled(entity.ToPlayer(), hitinfo);
        }

        void cmdRun(BasePlayer player, string cmd, string[] args)
        {
            if (!player)
                return;
            if (runningman != null)
            {
                var xr = runningman.transform.position.x;
                var zr = runningman.transform.position.z;
                var xk = player.transform.position.x;
                var zk = player.transform.position.z;
                var dist = Math.Floor(Math.Sqrt(Math.Pow(xr - xk, 2) + Math.Pow(zr - zk, 2)));
                SendReply(player, string.Format(lang.GetMessage("RunnerDistance", this, player.UserIDString), (string) Config["Default", "ChatName"], runningman.displayName, dist));
                time2 = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
                var time3 = time2 - time1;
                time3 = eventstart.Delay - time3;
                time3 = Math.Floor(time3/60);
                SendReply(player, string.Format(lang.GetMessage("UntilEndOfEvent", this, player.UserIDString), (string) Config["Default", "ChatName"], time3));
            }
            else
            {
                time2 = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;

                var time3 = time2 - time1;
                if (eventpause != null)
                {
                    time3 = eventpause.Delay - time3;
                    time3 = Math.Floor(time3/60);
                    SendReply(player, string.Format(lang.GetMessage("NotRunningEvent", this, player.UserIDString), (string)Config["Default", "ChatName"]));
                    SendReply(player,
                        string.Format(lang.GetMessage("UntilStartOfEvent", this, player.UserIDString), (string)Config["Default", "ChatName"], time3));
                }
                else
                {
                    SendReply(player, lang.GetMessage("NotRunningEvent", this, player.UserIDString));
                }
            }
        }

        void SendHelpText(BasePlayer player)
        {
            player.ChatMessage(lang.GetMessage("RunCommandHelp", this, player.UserIDString));
            var authlevel = player.net.connection.authLevel;
            if (authlevel >= (int) Config["Default", "authLevel"]) 
            {
                player.ChatMessage(lang.GetMessage("AdminCommandHelp", this, player.UserIDString));
            }
        }

        void cmdEvent(BasePlayer player, string cmd, string[] args)
        {
            if (player.net.connection.authLevel >= (int) Config["Default", "authLevel"])
            {
                if (eventpause != null)
                {
                    eventpause.Destroy();
                    eventpause = null;
                    runningman = null;
                    Runlog("timer eventpause stopped");
                }
                if (eventstart != null)
                {
                    eventstart.Destroy();
                    eventstart = null;
                    runningman = null;
                    Runlog("timer eventstart stopped");
                }
                List<BasePlayer> onlineplayers = BasePlayer.activePlayerList;
                if (onlineplayers == null)
                {
                    SendReply(player, string.Format(lang.GetMessage("NobodyOnline", this, player.UserIDString), Config["Default", "ChatName"]));
                    return;
                }
                var randI = rnd.Next(0, onlineplayers.Count);
                runningman = onlineplayers[randI];
                Runlog("Running man: " + runningman.displayName);
                BroadcastChat(string.Format(lang.GetMessage("StartEventRunner", this), (string) Config["Default", "ChatName"], runningman.displayName));
                eventstart = timer.Once(60*(int) Config["Default", "StarteventTime"], Runningstop);
                time1 = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
            }
            else
                SendReply(player, string.Format(lang.GetMessage("NoPerm", this, player.UserIDString), (string) Config["Default", "ChatName"]));
        }

        void ccmdEvent(ConsoleSystem.Arg arg)
        {
            if (eventpause != null)
            {
                eventpause.Destroy();
                runningman = null;
                Runlog("timer eventpause stopped");
            }
            if (eventstart != null)
            {
                eventstart.Destroy();
                runningman = null;
                Runlog("timer eventstart stopped");
            }
            List<BasePlayer> onlineplayers = BasePlayer.activePlayerList;
            var randI = rnd.Next(0, onlineplayers.Count);
            runningman = onlineplayers[randI];
            Runlog("Running man: " + runningman.displayName);
            BroadcastChat(string.Format(lang.GetMessage("StartEventRunner", this), (string) Config["Default", "ChatName"], runningman.displayName));
            eventstart = timer.Once(60*(int) Config["Default", "StarteventTime"], Runningstop);
            time1 = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        }

        void cmdEventOf(ConsoleSystem.Arg arg)
        {
            DestroyEvent();
        }

        void DestroyEvent()
        {
            if (eventpause != null)
            {
                eventpause.Destroy();
                eventpause = null;
                runningman = null;
                Runlog("timer eventpause stopped");

            }
            if (eventstart != null)
            {
                eventstart.Destroy();
                eventstart = null;
                runningman = null;
                Runlog("timer eventstart stopped");
            }
            Runlog("Running Man has stopped");
        }

        void DestroyLeaveEvent()
        {
            Runlog("Player " + runningman.displayName + " got scared and ran away!");
            BroadcastChat(string.Format(lang.GetMessage("RunnerLeaved", this), (string) Config["Default", "ChatName"], runningman.displayName));
            if (eventpause != null)
            {
                eventpause.Destroy();
                eventpause = null;
                runningman = null;
                Runlog("timer eventpause stopped");

            }
            if (eventstart != null)
            {
                eventstart.Destroy();
                eventstart = null;
                runningman = null;
                Runlog("timer eventstart stopped");
            }
            Runlog("Running Man has stopped");
            eventpause = timer.Once(60*(int) Config["Default", "PauseeventTime"], Startevent);
            time1 = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        }

        void cmdEventOff(BasePlayer player, string cmd, string[] args)
        {
            if (player.net.connection.authLevel >= (int) Config["Default", "authLevel"])
            {
                if (eventpause != null)
                {
                    eventpause.Destroy();
                    eventpause = null;
                    runningman = null;
                    Runlog("timer eventpause stopped");
                }
                if (eventstart != null)
                {
                    eventstart.Destroy();
                    eventstart = null;
                    runningman = null;
                    Runlog("timer eventstart stopped");
                }
                Runlog("Running Man has stopped");
                SendReply(player, string.Format(lang.GetMessage("EventStopped", this, player.UserIDString), Config["Default", "ChatName"]));
            }
            else
                SendReply(player, string.Format(lang.GetMessage("NoPerm", this, player.UserIDString), Config["Default", "ChatName"]));
        }
    }
}