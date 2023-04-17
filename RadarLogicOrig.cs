using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

//using Sandbox.Common.Components;
//using Sandbox.Common.ObjectBuilders;
//using Sandbox.ModAPI;
//using VRage.ModAPI;
//using VRage.ObjectBuilders;
//using VRage.ObjectBuilders.Definitions;
//using VRage.Components;
//using VRage;

using Sandbox.ModAPI;
using Sandbox.Common.ObjectBuilders;

using VRage;
using VRage.ModAPI;
using VRage.Game.Components;
using VRage.ObjectBuilders;

namespace RadarBlock
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Beacon), true, "LargeBlockRadar", "SmallBlockRadar")]
    //[MyEntityComponentDescriptor(typeof(MyObjectBuilder_Beacon), "LargeBlockRadar", "SmallBlockRadar")]
    //[MyEntityComponentDescriptor(typeof(MyObjectBuilder_Beacon))]
    public class RadarLogic : MyGameLogicComponent
	{
		// Contains the list of radar blocks in the world
		public static Dictionary<IMyEntity, MyTuple<IMyEntity, DateTime>> LastRadarUpdate
		{
			get
			{
				return m_lastUpdate;
			}
		}

		private MyObjectBuilder_EntityBase m_objectBuilder = null;
		private static Dictionary<IMyEntity, MyTuple<IMyEntity, DateTime>> m_lastUpdate = null;
		
		/// <summary>
		/// So, uhm.  Init on a beacon passes a null objectBuilder?  This can't be right.
		/// </summary>
		/// <param name="objectBuilder"></param>
		public override void Init(MyObjectBuilder_EntityBase objectBuilder)
		{
            if (m_lastUpdate == null)
				m_lastUpdate = new Dictionary<IMyEntity, MyTuple<IMyEntity, DateTime>>();

			IMyBeacon beacon = (IMyBeacon)Entity;
            Entity.NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;

            // Since our object builder is null, use Entity.
            if (beacon == null)
			{
				//Logging.Instance.WriteLine("Entity is null");
			}
			else if (beacon.BlockDefinition.SubtypeName.Contains("Radar"))
			{
                //Logging.Instance.WriteLine("Here4");
                if (!m_lastUpdate.ContainsKey(Entity))
				{
					m_lastUpdate.Add(Entity, new MyTuple<IMyEntity, DateTime>(Entity, DateTime.Now));
                    //Logging.Instance.WriteLine("Here5");
                }
                //Logging.Instance.WriteLine("Here6");
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

			if (m_lastUpdate.ContainsKey(Entity))
				m_lastUpdate.Remove(Entity);			
		}
	}
}
