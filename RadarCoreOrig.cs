using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Sandbox.Common;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game;

namespace RadarBlock
{
	
	
	public struct RadarSettings
	{
		public static int CheckRadarSeconds = 3; // update radars how often (in seconds)
		public static int ClearHUDListEverySeconds = 2; // hud list cleanup
		
		public static int UpdateRadarSeconds = 1; // obsolete, I think
		public static int UpdateLCDSeconds = 9; // obsolete, I think
	}

	[MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
	public class RadarCore : MySessionComponentBase
    {

		private static DateTime m_lastUpdate;
		private bool m_initialized;



		private void Initialize()
		{
			m_initialized = true;

			if (MyAPIGateway.Utilities.IsDedicated && MyAPIGateway.Multiplayer.IsServer)
				return;

			Logging.Instance.WriteLine("Radar Block Initialized");

			m_lastUpdate = DateTime.Now;
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
				Initialize();

			// Check our radar blocks once every second
			if (DateTime.Now - m_lastUpdate > TimeSpan.FromSeconds(RadarSettings.UpdateRadarSeconds))
			{

				// Process Radar Blocks
				RadarProcess.Process();

				// Process Hud Markers
				HudMarkManager.Process();

				m_lastUpdate = DateTime.Now;
			}

			base.UpdateBeforeSimulation();
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
			}
			catch { }

            RadarProcess.clearTimer.Stop();
            //if (RadarProcess.clearTimer != null && RadarProcess.clearTimer.Enabled)
            //    RadarProcess.clearTimer.Stop();

			base.UnloadData();
		}
    }
}
