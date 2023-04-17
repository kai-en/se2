using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Sandbox.Common;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;

using VRageMath;
using VRage;
using VRage.Game.ModAPI;

namespace RadarBlock
{
	public class HudMarkManager
	{
		private static List<MyTuple<IMyGps, DateTime>> m_hudMarkList = new List<MyTuple<IMyGps, DateTime>>();
		private static DateTime m_lastUpdate = DateTime.Now;
        private static bool m_initialized = false;

		/// <summary>
		/// Add a hud mark to the player's screen.  Using GPS here causes issue (sometimes they save??).  I need
		/// to add unsaved hudmarkers to the game's source.  Would allow for colours too if I do that.
		/// </summary>
		/// <param name="position"></param>
		/// <param name="description"></param>
		public static void Add(Vector3D position, string description)
		{
			// Does name need to be unique?  Let's assume yes.  ED: no name is what shows up on hud
			IMyGps gps = MyAPIGateway.Session.GPS.Create(description, "BLIP: " + description, position, true, true);
			m_hudMarkList.Add(new MyTuple<IMyGps, DateTime>(gps, DateTime.Now));
			MyAPIGateway.Session.GPS.AddLocalGps(gps);
		}

		/// <summary>
		/// Process our hud markers.  Remove them after a certian time. (5 seconds right now)
		/// </summary>
		public static void Process()
		{
            if (MyAPIGateway.Session == null)
                return;

            if (!m_initialized && !(MyAPIGateway.Utilities.IsDedicated && MyAPIGateway.Multiplayer.IsServer) && MyAPIGateway.Session.Player != null)
            {
                m_initialized = true;
                RemoveSavedGPS();               
            }

			for (int r = m_hudMarkList.Count - 1; r > -1; r--)
			{
				MyTuple<IMyGps, DateTime> items = m_hudMarkList[r];
				if (DateTime.Now - items.Item2 > TimeSpan.FromSeconds(RadarBlock.RadarSettings.ClearHUDListEverySeconds))
				{
					MyAPIGateway.Session.GPS.RemoveLocalGps(items.Item1);
					m_hudMarkList.RemoveAt(r);
				}
			}
        }

        private static void RemoveSavedGPS()
        {
            if (MyAPIGateway.Session != null)
                return;

            if (MyAPIGateway.Utilities.IsDedicated && MyAPIGateway.Multiplayer.IsServer)
                return;

            try
            {
                List<IMyGps> list = new List<IMyGps>();
                MyAPIGateway.Session.GPS.GetGpsList(MyAPIGateway.Session.Player.PlayerID, list);

                foreach (var item in list)
                {
                    if (item.Description.StartsWith("BLIP:"))
                    {
                        MyAPIGateway.Session.GPS.RemoveGps(MyAPIGateway.Session.Player.PlayerID, item);
                    }
                }
            }
            catch(Exception ex)
            {
                Logging.Instance.WriteLine(string.Format("RemoveSavedGPS(): {0}", ex.ToString()));
            }
        }
    }
}
