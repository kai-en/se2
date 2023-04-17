using System;
using System.Collections.Generic;

using Sandbox.ModAPI;
using Sandbox.Game;
using Sandbox.Common.ObjectBuilders;
using VRageMath;
using VRage.ModAPI;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.ObjectBuilders;
using VRage.Game.ModAPI;
using VRage.Utils;
using Sandbox.Game.EntityComponents;

namespace RadarBlock
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Beacon), false, "LargeBlockRadar")]
    public class RadarLogic : MyGameLogicComponent
	{
		// Contains the list of radar blocks in the world
		public static List<RadarData> LastRadarUpdate
		{
			get
			{
                if (m_lastUpdate == null) return new List<RadarData>();
				return m_lastUpdate;
			}
		}

		private MyObjectBuilder_EntityBase m_objectBuilder = null;
		private static List<RadarData> m_lastUpdate = null;
        private bool scriptInit = false;
		public IMyBeacon beacon = null;
        public IMyEntity radarEntity = null;
        private IMyCubeBlock CubeBlock = null;
		private MyEntitySubpart subpart= null;
		private static string emissiveName = "Emissive";
		private static Color activeColor = new Color(150,0,255,255);
		private static Color passiveColor = new Color(255,255,0,255);
		private static Color idleColor = new Color(0,255,0,255);
		private static Color inactiveColor = new Color(255,0,0,255);
        private Vector3 BlockColor = new Vector3(0, 0, 0);
        private MyStringHash texture;
		private bool nonFunctional = false;
        public static Guid cpmID = new Guid("B9916634-2230-41E3-8E77-15C233F8B1B2");

        /// <summary>
        /// So, uhm.  Init on a beacon passes a null objectBuilder?  This can't be right.
        /// </summary>
        /// <param name="objectBuilder"></param>
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
		{
            base.Init(objectBuilder);

            if (m_lastUpdate == null)
			{
				m_lastUpdate = new List<RadarData>();
			}

            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME | MyEntityUpdateEnum.EACH_FRAME | MyEntityUpdateEnum.EACH_100TH_FRAME;
            Logging.Instance.WriteLine("Radar Block Initialized");

        }
		
		public override void UpdateOnceBeforeFrame()
        {

            base.UpdateOnceBeforeFrame();

            beacon = (IMyBeacon)Entity;

            // Both Server and Clients Run This
            AllInit();

            if (MyAPIGateway.Multiplayer.IsServer) InitServer();

        }
		
		public override void UpdateBeforeSimulation()
        {
			base.UpdateBeforeSimulation();

            // Clients and Server are required to run this
            DishRotationUpdate();

            //if (scriptInit) return;
            //AllInit();
		}

        public override void UpdateBeforeSimulation100()
        {
            base.UpdateBeforeSimulation100();

            // Clients and Server are required to run this
            NewSubPartCheck();
        }

        private void AllInit()
        {
            scriptInit = true;
            if (beacon == null) return;
            radarEntity = beacon as IMyEntity;
            beacon.TryGetSubpart("radar", out subpart);
            BlockColor = beacon.SlimBlock.ColorMaskHSV;
            texture = beacon.SlimBlock.SkinSubtypeId;
            CubeBlock = Entity as IMyCubeBlock;
            beacon.AppendingCustomInfo += RadarCore.AppendCustomInfo;
            CubeBlock.IsWorkingChanged += WorkingStateChange;
            WorkingStateChange(CubeBlock);

        }

        private void InitServer()
        {
            // Since our object builder is null, use Entity.
            if (beacon == null)
            {
                Logging.Instance.WriteLine("Entity is null");
            }
            else if (beacon.BlockDefinition.SubtypeName.Contains("Radar"))
            {
                bool isActive = false;
                string emissive = "Passive";
                bool roidFilter = true;
                bool filterFriendly = true;
                bool filterNPC = true;
                string lcdName = "";
                RadarData loadData;
                byte[] byteData;
                bool loadDefault = true;
                try
                {

                    // MyAPIGateway.Utilities.GetVariable<bool>("RoidFilter" + beacon.EntityId.ToString(), out roidFilter);
                    // MyAPIGateway.Utilities.GetVariable<bool>("FriendlyFilter" + beacon.EntityId.ToString(), out filterFriendly);
                    // MyAPIGateway.Utilities.GetVariable<bool>("NPCFilter" + beacon.EntityId.ToString(), out filterNPC);
                    // MyAPIGateway.Utilities.GetVariable<string>("SelectedLCD" + beacon.EntityId.ToString(), out lcdName);


                    if (radarEntity.Storage != null)
                    {

                        string storage = radarEntity.Storage[cpmID];
                        byteData = Convert.FromBase64String(storage);
                        loadData = MyAPIGateway.Utilities.SerializeFromBinary<RadarData>(byteData);

                        var newData = new RadarData();
                        newData.EntityId = Entity.EntityId;
                        newData.EmissiveMode = emissive;
                        newData.ActiveRadar = isActive;
                        newData.filterFriendly = loadData.filterFriendly;
                        newData.filterRoids = loadData.filterRoids;
                        newData.filterNPC = loadData.filterNPC;
                        newData.SelectedLCD = loadData.SelectedLCD;
                        ChangeEmissive(emissive, beacon, newData);
                        if (!m_lastUpdate.Contains(newData))
                        {
                            m_lastUpdate.Add(newData);

                        }

                        loadDefault = false;

                    }

                    if (loadDefault)
                    {
                        var data = new RadarData();

                        data.EntityId = Entity.EntityId;
                        data.EmissiveMode = emissive;
                        data.ActiveRadar = isActive;
                        data.filterRoids = roidFilter;
                        data.filterFriendly = filterFriendly;
                        data.filterNPC = filterNPC;
                        data.SelectedLCD = lcdName;
                        ChangeEmissive(emissive, beacon, data);
                        if (!m_lastUpdate.Contains(data))
                        {
                            m_lastUpdate.Add(data);
                        }

                        if (radarEntity.Storage == null)
                        {
                            radarEntity.Storage = new MyModStorageComponent();

                            var newByteData = MyAPIGateway.Utilities.SerializeToBinary<RadarData>(data);
                            var base64string = Convert.ToBase64String(newByteData);
                            radarEntity.Storage[cpmID] = base64string;
                        }
                    }

                }
                catch (Exception exc)
                {
                    MyLog.Default.WriteLineAndConsole($"Failed To Get Block Settings {exc}");
                }
            }

            /*// Since our object builder is null, use Entity.
            if (beacon == null)
            {
                Logging.Instance.WriteLine("Entity is null");
            }
            else if (beacon.BlockDefinition.SubtypeName.Contains("Radar"))
            {
                bool isActive = false;
                string emissive = "Passive";
                bool roidFilter = true;
                bool filterFriendly = true;
                bool filterNPC = true;
                string lcdName = "";
                try
                {

                    MyAPIGateway.Utilities.GetVariable<bool>("RoidFilter" + beacon.EntityId.ToString(), out roidFilter);
                    MyAPIGateway.Utilities.GetVariable<bool>("FriendlyFilter" + beacon.EntityId.ToString(), out filterFriendly);
                    MyAPIGateway.Utilities.GetVariable<bool>("NPCFilter" + beacon.EntityId.ToString(), out filterNPC);
                    MyAPIGateway.Utilities.GetVariable<string>("SelectedLCD" + beacon.EntityId.ToString(), out lcdName);

                }
                catch (Exception exc)
                {
                    MyLog.Default.WriteLineAndConsole($"Failed To Get Block Settings {exc}");
                }

                var data = new RadarData();

                data.EntityId = Entity.EntityId;
                data.EmissiveMode = emissive;
                data.ActiveRadar = isActive;
                data.filterRoids = roidFilter;
                data.filterFriendly = filterFriendly;
                data.filterNPC = filterNPC;
                data.SelectedLCD = lcdName;
                ChangeEmissive(emissive, beacon, data);
                if (!m_lastUpdate.Contains(data))
                {
                    m_lastUpdate.Add(data);

                }
            }*/
        }

        private void DishRotationUpdate()
        {
            if (beacon == null) return;
            if (subpart == null) return;
            if (!beacon.IsWorking) return;

            subpart.PositionComp.LocalMatrix = Matrix.CreateRotationY(0.01f) * subpart.PositionComp.LocalMatrix;
        }

        private void NewSubPartCheck()
        {

            if (beacon == null) return;
            if (beacon.IsFunctional == false)
            {
                nonFunctional = true;
            }
            else
            {
                if (nonFunctional == true)
                {

                    nonFunctional = false;
                    beacon.TryGetSubpart("radar", out subpart);
                    CubeBlock = Entity as IMyCubeBlock;
                    WorkingStateChange(CubeBlock);
                }
            }

            if (BlockColor != beacon.SlimBlock.ColorMaskHSV)
            {
                beacon.TryGetSubpart("radar", out subpart);
                CubeBlock = Entity as IMyCubeBlock;
                WorkingStateChange(CubeBlock);
                BlockColor = beacon.SlimBlock.ColorMaskHSV;

            }

            if (texture != beacon.SlimBlock.SkinSubtypeId)
            {
                beacon.TryGetSubpart("radar", out subpart);
                CubeBlock = Entity as IMyCubeBlock;
                WorkingStateChange(CubeBlock);
                texture = beacon.SlimBlock.SkinSubtypeId;
            }
        }

        public static void AddRadarToClient(RadarData newData)
        {
            try
            {
                if (newData == null) return;
                bool exists = false;
                int index = -1;
                if(m_lastUpdate == null)
                {
                    m_lastUpdate = new List<RadarData>();
                }

                if(m_lastUpdate.Count != 0)
                {
                    foreach(var item in m_lastUpdate)
                    {
                        //	TODO {Thraxus}	This can be change to reduce nesting (easier to read): if (item.EntityId != newData.EntityId) continue;
                        //	TODO {Thraxus}	See comment below as to why reduced nesting is a good thing
                        if (item == null) continue;
                        if (item.EntityId != newData.EntityId) continue;

                        exists = true;

                        item.EmissiveMode = newData.EmissiveMode;
                        item.ActiveRadar = newData.ActiveRadar;
                        item.filterRoids = newData.filterRoids;
                        item.filterFriendly = newData.filterFriendly;
                        item.filterNPC = newData.filterNPC;
                        item.SelectedLCD = newData.SelectedLCD;
                        break;
                    }
                }
                

                /*if (exists)
                {
                    m_lastUpdate[index] = newData;
                }*/

                /*	TODO {Thraxus}	Can change this to an Object Initializer: 
				var newItem = new RadarData
					{
						EntityId = newData.EntityId,
						EmissiveMode = newData.EmissiveMode,
						ActiveRadar = newData.ActiveRadar,
						filterRoids = newData.filterRoids,
						filterFriendly = newData.filterFriendly,
						filterNPC = newData.filterNPC
					};
					t_radarList.Add(newItem);
				*/

                if (!exists)
                {
                    var newItem = new RadarData();

                    newItem.EntityId = newData.EntityId;
                    newItem.EmissiveMode = newData.EmissiveMode;
                    newItem.ActiveRadar = newData.ActiveRadar;
                    newItem.filterRoids = newData.filterRoids;
                    newItem.filterFriendly = newData.filterFriendly;
                    newItem.filterNPC = newData.filterNPC;
                    newItem.SelectedLCD = newData.SelectedLCD;
                    m_lastUpdate.Add(newItem);
                }
            }
            catch (Exception exc)
            {

                VRage.Utils.MyLog.Default.WriteLineAndConsole($"Radar Sync Communication Failed {exc.ToString()}");

            }
        }

        private void WorkingStateChange(IMyCubeBlock block){

			var radar = block as IMyBeacon;
			if(radar == null)return;
			
			if(block.IsFunctional == false || block.IsWorking == false){
				
				try{
					
					ChangeEmissive("Inactive", radar, null, true);
                    RadarData item = LoopRadarSync(block.EntityId);
                    if (item == null) return;
                    if (item.ActiveRadar) return;
                    LCDManager.Clear(block.GetTopMostParent(), item.SelectedLCD);
					
				}catch(Exception exc){

                    VRage.Utils.MyLog.Default.WriteLineAndConsole($"Failed Setting Emissives at Block State Change Event NonFunction{exc}");
                }
			}
			
			if(block.IsFunctional == true && block.IsWorking == true){
				
				try{

                    if (RadarCore.radarSettings == null) return;
					if(radar.Radius < RadarCore.radarSettings.modeSwitchRange){
					
						ChangeEmissive("Passive", radar, null, true);
					
					}else{
					
						ChangeEmissive("Idle", radar, null, true);
					}

                    beacon.TryGetSubpart("radar", out subpart);
                }
                catch(Exception exc){

                    VRage.Utils.MyLog.Default.WriteLineAndConsole($"Failed Setting Emissives at Block State Change Event Functional {exc}");
                }
			}
		}
		
		public static void ChangeEmissive(string mode, IMyBeacon myBeacon, RadarData item = null, bool forceUpdate = false){
			
			if(item != null){
				
				item.EmissiveMode = mode;
			}
			
			if(mode == "Active"){
				
				myBeacon.SetEmissiveParts(emissiveName, activeColor, 1f);
				myBeacon.SetEmissivePartsForSubparts(emissiveName, activeColor, 1f);
				myBeacon.SetEmissivePartsForSubparts("Emissive_White", activeColor, 1f);
                RadarCore.UpdateBeaconHudText(myBeacon, "ActiveScanning");

                if (item != null){
					
					item.ActiveRadar = true;
				}
			}
			
			if(mode == "Passive"){
				
				myBeacon.SetEmissiveParts(emissiveName, passiveColor, 1f);
				myBeacon.SetEmissivePartsForSubparts(emissiveName, passiveColor, 1f);
				myBeacon.SetEmissivePartsForSubparts("Emissive_White", passiveColor, 1f);
                RadarCore.UpdateBeaconHudText(myBeacon, "Passive");
                RadarCore.detailInfo = RadarCore.GetStringBuilderText("Passive");
                RadarCore.UpdateBlockDetails(myBeacon as IMyTerminalBlock);

                if (item != null){
					
					item.ActiveRadar = false;
				}
			}
			
			if(mode == "Idle"){
				
				myBeacon.SetEmissiveParts(emissiveName, idleColor, 1f);
				myBeacon.SetEmissivePartsForSubparts(emissiveName, idleColor, 1f);
				myBeacon.SetEmissivePartsForSubparts("Emissive_White", idleColor, 1f);
                RadarCore.UpdateBeaconHudText(myBeacon, "ActiveReady");
                RadarCore.detailInfo = RadarCore.GetStringBuilderText("ActiveReady");
                RadarCore.UpdateBlockDetails(myBeacon as IMyTerminalBlock);

                if (item != null){
					
					item.ActiveRadar = true;
				}
			}
			
			if(mode == "Inactive"){
				
				myBeacon.SetEmissiveParts(emissiveName, inactiveColor, 1f);
				myBeacon.SetEmissivePartsForSubparts(emissiveName, inactiveColor, 1f);
				myBeacon.SetEmissivePartsForSubparts("Emissive_White", inactiveColor, 1f);
                RadarCore.detailInfo = RadarCore.GetStringBuilderText("Offline");
                RadarCore.UpdateBlockDetails(myBeacon as IMyTerminalBlock);
            }
			
			if(forceUpdate){
				
				string message = "UpdateEmissive" + "\n";
				message += mode + "\n";
				message += myBeacon.EntityId.ToString() + "\n";

                if (!RadarCore.isServer)
                {
                    Comms.SendMessageToServer(message);
                }

                Comms.SendToOtherPlayers(message);
			}
		}
		
		public static RadarData LoopRadarSync(long entityId){
			
			try{
	
				if(m_lastUpdate.Count == 0)return null;
				foreach(var item in m_lastUpdate){
				
					if(item.EntityId == entityId){
					
						return item;
					}
				}
			
				return null;
				
			}catch(Exception exc){

                VRage.Utils.MyLog.Default.WriteLineAndConsole($"Failed to find radar settings in loop {exc}");
                return null;
			}
		}

        public static void SaveState(long entityId, RadarData item)
        {
            IMyEntity blockEntity = null;
            MyAPIGateway.Entities.TryGetEntityById(entityId, out blockEntity);
            if (blockEntity == null) return;

            if (blockEntity.Storage == null)
            {
                blockEntity.Storage = new MyModStorageComponent();
            }

            if (blockEntity.Storage != null)
            {
                var newByteData = MyAPIGateway.Utilities.SerializeToBinary<RadarData>(item);
                var base64string = Convert.ToBase64String(newByteData);
                blockEntity.Storage[cpmID] = base64string;
            }
        }

        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
		{
			return m_objectBuilder;
		}

		public override void Close()
		{
			Logging.Instance.WriteLine(string.Format("Close RadarLogic")); 
			
			if (Entity == null)
				return;

            m_lastUpdate.RemoveAll(x => x.EntityId == Entity.EntityId);
		}
		
		public override void OnRemovedFromScene(){

            try
            {
                base.OnRemovedFromScene();

                if (Entity == null || Entity.MarkedForClose) return;

                var Block = Entity as IMyBeacon;

                if (Block == null)
                {

                    return;

                }

                try
                {
                    if (CubeBlock == null || CubeBlock.MarkedForClose) return;

                    Block.AppendingCustomInfo -= RadarCore.AppendCustomInfo;
                    CubeBlock.IsWorkingChanged -= WorkingStateChange;
                }
                catch (Exception exc)
                {

                    VRage.Utils.MyLog.Default.WriteLineAndConsole($"Failed to deregister event: {exc}");
                    return;
                }

            }catch(Exception ex)
            {

            }
        }
	}
}
