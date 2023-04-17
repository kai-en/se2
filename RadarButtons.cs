using System.Collections.Generic;
using Sandbox.ModAPI;
using Sandbox.Game;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game.ModAPI;
using VRageMath;

namespace RadarBlock{	

	public static class RadarButtons{
		
		public static bool controlsCreated = false;
		
		public static void CreateControlsNew(IMyTerminalBlock block, List<IMyTerminalControl> controls){
			
			if(block as IMyBeacon != null){
				
				ButtonCreations.CreateControls(block, controls);
			}
		}
		
		public static void CreateActionsNew(IMyTerminalBlock block, List<IMyTerminalAction> controls){
			
			if(block as IMyBeacon != null){
				
				ButtonCreations.CreateActions(block, controls);
			}
		}
		
		public static bool ControlVisibility(IMyTerminalBlock block){
			
			if(block as IMyBeacon != null){
				
				var radar = block as IMyBeacon;
				if(radar.BlockDefinition.SubtypeName.Contains("Radar")){
				
					return true;
				}
				
			}
			
			return false;
		}

        public static bool HideControls(IMyTerminalBlock block)
        {
            if (block as IMyBeacon != null)
            {
                var radar = block as IMyBeacon;
                if (radar.BlockDefinition.SubtypeName.Contains("Radar"))
                {

                    return false;
                }
            }

            return true;
        }

        public static bool CheckEnabled(IMyTerminalBlock block)
        {
            RadarData data = RadarCore.LoopActiveRadars(block.EntityId);
            if (data != null) return false;
            var radar = block as IMyBeacon;
            if (radar == null) return false;
            if (!radar.BlockDefinition.SubtypeName.Contains("Radar")) return false;
            if (!radar.Enabled) return false;
            if(RadarCore.radarSettings == null)
            {
                var clientId = MyAPIGateway.Session.LocalHumanPlayer.IdentityId;
                Comms.SendMessageToServer("SyncConfig", clientId);
                return false;
            }
            if (radar.Radius <= RadarCore.radarSettings.modeSwitchRange) return false;

            var cubeBlock = block as IMyCubeBlock;
            if (cubeBlock == null) return false;
            var cubeGrid = cubeBlock.CubeGrid;
            if (cubeGrid == null) return false;

            if (RadarCore.config.RequireStationaryWhileActiveScanning)
            {
                var vel = (Vector3D)cubeGrid.Physics.LinearVelocity;
                var speed = vel.Length();
                if (speed > 10)
                {
                    return false;
                }
            }

            return true;
        }
	}
}