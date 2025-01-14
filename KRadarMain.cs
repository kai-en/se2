using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using Sandbox.Common;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game;
using VRage.ObjectBuilders;
using VRage.ModAPI;
using VRageMath;
using VRage.Game.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using Sandbox.ModAPI.Interfaces;
using System.Diagnostics;

namespace KRadarNamespace
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class KRadarMain : MySessionComponentBase
    {
        public static TextWriter m_logWriter = null;
        public static TextWriter logWriter {
            get
            {
                if (m_logWriter != null) return m_logWriter;
                try
                {
                    if (MyAPIGateway.Utilities != null)
                        m_logWriter = MyAPIGateway.Utilities.WriteFileInLocalStorage("KRadar.log", typeof(KRadarMain));

                    return m_logWriter;
                }
                catch {
                    return null;
                }

            }
        }

        public static void log(String info)
        {
            if (KRadarMain.logWriter != null)
            {
                KRadarMain.logWriter.WriteLine(DateTime.Now.ToString("[HH:mm:ss] ") + info);
            }
        }

        public static Guid LOCK_OFFSET_LIST_KEY = Guid.NewGuid();
        public static long frameCount = 0;
        public static bool actionAdded = false;

        public static void createActionDelegate(IMyTerminalBlock block, List<IMyTerminalAction> actions)
        {
            if (actionAdded) return;
            // not a KRadar return
            if (!(block is IMyBeacon)) return;
            //log($"create action start {block.BlockDefinition.SubtypeId} {block.BlockDefinition.SubtypeName}");
            if ((!"LargeBlockKRadar".Equals(block.BlockDefinition.SubtypeId)) && (!"SmallBlockKRadar".Equals(block.BlockDefinition.SubtypeId))) return;

            //Action - Lock
            var lockAction = MyAPIGateway.TerminalControls.CreateAction<IMyBeacon>("LockAction");
            lockAction.Enabled = b => b.IsFunctional; 
            lockAction.ValidForGroups = false;
            lockAction.Action = delegate(IMyTerminalBlock b)
            {
                if (!(b is IMyBeacon))
                {
                    return;
                }
                //int logIdx = 1;
                //log($"T{logIdx++}");
                string offsetListString;
                List<List<string>> offsetList;
                parseOffsetList(b, out offsetListString, out offsetList);
                //log($"T{logIdx++}");
                // FEATURE 0220
                // get name of camera
                IMyTerminalBlock camera;
                bool haveCamera = findCamera(b, out camera);
                if (!haveCamera) return;

                long entityId = 0;
                Vector3D offset = Vector3D.Zero;

                // raycast
                IHitInfo hitInfo;
                bool raycastSuccess = MyAPIGateway.Physics.CastRay(camera.GetPosition(), camera.GetPosition() + camera.WorldMatrix.Forward * 20000, out hitInfo);
                if (!raycastSuccess) return;
                if (!(hitInfo.HitEntity is IMyCubeGrid)) return;
                //log($"T{logIdx++}");
                IMyCubeGrid hitEntity = (IMyCubeGrid)hitInfo.HitEntity;
                var position = hitInfo.Position;
                entityId = hitEntity.EntityId;
                var lookAtMatrix = MatrixD.CreateLookAt(new Vector3D(), hitEntity.WorldMatrix.Forward, hitEntity.WorldMatrix.Up);
                offset = Vector3D.TransformNormal(position - hitEntity.GetPosition(), lookAtMatrix);

                List<string> currentLine;
                if (offsetList.Any(l => (entityId + "").Equals(l[0])))
                {
                    currentLine = offsetList.First(l => (entityId + "").Equals(l[0]));
                }
                else
                {
                    currentLine = new List<string>();
                    offsetList.Add(currentLine);
                    for (int i = 0; i < 4; i++)
                    {
                        currentLine.Add("");
                    }
                    currentLine[0] = entityId + "";
                }
                currentLine[1] = offset.X + "";
                currentLine[2] = offset.Y + "";
                currentLine[3] = offset.Z + "";
                List<string> outputLines = offsetList.Select(l => String.Join(":", l)).ToList();
                offsetListString = String.Join("\n", outputLines);

                b.Storage.Add(LOCK_OFFSET_LIST_KEY, offsetListString);
                //log($"raycast success {offsetListString}");
            };
            lockAction.Name = new StringBuilder("Lock");
            MyAPIGateway.TerminalControls.AddAction<IMyBeacon>(lockAction);
            actions.Add(lockAction);
            actionAdded = true;
        }

        private static bool findCamera(IMyTerminalBlock kradar, out IMyTerminalBlock camera)
        {
            string[] lines = kradar.CustomData.Split('\n');
            if (!lines.Any(l => l.Contains("nameCamera=")))
            {
                camera = null;
                return false;
            }
            //log($"T{logIdx++}");
            string nameCamera = lines.First(l => l.Contains("nameCamera=")).Split('=')[1];
            if (nameCamera == null || nameCamera == "")
            {
                camera = null;
                return false;
            }
            //log($"T{logIdx++}");
            var beacon = (IMyBeacon)kradar;
            var grid = (IMyCubeGrid)beacon.GetTopMostParent();
            var terminal = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(grid);
            var cameras = new List<IMyTerminalBlock>();
            terminal.GetBlocksOfType<IMyTerminalBlock>(cameras, c => c.CustomName.Contains(nameCamera));
            if (cameras.Count == 0)
            {
                camera = null;
                return false;
            }
            //log($"T{logIdx++}");
            camera = cameras[0];
            return true;
        }

        private static void parseOffsetList(IMyTerminalBlock b, out string offsetListString, out List<List<string>> offsetList)
        {
            bool haveOffsetList = b.Storage.TryGetValue(LOCK_OFFSET_LIST_KEY, out offsetListString);
            if (!haveOffsetList)
            {
                offsetListString = "";
            }

            offsetList = offsetListString.Split('\n').Select(delegate (string line)
            {
                if (line == null || line == "") return null;
                string[] fields = line.Split(':');
                if (fields.Length < 4) return null;
                return fields.ToList();
            }).Where(l => l != null).ToList();
        }

        private static string displayVector3D(Vector3D v)
        {
            return Math.Round(v.X, 2) + ", " + Math.Round(v.Y, 2) + ", " + Math.Round(v.Z, 2);
        }

        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
            base.Init(sessionComponent);

            MyAPIGateway.TerminalControls.CustomActionGetter += createActionDelegate;

            KRadarMain.log("KRadar initialized.");
        }

        public override void UpdateBeforeSimulation()
        {
            base.UpdateBeforeSimulation();
            frameCount++;
            lock (KRadarComponent.allKRadar)
            {
                int c = KRadarComponent.allKRadar.Count;
                for (int i = 0; i < c; i++)
                {
                    if (i < KRadarComponent.allKRadar.Count)
                    {
                        try
                        {
                            KRadarComponent.allKRadar[i].UpdateBeforeSimulation();
                        }
                        catch { }
                    }
                }
            }
        }

        protected override void UnloadData()
        {
            base.UnloadData();
            if (m_logWriter != null)
            {
                m_logWriter.Flush();
                m_logWriter.Close();
                m_logWriter = null;
            }
            MyAPIGateway.TerminalControls.CustomActionGetter -= createActionDelegate;
        }
    }

    public class RadarEntityExtend
    {
        public int type = 0; // 0 - asteroid 1 - grid
        public double signalRadius = 0;
        public double distance = 0;
        public Vector3D dir;
        public bool forceCover;

        public IMyEntity entity;
        public Vector3D center;
        public RadarEntityExtend(IMyEntity entity)
        {
            this.entity = entity;
        }
    }

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Beacon), true, "LargeBlockKRadar", "SmallBlockKRadar")]
    public class KRadarComponent : MyGameLogicComponent
    {
        private bool isEW = false;
        private string nameLCD = "";
        private List<IMyTextSurface> LCDs = new List<IMyTextSurface>();
        private IMyShipController beaconController = null;
        private readonly double EWRangeMax = 30000D;
        private readonly double BeaconRangeMax = 50000D;
        private readonly int EWDefaultCount = 5; // EW mode default max track (180 frame interval)
        private readonly double FCDefaultCount = 1; // Fire Control mode default max track (15 frame interval)
        private readonly double DirResoRadius = 0.1; // Radar Resolution min radius(based on normalized direction)
        private readonly int UPDATE_INTERVAL = 15;
        private readonly int UPDATE_INTERVAL_EW = 180;
        private long frameCount = 0;
        private int maxTrace = 1;
        private int updateFrame = 0;
        private string debugInfo = "";

        public static List<KRadarComponent> allKRadar = new List<KRadarComponent>();
        private string nameCamera="";
        private IEnumerable<IMyEntity> cAvaliableGridEntities;
        private Dictionary<long, Vector3D> offsetDic;
        private long lastUpdateFrame;
        private double SMALL_KRADAR_ANGLE_LIMIT = 0.9;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            KRadarNamespace.KRadarMain.log("one new KRadar");
            
            Entity.NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;

            this.updateFrame = new Random().Next(15);
            lock (allKRadar)
            {
                allKRadar.Add(this);
            }
        }

        public override void Close()
        {   
            try
            {
                base.Close();
                if (this == null) return;
                if (Entity != null && Entity is IMyTerminalBlock)
                {
                    ((IMyTerminalBlock)Entity).Storage.Clear();
                }
                if (allKRadar == null) return;
                lock (allKRadar)
                {
                    if (allKRadar != null && allKRadar.Contains(this))
                    {
                        allKRadar.Remove(this);
                    }
                }
            }
            catch(Exception e) { }
            
        }

        public override void UpdateBeforeSimulation()
        {
            base.UpdateBeforeSimulation();
            frameCount++;
            debugInfo = "";

            IMyBeacon beacon = (IMyBeacon)Entity;
            if (beacon == null) return;
            // if (beacon.OwnerId != MyAPIGateway.Session.Player.PlayerID)
            //    return;
            if (!beacon.IsWorking || !beacon.IsFunctional || !beacon.Enabled)
                return;

            if ((beaconController == null || !beaconController.IsFunctional) && (frameCount % 60 == 0)) {
                var beaconGrid = (IMyCubeGrid)beacon.GetTopMostParent();
                var beaconTerminal = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(beaconGrid);
                List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
                beaconTerminal.GetBlocksOfType<IMyShipController>(blocks, b => b.IsFunctional);
                if (blocks.Count == 0) return; // must have at least 1 working ship controller on the grid
                beaconController = (IMyShipController)blocks[0];
            }

            if (beaconController == null || !beaconController.IsFunctional) return;

            if (frameCount % 120 == 0) refreshCustomData();

            double radius = (double)beacon.Radius;
            int updateInterval = UPDATE_INTERVAL;
            if(beacon.CubeGrid.GridSizeEnum == MyCubeSize.Small)
            {
                if (frameCount % updateInterval == this.updateFrame)
                {
                    updateSmallKRadar();
                }
                return;
            }
            if (isEW || radius > EWRangeMax)
            {
                maxTrace = (int)((BeaconRangeMax * BeaconRangeMax) / (radius * radius) * EWDefaultCount);
                updateInterval = UPDATE_INTERVAL_EW;
            }
            else
            {
                // if radius > EWRangeMax, the maxCount will be zero, if so, this must be a EWRadar
                maxTrace = (int)((EWRangeMax * EWRangeMax) / (radius * radius) * FCDefaultCount);
            }
            // debugInfo += "maxCount: " + maxCount + "\n";
            if (frameCount % updateInterval != this.updateFrame) return;

            double detectRange = radius * (EWRangeMax / BeaconRangeMax);
            BoundingSphereD sphere = new BoundingSphereD(beacon.GetPosition(), detectRange);
            List<IMyEntity> candicateEntities = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere);
            var cEntitiesP = candicateEntities.Where(x =>
                x.Physics != null &&
                x.Physics.Enabled
            );

            var cAsteroidEntities = cEntitiesP.Where(x => x is IMyVoxelMap);
            // debugInfo += "AsCount: " + (cAsteroidEntities??new List<IMyEntity>()).Count() + "\n";

            var cGridEntities = cEntitiesP.Where(x => x is IMyCubeGrid).Select(x => x.GetTopMostParent())
                .GroupBy(x => ((IMyCubeGrid)x).GetGridGroup(GridLinkTypeEnum.Logical))
                .Select(x => x.OrderByDescending(y => getEntitySize(y)).First()) // choose biggest grid of a grid group TODO find a better way?
                ;

            var cSortedGridEntities = filterGrid(cAsteroidEntities, cGridEntities);
            // debugInfo += "gridCount: " + (cAsteroidEntities ?? new List<IMyEntity>()).Count() + "\n";
            // merge entity of similar direction

            cAvaliableGridEntities = cSortedGridEntities.Take(maxTrace);

            if (LCDs.Count > 0)
            {
                offsetDic = new Dictionary<long, Vector3D>();
                // read offset info
                string offsetListString;
                
                bool haveOffsetData = beacon.Storage.TryGetValue(KRadarMain.LOCK_OFFSET_LIST_KEY, out offsetListString);
                
                if (haveOffsetData && offsetListString!=null && offsetListString!="")
                {
                    List<List<string>> lineFields = offsetListString.Split('\n').Select(delegate (string line)
                    {
                        if (line == null) return null;
                        string[] fields = line.Split(':');
                        if (fields.Length < 4) return null;
                        return fields.ToList();
                    }).Where(l => l != null).ToList();
                    foreach(var fields in lineFields)
                    {
                        long entityId;
                        double x, y, z;
                        if(! long.TryParse(fields[0], out entityId) ) continue;
                        if (!double.TryParse(fields[1], out x)) continue;
                        if (!double.TryParse(fields[2], out y)) continue;
                        if (!double.TryParse(fields[3], out z)) continue;
                        offsetDic.Add(entityId, new Vector3D(x, y, z));
                    };
                }
                long oCount = offsetDic.Count;
                offsetDic = offsetDic.Where(d => cAvaliableGridEntities.Any(g => g.EntityId.Equals(d.Key))).ToDictionary(d => d.Key, d => d.Value);
                long nCount = offsetDic.Count;
                if (oCount != nCount)
                {
                    List<string> outputLines = offsetDic.Select(d => d.Key + ":" + d.Value.X + ":" + d.Value.Y + ":" + d.Value.Z).ToList();
                    offsetListString = String.Join("\n", outputLines);
                    beacon.Storage.Add(KRadarMain.LOCK_OFFSET_LIST_KEY, offsetListString);
                }

                StringBuilder sb = new StringBuilder();
                sb.Append(DateTime.UtcNow.Ticks).Append('\n');
                if (debugInfo.Length > 0) sb.Append(debugInfo);
                foreach (IMyEntity foundEntity in cAvaliableGridEntities ?? new List<IMyEntity>())
                {
                    // OUTPUT
                    Vector3D pos = getCenter(foundEntity, offsetDic);
                    IMyCubeGrid grid = (IMyCubeGrid)foundEntity.GetTopMostParent();
                    double gridSize = getEntitySize(foundEntity);
                    sb.Append(Math.Round(gridSize,2)).Append(":").Append(pos.X).Append(":").Append(pos.Y).Append(":").Append(pos.Z);
                    if (isEW == false)
                    {
                        Vector3D ve = foundEntity.Physics.LinearVelocity;
                        sb.Append(":").Append(ve.X).Append(":").Append(ve.Y).Append(":").Append(ve.Z);
                    }
                    sb.Append("\n");
                }

                foreach (var lcd in LCDs)
                {
                    lcd.WriteText(sb.ToString());
                }

                
            }

            lastUpdateFrame = frameCount;
        }

        private void updateSmallKRadar()
        {
            IMyBeacon beacon = (IMyBeacon)Entity;
            double detectRange = Math.Min(5000, beacon.Radius);
            BoundingSphereD sphere = new BoundingSphereD(beacon.GetPosition(), detectRange);
            List<IMyEntity> candicateEntities = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere);
            var cEntitiesP = candicateEntities.Where(x =>
                x.Physics != null &&
                x.Physics.Enabled
            );

            var cGridEntities = cEntitiesP.Where(x => x is IMyCubeGrid).Select(x => x.GetTopMostParent())
                .GroupBy(x => ((IMyCubeGrid)x).GetGridGroup(GridLinkTypeEnum.Logical))
                .Select(x => x.OrderByDescending(y => getEntitySize(y)).First()) // choose biggest grid of a grid group TODO find a better way?
                ;

            var cDirEntities = cGridEntities.Select(x =>
            {
                var ret = new RadarEntityExtend(x);
                ret.center = getCenter(x);
                var rpos = ret.center - beacon.GetPosition();
                ret.distance = rpos.Length();
                if (ret.distance == 0) return null;
                ret.dir = Vector3D.Normalize(rpos);

                return ret;
            }).Where(x => {
                if (x == null) return false;
                return (Vector3D.Dot(x.dir, beacon.WorldMatrix.Forward) > SMALL_KRADAR_ANGLE_LIMIT);
            });
            List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
            var cHasPower = cDirEntities.Where(x =>
            {
                IMyCubeGrid grid = (IMyCubeGrid)(x.entity.GetTopMostParent());
                // ignore no powered 
                var terminal = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(grid);
                blocks.Clear();
                terminal.GetBlocksOfType<IMyReactor>(blocks, y => y.IsFunctional);
                if (blocks.Any()) return true;
                blocks.Clear();
                terminal.GetBlocksOfType<IMyBatteryBlock>(blocks, y => y.IsFunctional);
                if (blocks.Any()) return true;
                blocks.Clear();
                terminal.GetBlocksOfType<IMyDecoy>(blocks, y => y.IsFunctional);
                if (blocks.Any()) return true;

                return false;
            });

            List<IMyPlayer> playerList = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(playerList, p => p.IdentityId == beacon.OwnerId);
            IMyPlayer radarOwnerPlayer = playerList[0];
            var cEnemyEntities = cHasPower.Where(x =>
            {
                IMyCubeGrid grid = (IMyCubeGrid)(x.entity.GetTopMostParent());
                // ignore friendly
                List<long> owners = new List<long>(grid.BigOwners);
                owners.Concat(grid.SmallOwners);
                var allowners = owners.Distinct();
                // debugInfo += "OwnerCount: " + owners.Count + (owners.Count > 0 ? " " + owners[0]: "") + (owners.Count > 1 ? " " + owners[1] : "") + "\n";
                bool isEnemy = allowners.Any(o => !radarOwnerPlayer.GetRelationTo(o).IsFriendly());
                return isEnemy;
            });

            var cSortedEntities = cEnemyEntities.OrderBy(x => x.distance).Select(x => x.entity);
            // debugInfo += "gridCount: " + (cAsteroidEntities ?? new List<IMyEntity>()).Count() + "\n";
            // merge entity of similar direction

            cAvaliableGridEntities = cSortedEntities.Take(1);

            if (LCDs.Count > 0)
            {
                offsetDic = new Dictionary<long, Vector3D>();
                // read offset info
                string offsetListString;

                bool haveOffsetData = beacon.Storage.TryGetValue(KRadarMain.LOCK_OFFSET_LIST_KEY, out offsetListString);

                if (haveOffsetData && offsetListString != null && offsetListString != "")
                {
                    List<List<string>> lineFields = offsetListString.Split('\n').Select(delegate (string line)
                    {
                        if (line == null) return null;
                        string[] fields = line.Split(':');
                        if (fields.Length < 4) return null;
                        return fields.ToList();
                    }).Where(l => l != null).ToList();
                    foreach (var fields in lineFields)
                    {
                        long entityId;
                        double x, y, z;
                        if (!long.TryParse(fields[0], out entityId)) continue;
                        if (!double.TryParse(fields[1], out x)) continue;
                        if (!double.TryParse(fields[2], out y)) continue;
                        if (!double.TryParse(fields[3], out z)) continue;
                        offsetDic.Add(entityId, new Vector3D(x, y, z));
                    };
                }
                long oCount = offsetDic.Count;
                offsetDic = offsetDic.Where(d => cAvaliableGridEntities.Any(g => g.EntityId.Equals(d.Key))).ToDictionary(d => d.Key, d => d.Value);
                long nCount = offsetDic.Count;
                if (oCount != nCount)
                {
                    List<string> outputLines = offsetDic.Select(d => d.Key + ":" + d.Value.X + ":" + d.Value.Y + ":" + d.Value.Z).ToList();
                    offsetListString = String.Join("\n", outputLines);
                    beacon.Storage.Add(KRadarMain.LOCK_OFFSET_LIST_KEY, offsetListString);
                }

                StringBuilder sb = new StringBuilder();
                sb.Append(DateTime.UtcNow.Ticks).Append('\n');
                if (debugInfo.Length > 0) sb.Append(debugInfo);
                foreach (IMyEntity foundEntity in cAvaliableGridEntities ?? new List<IMyEntity>())
                {
                    // OUTPUT
                    Vector3D pos = getCenter(foundEntity, offsetDic);
                    IMyCubeGrid grid = (IMyCubeGrid)foundEntity.GetTopMostParent();
                    double gridSize = getEntitySize(foundEntity);
                    sb.Append(Math.Round(gridSize, 2)).Append(":").Append(pos.X).Append(":").Append(pos.Y).Append(":").Append(pos.Z);
                    if (isEW == false)
                    {
                        Vector3D ve = foundEntity.Physics.LinearVelocity;
                        sb.Append(":").Append(ve.X).Append(":").Append(ve.Y).Append(":").Append(ve.Z);
                    }
                    sb.Append("\n");
                }

                foreach (var lcd in LCDs)
                {
                    lcd.WriteText(sb.ToString());
                }

                
            }
            lastUpdateFrame = frameCount;

        }

        private IEnumerable<IMyEntity> filterGrid(IEnumerable<IMyEntity> cAsteroidEntities, IEnumerable<IMyEntity> cGridEntities)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            var beacon = (IMyBeacon)Entity;

            List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();

            List<IMyPlayer> playerList = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(playerList, p => p.IdentityId == beacon.OwnerId);
            IMyPlayer radarOwnerPlayer = playerList[0];
            // 1. filter no power
            var cHavePower = cGridEntities.Select(x => {
                var grid = (IMyCubeGrid)x.GetTopMostParent();
                var terminal = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(grid);
                var ret = new RadarEntityExtend(x);
                ret.center = grid.Physics.CenterOfMassWorld;
                var ran = ret.center - beacon.GetPosition();

                var dir = Vector3D.Normalize(ran);
                ret.dir = dir;
                ret.distance = ran.Length();
                return ret;
            }).Where(x =>
            {
                IMyCubeGrid grid = (IMyCubeGrid)(x.entity.GetTopMostParent());
                var terminal = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(grid);
                // ignore friendly small grid(missiles)
                List<long> owners = new List<long>(grid.BigOwners);
                owners.Concat(grid.SmallOwners);
                var allowners = owners.Distinct();
                bool isEnemy = allowners.Any(o => !radarOwnerPlayer.GetRelationTo(o).IsFriendly());
                List<IMyTerminalBlock> terminalBlocks = new List<IMyTerminalBlock>();
                terminal.GetBlocks(terminalBlocks);
                if (!isEnemy && terminalBlocks.Count < 10 && grid.GridSizeEnum == MyCubeSize.Small) return false;

                // ignore no powered 
                blocks.Clear();
                terminal.GetBlocksOfType<IMyReactor>(blocks, y => y.IsFunctional);
                if (blocks.Any()) return true;
                blocks.Clear();
                terminal.GetBlocksOfType<IMyBatteryBlock>(blocks, y => y.IsFunctional);
                if (blocks.Any()) return true;
                blocks.Clear();
                terminal.GetBlocksOfType<IMyDecoy>(blocks, y => y.IsFunctional);
                if (blocks.Any()) return true;

                return false;
            });
            
            var performP1 = stopWatch.ElapsedTicks;

            // 2. filter planet coverage
            var cUnPlanetCover = cHavePower;
            Vector3D planetPos = new Vector3D();
            bool havePlanetPos = beaconController.TryGetPlanetPosition(out planetPos);
            double planetElv = 0;
            bool havePlanetElv = beaconController.TryGetPlanetElevation(Sandbox.ModAPI.Ingame.MyPlanetElevation.Sealevel, out planetElv);
            if(havePlanetPos && havePlanetElv)
            {
                var planetDis = (planetPos - beacon.GetPosition()).Length();
                var planetRadius = planetDis - planetElv;
                var coneHeightOp = planetRadius * (planetRadius / planetDis);
                var coneHeight = planetDis - coneHeightOp;
                var cosLimit = 0D;
                if (coneHeight > 0)
                {
                    var bcLength = Math.Sqrt((planetDis * planetDis) - (planetRadius * planetRadius));
                    cosLimit = coneHeight / bcLength;
                }
                var planetDir = Vector3D.Normalize(planetPos - beacon.GetPosition());
                //debugInfo += "planet : " + planetDis + " " + planetElv + " " + cosLimit + "\n";
                //debugInfo += "candidate Count: " + cHavePower.Count() + "\n";
                cUnPlanetCover = cHavePower.Where(x =>
                {
                    //debugInfo += "planetFilter start. \n";
                    var cos = Vector3D.Dot(x.dir, planetDir);
                    //debugInfo += "coverCos: " + cos + "\n";

                    if (cos < cosLimit) return true; // case 1 < cosLimit, radar can see it.
                    if (coneHeight < 0) return false; // case 1.1 if radar is under sealevel, all back object is unseen;

                    // case 2, > cosLimit calc distance with angle
                    var maxRange = coneHeight / cos;
                    //debugInfo += "coverDis: " + (pos - beacon.GetPosition()).Length() + " " + maxRange + "\n";
                    if ((x.center - beacon.GetPosition()).Length() < maxRange) return true;

                    return false;
                });
            }
            var performP2 = stopWatch.ElapsedTicks;

            // 3. resolution limit filter
            var cResoEntityExtendList = cUnPlanetCover
            .Select(x =>
            {
                var grid = (IMyCubeGrid)(x.entity.GetTopMostParent());
                var distance = x.distance;
                if (distance <= 0) return null;
                var dir = x.dir;
                var size = getEntitySize(x.entity);

                // 3.3 decoy default radius and more closer
                bool forceCover = false;
                while (true)
                {
                    var terminal = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(grid);
                    List<IMyTerminalBlock> terminalBlocks = new List<IMyTerminalBlock>();
                    terminal.GetBlocks(terminalBlocks);
                    if (terminalBlocks.Count > 20) // simplified!
                    {
                        forceCover = true;
                        break;
                    }
                    List<IMyDecoy> decoyList = new List<IMyDecoy>();
                    terminal.GetBlocksOfType<IMyDecoy>(decoyList);
                    if (decoyList.Count == 0) break;
                    // ignore friendly decoy?
                    var decoy = decoyList[0];
                    if (radarOwnerPlayer.GetRelationTo(decoy.OwnerId).IsFriendly()) break;

                    if (grid.GridSizeEnum == MyCubeSize.Large)
                    {
                        size += 200;
                        if (distance > 201) distance -= 200;
                        else distance = 1;
                    } else
                    {
                        size += 40;
                        if (distance > 41) distance -= 40;
                        else distance = 1;
                    }
                    forceCover = true;

                    break;
                }

                // Balance Adjust: beacon grid should be smaller, otherwise it will cover too much
                if (((IMyCubeGrid)beacon.GetTopMostParent()).IsInSameLogicalGroupAs(grid))
                {
                    size *= 0.5;
                    // debugInfo += "beacon grid signal: " + x.LocalAABB.Max + " " + x.LocalAABB.Min + " " + size + " " + distance + "\n";
                }

                var signalRadius = size / Math.Sqrt(size * size + distance * distance);
                x.type = 1;
                x.signalRadius = signalRadius;
                x.forceCover = forceCover;
                return x;
            }).Where(x => x!=null);
            var performP21 = stopWatch.ElapsedTicks;

            cResoEntityExtendList.Concat(cAsteroidEntities
            .Where(x => (getCenter(x) - beacon.GetPosition()).Length() > 1)
            .Select(delegate (IMyEntity x) 
            {
                var ret = new RadarEntityExtend(x);
                ret.center = getCenter(x);
                var rPos = ret.center - beacon.GetPosition();
                var distance = rPos.Length();
                var dir = Vector3D.Normalize(rPos);
                var size = getEntitySize(x);
                var signalRadius = size / Math.Sqrt(size * size + distance * distance);
                ret.type = 0;
                ret.signalRadius = signalRadius;
                ret.distance = distance;
                ret.dir = dir;
                ret.forceCover = true;
                return ret;
            }));
            var performP22 = stopWatch.ElapsedTicks;
            var cDirectionEntityExtendList = cResoEntityExtendList.GroupBy(x => {
                Vector3D realDir = x.dir;
                Vector3D resoDir = new Vector3D(Math.Round((int)(realDir.X / DirResoRadius) * DirResoRadius, 3),
                    Math.Round((int)(realDir.Y / DirResoRadius) * DirResoRadius, 3), 
                    Math.Round((int)(realDir.Z / DirResoRadius) * DirResoRadius, 3));
                return resoDir;
            }).SelectMany(g =>
            {
                List<RadarEntityExtend> ret = new List<RadarEntityExtend>();

                // 3.1 find uncovered
                var sorted = g.OrderByDescending(x => x.signalRadius);

                double maxDis = double.MaxValue;
                foreach (var c in sorted)
                {
                    if (c.distance > maxDis) continue;
                    ret.Add(c);
                    maxDis = c.distance;
                }

                return ret;
            });
            var performP3 = stopWatch.ElapsedTicks;

            // 4. filter big signal coverage
            var cUnCoveredEntityExtendList = cDirectionEntityExtendList.OrderBy(x => x.distance).ToList();
            double BigSignalRadiusLimit = DirResoRadius * 0.1;
            for (int iUnCoveredProcessed = 0; iUnCoveredProcessed < cUnCoveredEntityExtendList.Count; iUnCoveredProcessed ++)
            {
                var current = cUnCoveredEntityExtendList[iUnCoveredProcessed];
                bool canCover = current.signalRadius > BigSignalRadiusLimit;
                if (current.forceCover) canCover = true;
                if (!canCover) continue;
                // debugInfo += "+1 big signal: " + (iUnCoveredProcessed) + " " + current.signalRadius + "\n";
                for (int iLeftIdx = iUnCoveredProcessed + 1; iLeftIdx < cUnCoveredEntityExtendList.Count; iLeftIdx ++ )
                {
                    var toCheck = cUnCoveredEntityExtendList[iLeftIdx];

                    //if (Vector3D.Dot(toCheck.dir, current.dir) > 0.8)
                    //{
                    //    debugInfo += "cover check: " + iUnCoveredProcessed + " " + iLeftIdx +
                    //        " " + Math.Round(current.signalRadius,2) + " " + Math.Round(toCheck.signalRadius,2) +
                    //        " " + Math.Round(Math.Sqrt(1 - (current.signalRadius * current.signalRadius)),2) +
                    //        " " + Math.Round(Vector3D.Dot(toCheck.dir, current.dir),2) +
                    //        "\n";
                    //}

                    if (toCheck.signalRadius > BigSignalRadiusLimit) // simplified! big signal can not be covered
                    {
                        continue;
                    }

                    var cosLimit = Math.Sqrt(1 - (current.signalRadius * current.signalRadius));
                    if (Vector3D.Dot(toCheck.dir, current.dir) > cosLimit) // simplified! small signal as a point (no radius)
                    {
                        cUnCoveredEntityExtendList.RemoveAt(iLeftIdx);
                        iLeftIdx--;
                    }
                }
            }

            // remove asteroid
            var cUnCoveredEntities = cUnCoveredEntityExtendList.Where(x => x.type == 1);
            var performP4 = stopWatch.ElapsedTicks;
            // 5. filter friendly
            var cNoFriend = cUnCoveredEntities.Where(x =>
            {
                IMyCubeGrid grid = (IMyCubeGrid)(x.entity.GetTopMostParent());
                // ignore friendly
                List<long> owners = new List<long>(grid.BigOwners);
                owners.Concat(grid.SmallOwners);
                var allowners = owners.Distinct();
                // debugInfo += "OwnerCount: " + owners.Count + (owners.Count > 0 ? " " + owners[0]: "") + (owners.Count > 1 ? " " + owners[1] : "") + "\n";
                bool isEnemy = allowners.Any(o => !radarOwnerPlayer.GetRelationTo(o).IsFriendly());
                return isEnemy;
            }).Select(x=>x.entity).ToList();
            var performP5 = stopWatch.ElapsedTicks;
            stopWatch.Stop();
            // 6 sorted already sorted in step 4
            // debugInfo += $"{performP1} {performP2} {performP21} {performP22} {performP3} {performP4} {performP5}\n";
            return cNoFriend;
        }

        private double getEntitySize(IMyEntity e, bool round = false)
        {
            double size = (e.PositionComp.LocalAABB.Max - e.PositionComp.LocalAABB.Min).Length();

            if (round) size = Math.Round(size);
            return size;
        }

        private Vector3D getCenter(IMyEntity e, Dictionary<long, Vector3D> offsetDic = null)
        {
            var grid = e.GetTopMostParent();
            if (offsetDic == null) return grid.Physics.CenterOfMassWorld;
            Vector3D offset;
            bool haveOffset = offsetDic.TryGetValue(grid.EntityId, out offset);
            if (haveOffset)
            {
                return grid.GetPosition() + Vector3D.TransformNormal(offset, grid.WorldMatrix);
            } else
            {
                return grid.Physics.CenterOfMassWorld;
            }
        }

        private void refreshCustomData()
        {
            IMyBeacon beacon = (IMyBeacon)Entity;
            if (beacon.CubeGrid.GridSizeEnum == MyCubeSize.Small)
            {
                refreshCustomDataSmall();
                return;
            }

            double radius = beacon.Radius;
            CustomConfiguration cfg = new CustomConfiguration((IMyTerminalBlock)Entity);
            cfg.Load();
            cfg.Get("isEW", ref this.isEW);
            if (radius >= EWRangeMax) this.isEW = true;
            cfg.Set("isEW", this.isEW?"true":"false");

            cfg.Get("nameLCD", ref this.nameLCD);
            cfg.Set("nameLCD", this.nameLCD);

            cfg.Set("maxTrack", "" + this.maxTrace);
            cfg.Get("nameCamera", ref this.nameCamera);
            cfg.Set("nameCamera", this.nameCamera);
            cfg.Save();

            if (this.nameLCD != null && this.nameLCD.Length > 2)
            {
                var grid = (IMyCubeGrid)Entity.GetTopMostParent();
                var terminal = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(grid);
                List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
                terminal.GetBlocksOfType<IMyTextPanel>(blocks, b => b.CustomName.Contains(this.nameLCD));
                LCDs.Clear();
                LCDs.AddList(blocks.Select(b => (IMyTextSurface)b).ToList());
                blocks.Clear();
                terminal.GetBlocksOfType<IMyTextSurfaceProvider>(blocks, b => b.CustomName.Contains(this.nameLCD));
                LCDs.AddList(blocks.Select(delegate (IMyTerminalBlock b)
                {
                    var sp = (IMyTextSurfaceProvider)b;
                    if (sp.SurfaceCount > 1)
                    {
                        return (IMyTextSurface)sp.GetSurface(1);
                    } else
                    {
                        return (IMyTextSurface)sp.GetSurface(0);
                    }
                }).ToList());
            }
        }

        private void refreshCustomDataSmall()
        {
            IMyBeacon beacon = (IMyBeacon)Entity;
            CustomConfiguration cfg = new CustomConfiguration((IMyTerminalBlock)Entity);
            cfg.Load();

            cfg.Get("nameLCD", ref this.nameLCD);
            cfg.Set("nameLCD", this.nameLCD);

            cfg.Get("nameCamera", ref this.nameCamera);
            cfg.Set("nameCamera", this.nameCamera);

            double tmpLimit = 0;
            cfg.Get("angleLimit", ref tmpLimit);
            if (tmpLimit != 0) this.SMALL_KRADAR_ANGLE_LIMIT = tmpLimit;
            cfg.Set("angleLimit", "" + this.SMALL_KRADAR_ANGLE_LIMIT);
            cfg.Save();

            if (this.nameLCD != null && this.nameLCD.Length > 2)
            {
                var grid = (IMyCubeGrid)Entity.GetTopMostParent();
                var terminal = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(grid);
                List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
                terminal.GetBlocksOfType<IMyTextPanel>(blocks, b => b.CustomName.Contains(this.nameLCD));
                LCDs.Clear();
                LCDs.AddList(blocks.Select(b => (IMyTextSurface)b).ToList());
                blocks.Clear();
                terminal.GetBlocksOfType<IMyTextSurfaceProvider>(blocks, b => b.CustomName.Contains(this.nameLCD));
                LCDs.AddList(blocks.Select(delegate (IMyTerminalBlock b)
                {
                    var sp = (IMyTextSurfaceProvider)b;
                    if (sp.SurfaceCount > 1)
                    {
                        return (IMyTextSurface)sp.GetSurface(1);
                    }
                    else
                    {
                        return (IMyTextSurface)sp.GetSurface(0);
                    }
                }).ToList());
            }
        }

        /*
        private bool judgeFriendly(long gridOwner, long radarOwner)
        {
            if (gridOwner == radarOwner) return true;
            List<IMyPlayer> playerList = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(playerList, p => p.IdentityId == radarOwner);
            if (playerList.Count == 0) return false;
            IMyPlayer radarOwnerPlayer = playerList[0];
            return radarOwnerPlayer.GetRelationTo(gridOwner).IsFriendly();
        }
        */
    }

    public class CustomConfiguration
    {
        public IMyTerminalBlock configBlock;
        public Dictionary<string, string> config;

        public CustomConfiguration(IMyTerminalBlock block)
        {
            configBlock = block;
            config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public void Load()
        {
            ParseCustomData(configBlock, config);
        }

        public void Save()
        {
            WriteCustomData(configBlock, config);
        }

        public string Get(string key, string defVal = null)
        {
            return config.GetValueOrDefault(key.Trim(), defVal);
        }

        public void Get(string key, ref string res)
        {
            string val;
            if (config.TryGetValue(key.Trim(), out val))
            {
                res = val;
            }
        }

        public void Get(string key, ref int res)
        {
            int val;
            if (int.TryParse(Get(key), out val))
            {
                res = val;
            }
        }

        public void Get(string key, ref float res)
        {
            float val;
            if (float.TryParse(Get(key), out val))
            {
                res = val;
            }
        }

        public void Get(string key, ref double res)
        {
            double val;
            if (double.TryParse(Get(key), out val))
            {
                res = val;
            }
        }

        public void Get(string key, ref bool res)
        {
            bool val;
            if (bool.TryParse(Get(key), out val))
            {
                res = val;
            }
        }
        public void Get(string key, ref bool? res)
        {
            bool val;
            if (bool.TryParse(Get(key), out val))
            {
                res = val;
            }
        }

        public void Set(string key, string value)
        {
            config[key.Trim()] = value;
        }

        public static void ParseCustomData(IMyTerminalBlock block, Dictionary<string, string> cfg, bool clr = true)
        {
            if (clr)
            {
                cfg.Clear();
            }

            string[] arr = block.CustomData.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < arr.Length; i++)
            {
                string ln = arr[i];
                string va;

                int p = ln.IndexOf('=');
                if (p > -1)
                {
                    va = ln.Substring(p + 1);
                    ln = ln.Substring(0, p);
                }
                else
                {
                    va = "";
                }
                cfg[ln.Trim()] = va.Trim();
            }
        }

        public static void WriteCustomData(IMyTerminalBlock block, Dictionary<string, string> cfg)
        {
            StringBuilder sb = new StringBuilder(cfg.Count * 100);
            foreach (KeyValuePair<string, string> va in cfg)
            {
                sb.Append(va.Key).Append('=').Append(va.Value).Append('\n');
            }
            block.CustomData = sb.ToString();
        }
    }

}
