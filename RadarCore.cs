using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.Game;
using Sandbox.ModAPI;
using Sandbox.Game.Entities;
using VRage.Game.Components;
using VRage.Game;
using VRageMath;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;

namespace RadarBlock
{
	
	
	public class RadarSettings
	{
		public int CheckRadarSeconds = 5; // update radars how often (in seconds)
		public int CheckActiveSeconds = 20; // delay before active radar can scan
		public int ClearHUDListEverySeconds = 4; // hud list cleanup
		public float modeSwitchRange = 10000; // broadcast range at which switches between passive and active
		
		public int UpdateRadarSeconds = 1; // obsolete, I think
		public int UpdateLCDSeconds = 9; // obsolete, I think
	}

	[MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
	public class RadarCore : MySessionComponentBase
    {

		private static DateTime m_lastUpdate;
		private bool m_initialized;
        private static DateTime process;
		public static Dictionary<RadarData, int> activeRadars = new Dictionary<RadarData, int>();
		public static StringBuilder detailInfo;
		public static Settings config;
        public static bool isServer;
        public static RadarSettings radarSettings;
        public static bool OverwriteSettings;



        private void Initialize()
		{

            long clientId = 0;
            try
            {
                if (!MyAPIGateway.Multiplayer.IsServer)
                {
                    clientId = MyAPIGateway.Session.LocalHumanPlayer.IdentityId;
                } 
            }
            catch (Exception exc)
            {
                return;
            }

            MyAPIGateway.TerminalControls.CustomControlGetter += RadarButtons.CreateControlsNew;
            MyAPIGateway.TerminalControls.CustomActionGetter += RadarButtons.CreateActionsNew;
            MyAPIGateway.Multiplayer.RegisterMessageHandler(4110, Comms.StringMessageHandler);
            MyAPIGateway.Multiplayer.RegisterMessageHandler(4111, Comms.ObjectMessageHandler);

            if (MyAPIGateway.Multiplayer.IsServer)
            {
                isServer = true;
                config = Settings.LoadConfig();
                radarSettings = GetRadarSettings();
                process = DateTime.Now;
            }
            else
            {
                if (clientId == 0) return;
                Comms.SendMessageToServer("SyncConfig", clientId);
            }

            m_initialized = true;
            m_lastUpdate = DateTime.Now;
        }

        private static RadarSettings GetRadarSettings()
        {
            RadarSettings _radarConfig = new RadarSettings();

            if (config.ModeSwitchAtBroadcastRange <= 50000)
            {

                _radarConfig.modeSwitchRange = config.ModeSwitchAtBroadcastRange;
            }
            _radarConfig.CheckActiveSeconds = config.DelayInSecondsActiveScan;
            _radarConfig.ClearHUDListEverySeconds = _radarConfig.CheckRadarSeconds - 1;

            return _radarConfig;
        }

        public static void ServerGetConfig(string message)
        {
            var split = message.Split('\n');
            long playerId = 0;
            IMyPlayer thisPlayer = null;
            long.TryParse(split[1], out playerId);
            if (playerId == 0) return;

            List<IMyPlayer> playerList = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(playerList);

            foreach(var player in playerList)
            {
                if (player.IdentityId != playerId) continue;
                thisPlayer = player;
                break;
            }

            if (thisPlayer == null) return;
            Comms.SendSettingsToPlayer(config, thisPlayer);
        }

        public static void SyncConfig(Settings serverConfig)
        {

            config = Settings.LoadClientConfig(serverConfig);
            radarSettings = GetRadarSettings();
        }

        /// <summary>
        /// Simluation loop
        /// </summary>
        public override void UpdateBeforeSimulation()
        {
            // Sanity check
            if (MyAPIGateway.Session == null)
                return;

            if (!m_initialized)
            {
                Initialize();
                return;
            }

            if (!m_initialized) return;
            if (radarSettings == null || config == null) return;

            // Check our radar blocks once every second
            if (DateTime.Now - m_lastUpdate <= TimeSpan.FromSeconds(radarSettings.UpdateRadarSeconds)) return;

            if (isServer) ServerCheckRadars();

            // Everyone check for passive GPS markers and clear
            if (RadarProcess.passiveGPS) HudMarkManager.Process(false);

            // Everyone Processes Radar Blocks
           // RadarProcess.Process();

            RadarProcess.ProcessActiveRadars();

            m_lastUpdate = DateTime.Now;

            base.UpdateBeforeSimulation();
        }

        void ServerCheckRadars()
        {
            // Check all radars blocks if active/passive on server and send to all clients
            if (!isServer) return;
            if (OverwriteSettings) Settings.SaveConfig(config);
            if (RadarLogic.LastRadarUpdate.Count == 0) return;
            bool updateTime = false;
            foreach (var item in RadarLogic.LastRadarUpdate)
            {
                // if (DateTime.Now - item.LastUpdate <= TimeSpan.FromSeconds(RadarSettings.CheckRadarSeconds)) continue;
                // item.LastUpdate = DateTime.Now;
                if (item == null) continue;
                IMyEntity blockEntity = null;
                MyAPIGateway.Entities.TryGetEntityById(item.EntityId, out blockEntity);
                if (blockEntity == null || blockEntity.MarkedForClose) continue;
                var beacon = blockEntity as IMyBeacon;
                if (beacon == null) continue;
                if (!beacon.IsWorking) continue;

                Comms.SendToOtherPlayers(item);
                if (!beacon.IsWorking || !beacon.IsFunctional || !beacon.Enabled) continue;
                if (beacon.Radius >= radarSettings.modeSwitchRange)
                {

                   // item.ActiveRadar = true;
                    if (item.ActiveRadar == false && item.EmissiveMode != "Idle" && item.EmissiveMode != "Active")
                    {
                        RadarLogic.ChangeEmissive("Idle", beacon, item, true);
                        LCDManager.Clear(blockEntity.GetTopMostParent(), item.SelectedLCD);
                    }

                    continue;
                }
                else
                {
                    if (item.EmissiveMode != "Passive")
                    {
                        RadarLogic.ChangeEmissive("Passive", beacon, item, true);

                    }
                }
                if (DateTime.Now - process <= TimeSpan.FromSeconds(RadarCore.radarSettings.CheckRadarSeconds)) continue;
                RadarProcess.Process(blockEntity, "Passive", item);
                updateTime = true;
            }

            if (updateTime)
            {
                process = DateTime.Now;
            }
        }
		
		public static void ActiveScan(IMyTerminalBlock block){

			if(block as IMyBeacon == null)return;
			var radar = block as IMyBeacon;
			if(radar == null)return;

            // Add active radar to active radar list
            RadarData data = RadarLogic.LoopRadarSync(block.EntityId);
            if (!activeRadars.ContainsKey(data)){

				detailInfo = GetStringBuilderText("ScanInProgress");
				UpdateBlockDetails(block);
                HudMarkManager.Process(true);
				RadarLogic.ChangeEmissive("Active", radar, null, true);
				activeRadars.Add(data, radarSettings.CheckActiveSeconds);
				
				string s = "AddActive" + "\n";
				s += block.EntityId.ToString();

                if (!isServer) Comms.SendMessageToServer(s);
                if (isServer) MyAPIGateway.Utilities.SendModMessage(1765707984, block.EntityId);

                Comms.SendToOtherPlayers(s);
			}
		}
		
		public static bool GetRoidFilter(IMyTerminalBlock block){
			
			bool storedValue = true;
            RadarData item = RadarLogic.LoopRadarSync(block.EntityId);
            if (item == null)
            {

               // MyVisualScriptLogicProvider.ShowNotification("Data Server nulled", 10000, "Red");
                return true;
            }

            storedValue = item.filterRoids;
            return storedValue;
			
		}
		
		public static void SetRoidFilter(IMyTerminalBlock block, bool boxValue)
        {
			try
            {
				
				RadarData data = RadarLogic.LoopRadarSync(block.EntityId);
				if(data != null){
					
					data.filterRoids = boxValue;
					//MyVisualScriptLogicProvider.ShowNotification("Set Roid filter = " +data.filterRoids.ToString(), 10000, "Red");
				}
			
				
				string message = "FilterToServer" + "\n";
				message += "RoidFilter" + "\n";
				message += block.EntityId.ToString() + "\n";
				message += boxValue.ToString() + "\n";

                Comms.SendMessageToServer(message);
				
			}catch(Exception exc){

                VRage.Utils.MyLog.Default.WriteLineAndConsole($"Failed Setting Roid Filter To Block {exc}");
            }
		}

        public static bool GetFriendlyFilter(IMyTerminalBlock block){

            bool storedValue = true;
            RadarData item = RadarLogic.LoopRadarSync(block.EntityId);
            if (item == null)
            {

               // MyVisualScriptLogicProvider.ShowNotification("Data Server nulled", 10000, "Red");
                return true;
            }

            storedValue = item.filterFriendly;
            return storedValue;

        }
		
		public static void SetFriendlyFilter(IMyTerminalBlock block, bool boxValue){

            try
            {

                RadarData data = RadarLogic.LoopRadarSync(block.EntityId);
                if (data != null)
                {

                    data.filterFriendly = boxValue;
                   // MyVisualScriptLogicProvider.ShowNotification("Set Friendly filter = " + data.filterFriendly.ToString(), 10000, "Red");
                }


                string message = "FilterToServer" + "\n";
                message += "FriendlyFilter" + "\n";
                message += block.EntityId.ToString() + "\n";
                message += boxValue.ToString() + "\n";

                Comms.SendMessageToServer(message);

            }
            catch (Exception exc)
            {

                VRage.Utils.MyLog.Default.WriteLineAndConsole($"Failed Setting Friendly Filter To Block {exc}");
            }
        }

        public static bool GetNPCFilter(IMyTerminalBlock block)
        {
            bool storedValue = true;
            RadarData item = RadarLogic.LoopRadarSync(block.EntityId);
            if (item == null)
            {

               // MyVisualScriptLogicProvider.ShowNotification("Data Server nulled", 10000, "Red");
                return true;
            }

            storedValue = item.filterNPC;
            return storedValue;
        }

        public static void SetNPCFilter(IMyTerminalBlock block, bool boxValue)
        {
            try
            {

                RadarData data = RadarLogic.LoopRadarSync(block.EntityId);
                if (data != null)
                {

                    data.filterNPC = boxValue;
                   // MyVisualScriptLogicProvider.ShowNotification("Set NPC filter = " + data.filterNPC.ToString(), 10000, "Red");
                }


                string message = "FilterToServer" + "\n";
                message += "NPCFilter" + "\n";
                message += block.EntityId.ToString() + "\n";
                message += boxValue.ToString() + "\n";

                Comms.SendMessageToServer(message);

            }
            catch (Exception exc)
            {

                VRage.Utils.MyLog.Default.WriteLineAndConsole($"Failed Setting NPC Filter To Block {exc}");
            }
        }

        public static void ClearGPS(IMyTerminalBlock block)
        {
            HudMarkManager.Process(true);
        }

        public static void GetLCDs(IMyTerminalBlock block, List<MyTerminalControlListBoxItem> listItems, List<MyTerminalControlListBoxItem> selectedItems)
        {
            RadarData item = RadarLogic.LoopRadarSync(block.EntityId);
            if (item == null) return;
            string objectText = "abc";
            var dummy = new MyTerminalControlListBoxItem(MyStringId.GetOrCompute("-Select LCD Below-"), MyStringId.GetOrCompute("-Select LCD Below-"), objectText);
            listItems.Add(dummy);
            if (item.SelectedLCD == "" || item.SelectedLCD == "-Select LCD Below-")
            {
                //var dummy = new MyTerminalControlListBoxItem(MyStringId.GetOrCompute("-Select LCD Below-"), MyStringId.GetOrCompute("-Select LCD Below-"), objectText);
               // listItems.Add(dummy);
                selectedItems.Add(dummy);

            }
            else
            {
                //var dummy = new MyTerminalControlListBoxItem(MyStringId.GetOrCompute("-Select LCD Below-"), MyStringId.GetOrCompute("-Select LCD Below-"), objectText);
               // var selectedLCD = new MyTerminalControlListBoxItem(MyStringId.GetOrCompute(item.SelectedLCD), MyStringId.GetOrCompute(item.SelectedLCD), objectText);
                //listItems.Add(dummy);
                //selectedItems.Add(selectedLCD);
            }

            IMyEntity parent = block.GetTopMostParent();
            IMyCubeGrid Grid = parent as IMyCubeGrid;
            if (Grid == null) return;

            List<IMyTextPanel> Blocks = new List<IMyTextPanel>();
            MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(Grid).GetBlocksOfType(Blocks);
            foreach(var lcds in Blocks)
            {
                if (lcds == null) continue;
                if (string.IsNullOrEmpty(lcds.CustomName)) continue;
                var toList = new MyTerminalControlListBoxItem(MyStringId.GetOrCompute(lcds.CustomName), MyStringId.GetOrCompute(lcds.CustomName), objectText);
                if (lcds.CustomName == item.SelectedLCD)
                {
                    selectedItems.Add(toList);
                }
                listItems.Add(toList);

            }
        }

        public static void SetSelectedLCD(IMyTerminalBlock block, List<MyTerminalControlListBoxItem> listItems)
        {
            if (listItems.Count == 0) return;

            try
            {

                if (isServer)
                {
                    RadarData item = RadarLogic.LoopRadarSync(block.EntityId);
                    if (item == null) return;
                    LCDManager.Clear(block.GetTopMostParent(), item.SelectedLCD);
                    item.SelectedLCD = listItems[0].Text.ToString();
                    RadarLogic.SaveState(item.EntityId, item);
                    //MyAPIGateway.Utilities.SetVariable<string>("SelectedLCD" + block.EntityId.ToString(), listItems[0].Text.ToString());
                    //MyVisualScriptLogicProvider.ShowNotification($"Set LCD = {listItems[0].Text}", 10000, "Red");
                }
                else
                {
                    RadarData data = RadarLogic.LoopRadarSync(block.EntityId);
                    if (data == null) return;
                    LCDManager.Clear(block.GetTopMostParent(), data.SelectedLCD);
                    if (data != null)
                    {
                        data.SelectedLCD = listItems[0].Text.ToString();
                       // MyVisualScriptLogicProvider.ShowNotification($"Set LCD = {listItems[0].Text}", 10000, "Red");
                    }

                    /*string message = "SetSelectedLCD" + "\n";
                    message += listItems[0].Text.ToString() + "\n";
                    message += block.EntityId.ToString() + "\n";*/

                    Comms.SendRadarDataToServer(data);
                }
            }
            catch (Exception exc)
            {

            }

        }

        public static void UpdateBlockDetails(IMyTerminalBlock block){

			var localPlayer = MyAPIGateway.Session.LocalHumanPlayer;
			if(localPlayer == null)return;

            var radar = block as IMyBeacon;
            if (radar == null) return;
            if (!radar.BlockDefinition.SubtypeName.Contains("Radar")) return;

            var getCustomInfo = block.CustomInfo;
            block.RefreshCustomInfo();

            if (MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel){

				if(getCustomInfo != block.CustomInfo){
					
					var myCubeBlock = block as MyCubeBlock;
						
					if(myCubeBlock.IDModule != null){
							
						var share = myCubeBlock.IDModule.ShareMode;
						var owner = myCubeBlock.IDModule.Owner;
						myCubeBlock.ChangeOwner(owner, share == MyOwnershipShareModeEnum.None ? MyOwnershipShareModeEnum.Faction : MyOwnershipShareModeEnum.None);
						myCubeBlock.ChangeOwner(owner, share);
							
					}
				}
			}
		}

        public static void UpdateBeaconHudText(IMyBeacon radar, string text)
        {
            if(text == "ActiveReady")
            {
                radar.HudText = "Active Radar Online";
            }

            if(text == "ActiveScanning")
            {
                radar.HudText = "Active Radar Scanning In Progress...";
            }

            if(text == "Passive")
            {
                radar.HudText = "Passive Radar Scanning In Progress...";
            }
        }
		
		public static void AppendCustomInfo(IMyTerminalBlock block, StringBuilder text){
			
			//RadarData data = RadarLogic.LoopRadarSync(block.EntityId);
			//if(data == null)return;
				
			text.Clear();
			text.Append(detailInfo);

			//MyVisualScriptLogicProvider.ShowNotification("Details Updated", 10000, "Green");
		}
		
		public static StringBuilder GetStringBuilderText(string title, int count = 0)
        {
			
			StringBuilder sb = new StringBuilder();
			if(title == "ScanInProgress")
            {
				sb.AppendLine();
                if (config.RequireStationaryWhileActiveScanning) sb.Append("Grid Must Be Stationary While Scanning!").AppendLine().AppendLine();
				sb.Append("Active Scanning In Progress....\nDelay "+radarSettings.CheckActiveSeconds.ToString()+ " seconds").AppendLine().AppendLine();
				sb.Append("[Scanning Mode = Active]");
			}
			
			if(title == "ActiveCountdown")
            {
				sb.AppendLine();
                if (config.RequireStationaryWhileActiveScanning) sb.Append("Grid Must Be Stationary While Scanning!").AppendLine().AppendLine();
                sb.Append("Active Scanning In Progress....\nDelay "+count.ToString()+ " seconds").AppendLine().AppendLine();
				sb.Append("[Scanning Mode = Active]");
			}
			
			if(title == "ActiveReady")
            {
				sb.AppendLine();
				sb.Append("[Scanning Mode = Active]").AppendLine();
				sb.Append("Ready To Scan");
			}

            if(title == "Passive")
            {
                sb.AppendLine();
                sb.Append("[Scanning Mode = Passive]").AppendLine();
            }

            if(title == "Offline")
            {
                sb.AppendLine();
                sb.Append("Radar is offline").AppendLine();
            }
			
			return sb;
		}
		
		public static void SyncActiveRadar(string data)
        {
			var split = data.Split('\n');
			
			long blockEntityId = 0;
			IMyEntity entity = null;
            long.TryParse(split[1], out blockEntityId);

			if(blockEntityId == 0)return;
			
			MyAPIGateway.Entities.TryGetEntityById(blockEntityId, out entity);
			if(entity == null)return;
			var block = entity as IMyTerminalBlock;
			if(block == null)return;

            RadarData item = RadarLogic.LoopRadarSync(blockEntityId);
			if(!activeRadars.ContainsKey(item)){

				//detailInfo = GetStringBuilderText("ScanInProgress");
				//UpdateBlockDetails(block);
				//HudMarkManager.Process(true);
				//RadarLogic.ChangeEmissive("Active");
				activeRadars.Add(item, radarSettings.CheckActiveSeconds);
			}

            if (isServer)
            {
                MyAPIGateway.Utilities.SendModMessage(1765707984, blockEntityId);
            }
		}
		
		public static void SyncEmissives(string message){

			var split = message.Split('\n');
			
			long EntityId = 0;
			string mode = "";
			IMyEntity blockEntity = null;
			
			mode = split[1];
			if(long.TryParse(split[2], out EntityId) == false)return;
			if(EntityId == 0)return;
			if(MyAPIGateway.Entities.TryGetEntityById(EntityId, out blockEntity) == false)return;
			if(blockEntity == null || mode == "")return;
			var radar = blockEntity as IMyBeacon;
			if(radar == null)return;
            RadarData data = RadarLogic.LoopRadarSync(EntityId);
			
			//MyVisualScriptLogicProvider.ShowNotification("Emissives Update", 10000, "Green");
			
			RadarLogic.ChangeEmissive(mode, radar, data);
		}
		
		public static void SyncFilters(string message)
        {
			var split = message.Split('\n');
			
			long EntityId = 0;
			string filter = "";
			bool filterState = true;
			
			filter = split[1];
            long.TryParse(split[2], out EntityId);
            bool.TryParse(split[3], out filterState);
			
			if(EntityId == 0 || filter == "")return;
			//MyAPIGateway.Utilities.SetVariable<bool>(filter+EntityId.ToString(), filterState);
			
			RadarData item = RadarLogic.LoopRadarSync(EntityId);
			if(item == null)return;

            if(filter == "RoidFilter")
            {
                item.filterRoids = filterState;
            }

            if(filter == "FriendlyFilter")
            {
                item.filterFriendly = filterState; 
            }

            if(filter == "NPCFilter")
            {
                item.filterNPC = filterState;
            }

            RadarLogic.SaveState(EntityId, item);

            //VRage.Utils.MyLog.Default.WriteLineAndConsole("Server Filter Synced : Filter = " +filter+ " Filter State = " +filterState.ToString());
        }

        public static void SyncLCD(RadarData cData)
        {

            if (cData == null) return;
            RadarData sData = RadarLogic.LoopRadarSync(cData.EntityId);
            if (sData == null) return;

            sData.SelectedLCD = cData.SelectedLCD;
            RadarLogic.SaveState(sData.EntityId, sData);
        }

        /*public static void SyncLCD(string message)
        {
             var split = message.Split('\n');

             long EntityId = 0;
             string lcdName = split[1];
             long.TryParse(split[2], out EntityId);

             if (EntityId == 0) return;
             MyAPIGateway.Utilities.SetVariable<string>("SelectedLCD"+EntityId.ToString(), lcdName);
             RadarData data = RadarLogic.LoopRadarSync(EntityId);
             if (data == null) return;
             data.SelectedLCD = lcdName;
         }*/

        public static double MeasureDistance(Vector3D coordsStart, Vector3D coordsEnd){

             double distance = Math.Round( Vector3D.Distance( coordsStart, coordsEnd ), 2 );
             return distance;

         }

         /*public static void UpdateRadar(string message)
         {
             var split = message.Split('\n');

             long EntityId = 0;
             long.TryParse(split[1], out EntityId);
             if (EntityId == 0) return;
             RadarData serverData = RadarLogic.LoopRadarSync(EntityId);
             if (serverData == null) return;
             serverData.LastUpdate = DateTime.Now;
         }*/

            public static RadarData LoopActiveRadars(long entityId)
        {

            foreach(var item in activeRadars.Keys)
            {
                if(item.EntityId == entityId)
                {
                    return item;
                }
            }

            return null;
        }

		/// <summary>
		/// Mod is unloading
		/// </summary>
		protected override void UnloadData()
		{
			try
			{
				if (Logging.Instance != null)
					Logging.Instance.Close();
				
				MyAPIGateway.TerminalControls.CustomControlGetter -= RadarButtons.CreateControlsNew;
				MyAPIGateway.TerminalControls.CustomActionGetter -= RadarButtons.CreateActionsNew;
				MyAPIGateway.Multiplayer.UnregisterMessageHandler(4110, Comms.StringMessageHandler);
                MyAPIGateway.Multiplayer.UnregisterMessageHandler(4111, Comms.ObjectMessageHandler);
                //MyAPIGateway.Multiplayer.UnregisterMessageHandler(4120, RadarProcess.RadarSyncMessageHandler);

                /*if(MyAPIGateway.Multiplayer.IsServer){
					
					MyVisualScriptLogicProvider.PlayerConnected -= ProcessConnectedPlayer;
				}*/
            }
			catch { }

            RadarProcess.clearTimer.Stop();
            //if (RadarProcess.clearTimer != null && RadarProcess.clearTimer.Enabled)
            //    RadarProcess.clearTimer.Stop();

			base.UnloadData();
		}
    }
}
