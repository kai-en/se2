using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using VRage;
using VRageMath;

using Sandbox.Common;
using Sandbox.ModAPI;

using VRage.ModAPI;
using VRage.Serialization;
using VRage.Game.ModAPI;

namespace RadarBlock
{
	public class RadarProcess
	{
		private static List<IMyEntity> m_updateList;
		private static List<RadarObjects> m_radarObjectList;
		private static bool m_init = false;
		private static bool m_LCDClear = false;
		//private static IMyEntity m_LCDParent = null;
		//private static string m_LCDName = "";

		//private static List<IMyEntity> m_LCDParentList = new List<IMyEntity>();
		private static List<RadarOutputItem> m_radarOutputList = new List<RadarOutputItem>();
		internal static System.Timers.Timer clearTimer = new System.Timers.Timer();

		private static List<RadarSoundItem> m_radarSoundList = new List<RadarSoundItem>();

		/// <summary>
		/// Process Radar Blocks
		/// </summary>
		public static void Process()
		{
			if (MyAPIGateway.Session == null)
				return;

			if (MyAPIGateway.Utilities.IsDedicated && MyAPIGateway.Multiplayer.IsServer)
				return;

			if (RadarLogic.LastRadarUpdate == null)
				return;

			if (!m_init) {
				m_init = true;
				Initialize();
			}

			if (MyAPIGateway.Session.Player == null)
				return;

			/*
			// The player needs to be in control of something
			if (MyAPIGateway.Session.Player == null || MyAPIGateway.Session.Player.Controller == null || MyAPIGateway.Session.Player.Controller.ControlledEntity == null || MyAPIGateway.Session.Player.Controller.ControlledEntity.Entity == null)
				return;

			// The player needs to be in a ship at least
			if (MyAPIGateway.Session.Player.Controller.ControlledEntity.Entity.GetTopMostParent() is IMyCharacter)
				return;

			 */

			// Only really one ship will paint hud markers.  When found, stop processing.
			//bool found = false;

			// Check radar block once every 10 seconds.
			foreach (KeyValuePair<IMyEntity, MyTuple<IMyEntity, DateTime>> p in RadarLogic.LastRadarUpdate) {
				if (DateTime.Now - p.Value.Item2 > TimeSpan.FromSeconds(RadarBlock.RadarSettings.CheckRadarSeconds)) {
					//if (!found)
					//{
					//if (ProcessRadarItem(p.Value.Item1))
					//found = true;
					//}

					ProcessRadarItem(p.Value.Item1);
					m_updateList.Add(p.Key);
				}
			}

			// Update timer on radar block
			foreach (IMyEntity updatedItem in m_updateList) {
				if (RadarLogic.LastRadarUpdate.ContainsKey(updatedItem))
					RadarLogic.LastRadarUpdate[updatedItem] = new MyTuple<IMyEntity, DateTime>(updatedItem, DateTime.Now);
			}

			m_updateList.Clear();
		}

		/*
		 * 100%-75% - Can detect massive objects
		 * 74%-55% - Can detect huge objects
		 * 54%-35% - Can detect large objects
		 * 34%-20% - Can detect moderate objects
		 * 19%-10% - Can detect small objects
		 * 0-9% - Can detect tiny objects
		 * Why do I care about min distance now that I think of it?  hmm.
		 */
		private static void Initialize()
		{
			m_updateList = new List<IMyEntity>();

			m_radarObjectList = new List<RadarObjects>();
			/*
			AddScanItem("Massive", 0.75f, 1f, 500f);
			AddScanItem("Huge", 0.55f, 0.75f, 250f);
			AddScanItem("Large", 0.35f, 0.55f, 100f);
			AddScanItem("Medium", 0.20f, 0.35f, 50f);
			AddScanItem("Small", 0.10f, 0.20f, 25f);
			AddScanItem("Tiny", 0.01f, 0.10f, 0f);
			*/
			
			AddScanItem("Massive", 1f, 1f, 500f);
			AddScanItem("Huge", 1f, 1f, 250f);
			AddScanItem("Large", 1f, 1f, 100f);
			AddScanItem("Medium", 1f, 1f, 50f);
			AddScanItem("Small", 1f, 1f, 25f);
			AddScanItem("Tiny", 1f, 1f, 0f);

		}

		private static void AddScanItem(string name, float min, float max, float size)
		{
			RadarObjects p = new RadarObjects();
			p.SizeName = name;
			p.MinimumDistance = min;
			p.MaximumDistance = max;
			p.Size = size;
			m_radarObjectList.Add(p);
		}

		/// <summary>
		/// Process a radar block on a grid.  If it's the grid the player is flying, draw hud markers of entities the radar can spot
		/// </summary>
		/// <param name="entity"></param>
		/// <returns></returns>
		private static bool ProcessRadarItem(IMyEntity entity)
		{
			// Sanity check
			if (!(entity is IMyBeacon))
				return false;

			IMyEntity parent = entity.GetTopMostParent();
			m_radarSoundList.RemoveAll(x => x.EntityId == parent.EntityId);

			// Player needs to be controlling the grid this radar is on
			//if (parent != MyAPIGateway.Session.Player.Controller.ControlledEntity.Entity.GetTopMostParent())
			//	return false;

			// This radar needs to be owned by the local player
			IMyBeacon beacon = (IMyBeacon)entity;
			if (beacon.OwnerId != MyAPIGateway.Session.Player.PlayerID)
				return false;

			// Needs to be on and working
			if (!beacon.IsWorking || !beacon.IsFunctional || !beacon.Enabled)
				return false;

			Vector3D position = parent.GetPosition();
			double radius = (double)beacon.Radius;
			BoundingSphereD sphere = new BoundingSphereD(position, radius);
			List<IMyEntity> entities = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere);
			RadarFilter radarFilter = GetRadarEntityFilters(entity);
			String lcdoutput = "";
			
			foreach (IMyEntity foundEntity in entities) {
				// Projection or invalid object
				if (foundEntity.Physics == null)
					continue;

				// Waypoints and other things that are free of physics
				if (!foundEntity.Physics.Enabled)
					continue;

				// Ignore our own ship
				if (MyAPIGateway.Session.Player.Controller != null && MyAPIGateway.Session.Player.Controller.ControlledEntity != null && MyAPIGateway.Session.Player.Controller.ControlledEntity.Entity != null && foundEntity == MyAPIGateway.Session.Player.Controller.ControlledEntity.Entity.GetTopMostParent())
					continue;

				// Ignore our character entity
				if (foundEntity.DisplayName == MyAPIGateway.Session.Player.DisplayName)
					continue;

				if (foundEntity is IMyVoxelMap ||
				    foundEntity is IMyCharacter ||
				    foundEntity is IMyCubeGrid) {
					double distance = Vector3D.DistanceSquared(foundEntity.GetPosition(), position);
					distance = Math.Sqrt(distance);
					if (distance < radius) {
						lcdoutput = lcdoutput + AddRadarEntity(foundEntity, parent, radarFilter, distance, radius);
					}
				}
			}
			
			// lcdoutput can now be sorted, it's basically a CSV.
			
			if (radarFilter.OutputLCDName != null && radarFilter.OutputLCDName != "")
			{
				
				//Logging.Instance.WriteLine(lcdoutput);

				LCDManager.Add(parent, radarFilter.OutputLCDName, lcdoutput, false);//m_LCDClear);
				RadarOutputItem outputItem = new RadarOutputItem();
				outputItem.Parent = parent;
				outputItem.Name = radarFilter.OutputLCDName;
				m_radarOutputList.Add(outputItem);
				/*
				// Clear LCD timer
				if (!m_LCDClear) {
					m_LCDClear = true;

					//m_LCDParent = parent;
					//m_LCDName = radarFilter.OutputLCDName;

					//System.Timers.Timer t = new System.Timers.Timer(9000);
					
					clearTimer = new System.Timers.Timer(RadarBlock.RadarSettings.UpdateLCDSeconds * 1000);
					clearTimer.Elapsed += ClearLCDTimer;
					clearTimer.AutoReset = false;
					clearTimer.Start();
				}
				
				 */
			}
			

			return true;
		}

		/*
		 *  RULES:
		 *  Objects > 66% of radar distance unknown object
		 *  Objects > 33% of radar distance size known (AABB?)
		 *  Objects < 33% of radar distance type known
		 */
		/// <summary>
		/// Add hud markers on a found entity
		/// </summary>
		/// <param name="entity"></param>
		/// <param name="distanceSquared"></param>
		/// <param name="radius"></param>
		private static String AddRadarEntity(IMyEntity entity, IMyEntity parent, RadarFilter radarFilter, double distance, double radius)
		{
			string output = "";

			try {
				bool showHud = true;
				
				bool powered = true; // start as true because we want to see asteroids if not specified otherise
				bool sonarmode = false; 
				bool functional = false;
				bool broadcasting = false;
				
				if (radarFilter.SonarMode)
					sonarmode=true; // TODO: increase detection range past transmission range, but only return noisy blocks in an atmosphere.
				
				

				// Player needs to be controlling something to see hud marks
				if (MyAPIGateway.Session.Player == null || MyAPIGateway.Session.Player.Controller == null || MyAPIGateway.Session.Player.Controller.ControlledEntity == null || MyAPIGateway.Session.Player.Controller.ControlledEntity.Entity == null)
					showHud = false;

				// Player needs to be controlling the ship that the radar is on to see hud marks (unless specified)
				if ((radarFilter.PassengerHud==false) && (parent != MyAPIGateway.Session.Player.Controller.ControlledEntity.Entity.GetTopMostParent()))
					showHud = false;
				
				RadarObjectTypes radarType = GetEntityObjectType(entity);
				RadarObjects radarItem = GetEntityObjectSize(entity);
				
				float maxdist = radarItem.MaximumDistance;
				if (sonarmode) // TODO: this will not work yet
					maxdist=maxdist * 100.0f;


				// Radar can't detect item of this size at this range
				if (distance > radius * maxdist)
					return "";

				if (radarFilter.MinimumDistance > 0f) {
					// The item is too close
					if (distance < radarFilter.MinimumDistance) {
						return "";
					}
				}
				
				// Do not display asteroids
				if (radarType == RadarObjectTypes.Asteroid && radarFilter.NoAsteroids)
					return "";

				// Do not display ships
				if (radarType == RadarObjectTypes.Ship && radarFilter.NoShips)
					return "";

				// Do not display stations
				if (radarType == RadarObjectTypes.Station && radarFilter.NoStations)
					return "";

				// Do not display players
				if (radarType == RadarObjectTypes.Astronaut && radarFilter.NoCharacters)
					return "";
				
				double objectsize = (entity.PositionComp.LocalAABB.Max - entity.PositionComp.LocalAABB.Min).Volume;
				
				// Do not display debris (but do display lifeforms regardless of size)
				if ((radarType != RadarObjectTypes.Astronaut) && (objectsize < Math.Abs(radarFilter.MinimumSize + 0.01)))
					return "";

				string description = "Unknown";
				


				broadcasting=false;
				if (radarFilter.OnlyPowered) // only powered also gives you extra info if the thing is broadcasting
				{
					if (radarType == RadarObjectTypes.Ship || radarType == RadarObjectTypes.Station)
				{
					IMyCubeGrid Grid = entity as IMyCubeGrid;
					List<IMyTerminalBlock> Blocks = new List<IMyTerminalBlock>();
					MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(Grid).GetBlocksOfType(Blocks, x => x.IsFunctional);
					broadcasting = Blocks.Any(x => (x as IMyBeacon)?.IsWorking == true || (x as IMyRadioAntenna)?.IsWorking == true);
					powered = Blocks.Any(x => (x as IMyReactor)?.IsWorking == true || (x as IMyBatteryBlock)?.CurrentStoredPower > 0f);
				}
				if (powered==false)
					return "";
				}
				
				if (broadcasting)
					description=entity.DisplayName;
				else if (distance < radius * maxdist * 0.33)
				{
					if (radarType == RadarObjectTypes.Astronaut)
					{
						if (entity.DisplayName.Length > 1)
							description = "Humanoid";
						else
							description = "Lifeform";
					}
					else
					{
						description = string.Format("{0} {1} {2}", radarItem.SizeName, radarType.ToString(), Math.Abs(entity.EntityId % 10000));
					}
				} else if (distance < radius * maxdist * 0.66) {
					description = string.Format("{0} unknown {1}", radarItem.SizeName, Math.Abs(entity.EntityId % 10000));
					
				}
				else if (radarType == RadarObjectTypes.Asteroid && objectsize > 2022 && objectsize < 4098) // hardcoded boulder size. Do not show these, on planets it's spammy and in space it just brings up subvoxels
					description = "Boulder";

				Vector3D position = entity.GetPosition();
				
				
				// No hud marks
				if (!radarFilter.NoHud && showHud)
					HudMarkManager.Add(position, description);

				// output to LCD
				if (!string.IsNullOrEmpty(radarFilter.OutputLCDName)) {

					output = string.Format("GPS:{0}:{1}:{2}:{3}:Size {4}:Dist {5}:\n", description, Math.Round(position.X), Math.Round(position.Y), Math.Round(position.Z), description.StartsWith("Unk") ? (-1) : (Math.Round(objectsize)), Math.Round(distance));//entity.PositionComp.LocalAABB.Max - entity.PositionComp.LocalAABB.Min);
//					LCDManager.Add(parent, radarFilter.OutputLCDName, output, m_LCDClear);
					/*
					RadarOutputItem outputItem = new RadarOutputItem();
					outputItem.Parent = parent;
					outputItem.Name = radarFilter.OutputLCDName;
					m_radarOutputList.Add(outputItem);

					// Clear LCD timer
					if (!m_LCDClear) {
						m_LCDClear = true;

						//m_LCDParent = parent;
						//m_LCDName = radarFilter.OutputLCDName;

						//System.Timers.Timer t = new System.Timers.Timer(9000);
						
						clearTimer = new System.Timers.Timer(RadarBlock.RadarSettings.UpdateLCDSeconds * 1000);
						clearTimer.Elapsed += ClearLCDTimer;
						clearTimer.AutoReset = false;
						clearTimer.Start();
					}
					 */
				}

				if (radarFilter.TriggerSoundName != null && radarFilter.TriggerSoundName != "") {
					if (m_radarSoundList.Where(x => x.EntityId == parent.EntityId).Count() < 1) {
						m_radarSoundList.Add(new RadarSoundItem() { EntityId = parent.EntityId });
						SoundBlockManager.PlaySound(parent, radarFilter.TriggerSoundName);
					}
				}
			} catch (Exception ex) {
				Logging.Instance.WriteLine(string.Format("AddRadarEntity Error: {0}", ex.ToString()));
				return string.Format("AddRadarEntity Error: {0}", ex.ToString());
			}
			return output;
		}

		/// <summary>
		/// Clear the LCD screen we are outputting to
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		static void ClearLCDTimer(object sender, System.Timers.ElapsedEventArgs e)
		{
			if (MyAPIGateway.Session == null)
				return;

			MyAPIGateway.Utilities.InvokeOnGameThread(new Action(() => {
			                                                     	m_LCDClear = false;
			                                                     	foreach (var item in m_radarOutputList) {
			                                                     		LCDManager.Clear(item.Parent, item.Name);
			                                                     		//LCDManager.Clear(m_LCDParent, m_LCDName);
			                                                     	}
			                                                     	m_radarOutputList.Clear();
			                                                     }));
		}

		/// <summary>
		/// Extract any text filters the player has put on the radar
		/// </summary>
		/// <param name="radarEntity"></param>
		/// <returns></returns>
		private static RadarFilter GetRadarEntityFilters(IMyEntity radarEntity)
		{
			RadarFilter filter = new RadarFilter();
			IMyBeacon radarBeacon = (IMyBeacon)radarEntity;
			String customdata = radarBeacon.CustomData;
			if (customdata.Length<2) // empty string, so let's get some defaults in there first
			{
				radarBeacon.CustomData="MinimumDistance:1\nMinimumSize:2\nYesShips\nYesStations\nYesHud\nYesUnpowered\nYesLifeforms\n\nOutputLCD:RadarLCD\n";
			}
			
			customdata = customdata.ToLowerInvariant();
			customdata.Replace("minsize:","minimumsize:");
			customdata.Replace("mindist:","minimumdistance:");
			customdata.Replace("mindistance:","minimumdistance:");
			customdata.Replace("output:","outputlcd:");

			if (customdata.Contains("noasteroids")||customdata.Contains("noroids"))
				filter.NoAsteroids = true;
			if (customdata.Contains("noships"))
				filter.NoShips = true;
			if (customdata.Contains("nostations"))
				filter.NoStations = true;
			if (customdata.Contains("nocharacters")||customdata.Contains("nolifeforms"))
				filter.NoCharacters = true;
			if (customdata.Contains("nohud"))
				filter.NoHud = true;
			if (customdata.Contains("passengerhud"))
				filter.PassengerHud = true;
			if (customdata.Contains("onlypowered")||customdata.Contains("nounpowered"))
				filter.OnlyPowered = true;
			if (customdata.Contains("sonarmode"))
				filter.SonarMode = true;

			// I should be using Regex, but don't really have time to profile.  I don't want to be stuck with a weird
			// processing issue as sometimes regex can be fickle
			if (customdata.Contains("minimumsize:")) {
				int pos = customdata.IndexOf("minimumsize:") + 12;
				string minString = customdata.Substring(pos, customdata.Length - pos);
				string corrected = "";
				for (int r = 0; r < minString.Length; r++) {
					int test = 0;
					string numTest = "";
					numTest += minString[r];
					if (!int.TryParse(numTest, out test))
						break;

					corrected += minString[r];
				}

				float minimumSize = 2f;
				filter.MinimumSize = float.TryParse(corrected, out minimumSize) ? minimumSize : 2f; // by default skip size 0~1 objects or we'll get all sorts of spam
			}
			else
				filter.MinimumSize = 2f; // by default skip size 0~1 objects or we'll get all sorts of spam
			if (customdata.Contains("minimumdistance:")) {
				int pos = customdata.IndexOf("minimumdistance:") + 16;
				string minString = customdata.Substring(pos, customdata.Length - pos);
				string corrected = "";
				for (int r = 0; r < minString.Length; r++) {
					int test = 0;
					string numTest = "";
					numTest += minString[r];
					if (!int.TryParse(numTest, out test))
						break;

					corrected += minString[r];
				}

				float minimumDistance = 1f; // default minimum distance to 1, so that we don't scan our own grid. But leave that as an option in case we need to see size.
				filter.MinimumDistance = float.TryParse(corrected, out minimumDistance) ? minimumDistance : 1f;
			}
			if (customdata.Contains("outputlcd:")) {
				int pos = customdata.IndexOf("outputlcd:") + 10;
				string minString = customdata.Substring(pos, customdata.Length - pos);
				string corrected = "";
				for (int r = 0; r < minString.Length; r++) {
					if (minString[r] == ',' || minString[r] == ':' || minString[r] == ')' || minString[r] == ']' || minString[r] == '\n')
						break;

					corrected += minString[r];
				}

				filter.OutputLCDName = corrected;
				
			}

			if (customdata.Contains("triggersoundname:")) {
				int pos = customdata.IndexOf("triggersoundname:") + 17;
				string minString = customdata.Substring(pos, customdata.Length - pos);
				string corrected = "";
				for (int r = 0; r < minString.Length; r++) {
					if (minString[r] == ',' || minString[r] == ':' || minString[r] == ')' || minString[r] == ']' || minString[r] == '\n')
						break;

					corrected += minString[r];
				}

				filter.TriggerSoundName = corrected;
				Logging.Instance.WriteLine(string.Format("Sound Name: {0}", filter.TriggerSoundName));
			}

			return filter;
		}

		
		/// <summary>
		/// Get the entity type
		/// </summary>
		/// <param name="entity"></param>
		/// <returns></returns>
		private static RadarObjectTypes GetEntityObjectType(IMyEntity entity)
		{
			if (entity is IMyVoxelMap)
				return RadarObjectTypes.Asteroid;

			if (entity is IMyCharacter) {
				return RadarObjectTypes.Astronaut;
			}

			if (entity is IMyCubeGrid && ((IMyCubeGrid)entity).IsStatic)
				return RadarObjectTypes.Station;

			return RadarObjectTypes.Ship;
		}

		/// <summary>
		/// Get the entity size
		/// </summary>
		/// <param name="entity"></param>
		/// <returns></returns>
		private static RadarObjects GetEntityObjectSize(IMyEntity entity)
		{

			double entitySize = entity.PositionComp.WorldAABB.Size.AbsMax();

			foreach (RadarObjects item in m_radarObjectList) {
				if (entitySize >= item.Size) {
					return item;
				}
			}

			return null;
		}
	}

	public class RadarObjects
	{
		public float MinimumDistance { get; set; }
		public float MaximumDistance { get; set; }
		public string SizeName { get; set; }
		public float Size { get; set; }
	}

	public class RadarFilter
	{
		public bool NoAsteroids { get; set; }
		public bool NoShips { get; set; }
		public bool NoStations { get; set; }
		public bool NoCharacters { get; set; }
		public bool NoHud { get; set; }
		public bool PassengerHud { get; set; }
		public bool OnlyPowered { get; set; }
		public bool SonarMode { get; set; }
		public float MinimumSize { get; set; }
		public float MinimumDistance { get; set; }
		public string OutputLCDName { get; set; }
		public string TriggerSoundName { get; set; }
	}

	public enum RadarObjectTypes
	{
		Astronaut,
		Asteroid,
		Station,
		Ship
	}

	public class RadarOutputItem
	{
		public IMyEntity Parent { get; set; }
		public string Name { get; set; }
	}

	public class RadarSoundItem
	{
		public long EntityId { get; set; }
	}

}
