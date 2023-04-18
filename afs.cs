using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRage.Game;
using VRage.Game.ModAPI.Ingame;
using VRageMath;

namespace kradar_p
{
    partial class afs : MyGridProgram
    {
        #region ingamescript

        /*
        Kaien's automatic fly system V0.1
        */

        void Main(string arguments, UpdateType updateType)
        {
            // start up
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            tickPlus();
            debugClear();

            // parse command
            parseRadar(arguments);

            // check ship
            checkShip();

            // parse input
            parseInput();

            // decide mode
            decideMode();
            debug("ab: " + autoBalance + " ad: " + autoDown);

            if (!isStandBy) {
              // calcFollowNA
              calcFollowNA();

              // turn level 1
              balanceGravity();

              // adjust thrust level 1
              controlThrust();

              // turn level 2

              // adjust thrust level 2
            }

            debugShow();
        }

        #region tick
        long tick = 0;
        void tickPlus()
        {
            tick++;
        }
        long tickGet()
        {
            return tick;
        }
        #endregion tick

        #region debug
        string debugInfo;
        void debug(string info)
        {
            debugInfo += info + "\n";
        }
        void debugClear()
        {
            debugInfo = "";
        }
        string[] runIndiStr = new string[] {"|", "/", "-", "\\"};
        void debugShow()
        {
            Echo("Kaien Automatic Fly System V0.1 " + runIndiStr[tickGet()/10 % 4] + "\n");
            Echo(debugInfo);
        }
        #endregion debug

        #region checkship
        IMyShipController mainShipCtrl;
        Vector3D shipVelLocal = Vector3D.Zero;
        Vector3D shipPosition = Vector3D.Zero;
        MatrixD shipRevertMat;
        List<List<List<IMyThrust>>> shipThrusts = new List<List<List<IMyThrust>>>();
        List<IMyGyro> shipGyros = new List<IMyGyro>();
        List<List<string>> gyroFields = new List<List<string>>();
        List<List<int>> gyroFactors = new List<List<int>>();
        const int G_YAW = 0;
        const int G_PITCH = 1;
        const int G_ROLL = 2;
        Vector3D pGravity = Vector3D.Zero;
        double shipMaxForce = 0;
        double shipMass = 0;
        void checkShip()
        {
            getBlocks();

            if (mainShipCtrl == null) return;

            Vector3D sv = mainShipCtrl.GetShipVelocities().LinearVelocity;
            shipRevertMat = MatrixD.CreateLookAt(new Vector3D(), mainShipCtrl.WorldMatrix.Forward, mainShipCtrl.WorldMatrix.Up);
            shipVelLocal = Vector3D.TransformNormal(sv, shipRevertMat);
            shipPosition = mainShipCtrl.GetPosition();

            pGravity = Vector3D.TransformNormal(mainShipCtrl.GetNaturalGravity(), shipRevertMat);
            shipMaxForce = 0;
            foreach (IMyThrust t in shipThrusts[0][T_UP])
            {
                shipMaxForce += t.MaxEffectiveThrust;
            }
            shipMass = mainShipCtrl.CalculateShipMass().PhysicalMass;
        }

        Vector3D shipVelLocalGet()
        {
            return shipVelLocal;
        }
        void getBlocks()
        {
            List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
            if (mainShipCtrl == null)
            {
                GridTerminalSystem.GetBlocksOfType<IMyShipController>(blocks, b => b.CubeGrid == Me.CubeGrid);
                mainShipCtrl = (IMyShipController)matchNameOrFirst(blocks, "main");
            }
            if (mainShipCtrl == null) return;

            if (tickGet() % 300 == 0)
            {
                shipThrusts.Clear();
                shipGyros.Clear();
                gyroFields.Clear();
                gyroFactors.Clear();
            }

            if (shipThrusts.Count == 0) getThrusts();
            if (shipGyros.Count == 0) getGyros();
        }
        void getGyros()
        {
            List<IMyGyro> blocks = new List<IMyGyro>();
            GridTerminalSystem.GetBlocksOfType<IMyGyro>(blocks, b => b.CubeGrid == Me.CubeGrid);
            foreach(IMyGyro g in blocks)
            {
                shipGyros.Add(g);
                Base6Directions.Direction gyroUp = g.WorldMatrix.GetClosestDirection(mainShipCtrl.WorldMatrix.Up);
                Base6Directions.Direction gyroLeft = g.WorldMatrix.GetClosestDirection(mainShipCtrl.WorldMatrix.Left);
                Base6Directions.Direction gyroForward = g.WorldMatrix.GetClosestDirection(mainShipCtrl.WorldMatrix.Forward);
                List<string> fields = new List<string> { "", "", "" };
                gyroFields.Add(fields);
                List<int> factors = new List<int> { 0, 0, 0 };
                gyroFactors.Add(factors);
                switch (gyroUp)
                {
                    case Base6Directions.Direction.Up:
                        fields[G_YAW] = "Yaw";
                        factors[G_YAW] = 1;
                        break;
                    case Base6Directions.Direction.Down:
                        fields[G_YAW] = "Yaw";
                        factors[G_YAW] = -1;
                        break;
                    case Base6Directions.Direction.Left:
                        fields[G_YAW] = "Pitch";
                        factors[G_YAW] = 1;
                        break;
                    case Base6Directions.Direction.Right:
                        fields[G_YAW] = "Pitch";
                        factors[G_YAW] = -1;
                        break;
                    case Base6Directions.Direction.Forward:
                        fields[G_YAW] = "Roll";
                        factors[G_YAW] = -1;
                        break;
                    case Base6Directions.Direction.Backward:
                        fields[G_YAW] = "Roll";
                        factors[G_YAW] = 1;
                        break;
                }

                switch (gyroLeft)
                {
                    case Base6Directions.Direction.Up:
                        fields[G_PITCH] = "Yaw";
                        factors[G_PITCH] = 1;
                        break;
                    case Base6Directions.Direction.Down:
                        fields[G_PITCH] = "Yaw";
                        factors[G_PITCH] = -1;
                        break;
                    case Base6Directions.Direction.Left:
                        fields[G_PITCH] = "Pitch";
                        factors[G_PITCH] = 1;
                        break;
                    case Base6Directions.Direction.Right:
                        fields[G_PITCH] = "Pitch";
                        factors[G_PITCH] = -1;
                        break;
                    case Base6Directions.Direction.Forward:
                        fields[G_PITCH] = "Roll";
                        factors[G_PITCH] = -1;
                        break;
                    case Base6Directions.Direction.Backward:
                        fields[G_PITCH] = "Roll";
                        factors[G_PITCH] = 1;
                        break;
                }

                switch (gyroForward)
                {
                    case Base6Directions.Direction.Up:
                        fields[G_ROLL] = "Yaw";
                        factors[G_ROLL] = 1;
                        break;
                    case Base6Directions.Direction.Down:
                        fields[G_ROLL] = "Yaw";
                        factors[G_ROLL] = -1;
                        break;
                    case Base6Directions.Direction.Left:
                        fields[G_ROLL] = "Pitch";
                        factors[G_ROLL] = 1;
                        break;
                    case Base6Directions.Direction.Right:
                        fields[G_ROLL] = "Pitch";
                        factors[G_ROLL] = -1;
                        break;
                    case Base6Directions.Direction.Forward:
                        fields[G_ROLL] = "Roll";
                        factors[G_ROLL] = -1;
                        break;
                    case Base6Directions.Direction.Backward:
                        fields[G_ROLL] = "Roll";
                        factors[G_ROLL] = 1;
                        break;
                }
            }
        }
        float gyroDZ = 0.01F;
        double rateAdjust(double r)
        {
            return r * r * r * (60 / (gyroDZ * gyroDZ));
        }
        bool gyroAntiDithering = false;
        void SetGyroYaw(double yawRate)
        {
            if (gyroAntiDithering && Math.Abs(yawRate) < gyroDZ) yawRate = rateAdjust(yawRate);
            else yawRate *= 60;
            for (int i = 0; i < shipGyros.Count; i++)
            {
                shipGyros[i].SetValue(gyroFields[i][G_YAW], (float)yawRate * gyroFactors[i][G_YAW]);
            }
        }

        void SetGyroPitch(double pitchRate)
        {
            if (gyroAntiDithering && Math.Abs(pitchRate) < gyroDZ) pitchRate = rateAdjust(pitchRate);
            else pitchRate *= 60;
            for (int i = 0; i < shipGyros.Count; i++)
            {
                shipGyros[i].SetValue(gyroFields[i][G_PITCH], (float)pitchRate * gyroFactors[i][G_PITCH]);
            }
        }

        void SetGyroRoll(double rollRate)
        {
            if (gyroAntiDithering && Math.Abs(rollRate) < gyroDZ) rollRate = rateAdjust(rollRate);
            else rollRate *= 60;
            for (int i = 0; i < shipGyros.Count; i++)
            {
                shipGyros[i].SetValue(gyroFields[i][G_ROLL], (float)rollRate * gyroFactors[i][G_ROLL]);
            }
        }
        const int T_LEFT = 0;
        const int T_RIGHT = 1;
        const int T_DOWN = 2;
        const int T_UP = 3;
        const int T_FRONT = 4;
        const int T_BACK = 5;
        int mafInd = 0;
        void getThrusts()
        {
            List<List<IMyThrust>> l0Thrusts = new List<List<IMyThrust>>();
            shipThrusts.Add(l0Thrusts);
            for (int i = 0; i < 6; i++)
            {
                l0Thrusts.Add(new List<IMyThrust>());
            }
            List<IMyThrust> blocks = new List<IMyThrust>();
            GridTerminalSystem.GetBlocksOfType<IMyThrust>(blocks, b => b.CubeGrid == Me.CubeGrid);
            List<double> mafThusts = new List<double> { 0, 0, 0, 0, 0, 0 };

            foreach (IMyThrust t in blocks)
            {
                Base6Directions.Direction thrusterDirection = mainShipCtrl.WorldMatrix.GetClosestDirection(t.WorldMatrix.Backward);
                switch (thrusterDirection)
                {
                    case Base6Directions.Direction.Forward:
                        l0Thrusts[T_FRONT].Add(t);
                        mafThusts[T_FRONT] += t.MaxEffectiveThrust;
                        break;
                    case Base6Directions.Direction.Backward:
                        l0Thrusts[T_BACK].Add(t);
                        mafThusts[T_BACK] += t.MaxEffectiveThrust;
                        break;
                    case Base6Directions.Direction.Left:
                        l0Thrusts[T_LEFT].Add(t);
                        mafThusts[T_LEFT] += t.MaxEffectiveThrust;
                        break;
                    case Base6Directions.Direction.Right:
                        l0Thrusts[T_RIGHT].Add(t);
                        mafThusts[T_RIGHT] += t.MaxEffectiveThrust;
                        break;
                    case Base6Directions.Direction.Down:
                        l0Thrusts[T_DOWN].Add(t);
                        mafThusts[T_DOWN] += t.MaxEffectiveThrust;
                        break;
                    case Base6Directions.Direction.Up:
                        l0Thrusts[T_UP].Add(t);
                        mafThusts[T_UP] += t.MaxEffectiveThrust;
                        break;
                }
                double maf = mafThusts.Max();
                mafInd = mafThusts.IndexOf(maf);
            }
        }
        int thrustMaxDir()
        {
            return mafInd;
        }
        IMyTerminalBlock matchNameOrFirst(List<IMyTerminalBlock> blocks, string name)
        {
            IMyTerminalBlock ret = null;
            if (blocks.Count > 1)
            {
                ret = blocks.Find(b => b.CustomName.ToLower().Contains(name));
            }
            if (ret == null && blocks.Count > 0)
            {
                ret = (IMyShipController)blocks[0];
            }
            return ret;
        }
        #endregion checkship

        #region parseInput
        Vector3D angleInput = Vector3D.Zero;
        Vector3D moveInput = Vector3D.Zero;
        void parseInput()
        {
            if (mainShipCtrl == null) return;
            angleInput = new Vector3D(mainShipCtrl.RotationIndicator.X, mainShipCtrl.RotationIndicator.Y, mainShipCtrl.RollIndicator);
            moveInput = mainShipCtrl.MoveIndicator;
        }
        #endregion parseInput

        #region decideMode
        long spaceStart = 0;
        bool autoBalance = false;
        long downStart = 0;
        bool autoDown = false;
        bool autoFollow = false;
        bool needBalance = false;
        void decideMode()
        {
            shortClick(ref spaceStart, moveInput.Y, true, 0.5, 0.1, ref autoBalance);

            if (moveInput.Y < -0.5) autoBalance = false;
            if (Math.Abs(angleInput.Z) > 0.5) autoBalance = false;

            shortClick(ref downStart, moveInput.Y, false, -0.5, -0.1, ref autoDown);
            if (moveInput.Y > 0.5) autoDown = false;
            if (Math.Abs(angleInput.Z) > 0.5) autoDown = false;

            // follow mode
            if(cmdFollow) {
                autoBalance = false;
                autoDown = false;
                autoFollow = true;
            }
            needBalance = autoBalance || autoDown;
        }
        void shortClick(ref long si, double inp, bool isP, double tl, double dl, ref bool mode)
        {
            if (si == 0)
            {
                if ((isP && inp > tl) || (!isP && inp < tl))
                {
                    si = tickGet();
                    mode = false;
                }
            } else
            {
                if ((isP && inp < dl) || (!isP && inp > dl) )
                {
                    if (tickGet() - si > 30)
                    {
                        mode = false;
                        si = 0;
                    } else
                    {
                        mode = true;
                        si = 0;
                    }
                }
            }
        }
        #endregion decideMode

        #region balanceGravity
        void balanceGravity()
        {
            bool[] needRYP = new bool[] { false, false, false };
            if (mainShipCtrl == null) return;
            if (pGravity.Length() < 0.01) return;
            double ma = shipMaxForce / shipMass;
            double sideALimit = Math.Sqrt(ma * ma - pGravity.Length() * pGravity.Length());
            if (needBalance)
            {
                Vector3D graNoFB = Vector3D.Reject(pGravity, new Vector3D(0, 0, 1));

                Vector3D sv = shipVelLocalGet();
                double nv = sv.X * -0.5;
                nv = utilMyClamp(nv, sideALimit);

                double lrAngle = Math.Atan2(-graNoFB.Y, -graNoFB.X + nv) - Math.PI * 0.5;
                lrAngle = -lrAngle * 1 ;

                SetGyroRoll(lrAngle * -0.15);
                needRYP[0] = true;
            }

            if (autoDown)
            {
                Vector3D graNoLR = Vector3D.Reject(pGravity, new Vector3D(1, 0, 0));
                Vector3D sv = shipVelLocalGet();
                double nv = sv.Z * -0.5;
                nv = utilMyClamp(nv, sideALimit);
                double fbAngle = Math.Atan2(-graNoLR.Y, -graNoLR.Z + nv) - Math.PI * 0.5;
                fbAngle = -fbAngle * 1 ;
                debug("fb: " + fbAngle);
                
                SetGyroPitch(fbAngle * 0.15);
                needRYP[2] = true;
            }

            // follow mode
            if (autoFollow) {
                // TODO yaw
                // TODO pitch
                // TODO roll
            }

            if (!needRYP[0]) SetGyroRoll(angleInput.Z * 0.1);
            if (!needRYP[1]) SetGyroYaw(angleInput.Y * 0.1);
            if (!needRYP[2]) SetGyroPitch(angleInput.X * -0.1);
            if (needRYP.Any(b => b)) SetGyroOverride(true);
            else SetGyroOverride(false);
        }

        void SetGyroOverride(bool bOverride)
        {
            foreach (IMyGyro g in shipGyros)
            {
                if (g.GyroOverride != bOverride)
                {
                    g.ApplyAction("Override");
                }
            }
        }

        double utilMyClamp(double inp, double li)
        {
            if (inp < -li) return -li;
            if (inp > li) return li;
            return inp;
        }

        #endregion balanceGravity

        #region controlThrust
        void controlThrust()
        {
            if (autoDown)
            {
                double vy = shipVelLocalGet().Y;
                double vd = -1.5 - vy;

                double ga = pGravity.Y;
                double na = vd * 10 - ga;

                double nf = shipMass * na;

                double per = nf / shipMaxForce;
                debug("per: " + per);

                foreach (IMyThrust t in shipThrusts[0][T_UP])
                {
                    t.ThrustOverridePercentage = (float)per;
                }
            } else
            {
                foreach (IMyThrust t in shipThrusts[0][T_UP])
                {
                    t.ThrustOverridePercentage = 0;
                }
            }
        }
        #endregion controlThrust

        #region parseradar
        string sonCode = "RADAR";
        Dictionary<long, Vector3D> avoidMap = new Dictionary<long, Vector3D>();
        Dictionary<long, long> avoidLifeTimeMap = new Dictionary<long, long>();
        bool isStandBy = true;
        Vector3D motherPosition;
        Vector3D motherVelocity;
        MatrixD motherMatrixD;
        long lastMotherSignalTime = 0;
        void setDampenersOverride(IMyTerminalBlock controller, bool onOff)
        {
            bool nowOnOff = controller.GetValue<bool>("DampenersOverride");
            if (nowOnOff != onOff)
            {
                PlayAction(controller, "DampenersOverride");
            }
        }
        static void PlayAction(IMyTerminalBlock block, String action)
        {
            if (block != null)
            {
                var a = block.GetActionWithName(action);
                if (a != null) a.Apply(block);
            }
        }
        static void PlayActionList(List<IMyTerminalBlock> blocks, String action)
        {
            if (blocks == null) return;
            for (int i = 0; i < blocks.Count; i++)
            {
                blocks[i].GetActionWithName(action).Apply(blocks[i]);
            }
        }

        string display3D(Vector3D v)
        {
            return string.Join(",", new List<double>() { v.X, v.Y, v.Z }.Select(d => Math.Round(d, 2) + ""));
        }

        bool cmdFollow;
        bool cmdDock;
        void parseRadar(string arguments)
        {
            debug("standby: " + isStandBy);
            debug("mother: " + display3D(motherPosition));
            if (arguments == null) return;
            String[] kv = arguments.Split(':');
            String[] args;

            if (kv[0].Equals(sonCode + "-AVOID"))
            {
                args = kv[1].Split(',');
                avoidMap[Convert.ToInt64(args[0])] = new Vector3D(Convert.ToDouble(args[1]), Convert.ToDouble(args[2]), Convert.ToDouble(args[3]));
                avoidLifeTimeMap[Convert.ToInt64(args[0])] = tickGet();
            }

            foreach (var item in avoidLifeTimeMap.ToList())
            {
                if (tickGet() > item.Value + 120)
                {
                    avoidMap.Remove(item.Key);
                    avoidLifeTimeMap.Remove(item.Key);
                }
            }
            if (!kv[0].Equals(sonCode)) return;

            args = kv[1].Split(',');
            switch (args[0])
            {
                case "STANDBYON":
                    isStandBy = true;
                    setDampenersOverride(mainShipCtrl, false);
                    shipThrusts[0][T_UP].ForEach(t => t.Enabled = false);
                    shipThrusts[0][T_LEFT].ForEach(t => t.Enabled = false);
                    shipThrusts[0][T_RIGHT].ForEach(t => t.Enabled = false);
                    SetGyroOverride(false);
                    break;
                case "STANDBYOFF":
                    isStandBy = false;
                    autoDown = false;
                    setDampenersOverride(mainShipCtrl, true);
                    shipThrusts[0].ForEach(l => l.ForEach(t => t.Enabled = true));
                    break;
            }
            if (isStandBy) return;


            switch (args[0])
            {
                case "FLYBYON":
                    if (motherPosition == Vector3D.Zero) break;
                    cmdFollow = true;
                    break;
                // case "DOCKINGON":
                //     if (motherPosition == Vector3D.Zero) break;
                //     commandCache = "DOCKINGON";
                //     commandStart = t;
                //     break;
                // case ("LOADMISSILEON"):
                //     //TODO
                //     break;
                // case "ATTACKON":
                //     if (flyByOn)
                //     {
                //         attackMode = true;
                //     }
                //     break;
                // case "ATTACKOFF":
                //     if (flyByOn)
                //     {
                //         attackMode = false;
                //     }
                //     break;
                // case "WEAPON1":
                //     callComputer(fighterFcs, "WEAPON1");
                //     break;
                // case "WEAPON2":
                //     callComputer(fighterFcs, "WEAPON2");
                //     break;
                // case "VFTUP":
                //     VFTransformNew(true);
                //     break;
                // case "VFTDOWN":
                //     VFTransformNew(false);
                //     break;
                // case "DKMVF7":
                //     dockMove(18);
                //     break;
                // case "DKMVF6":
                //     dockMove(16.25);
                //     break;
                // case "DKMVF5":
                //     dockMove(12.5);
                //     break;
                // case "DKMVB5":
                //     dockMove(-12.5);
                //     break;
                default:
                    break;
            }
            if (args.Count() < 19) return;
            lastMotherSignalTime = tickGet();
            motherMatrixD = new MatrixD(Convert.ToDouble(args[0]), Convert.ToDouble(args[1]), Convert.ToDouble(args[2]), Convert.ToDouble(args[3]),
            Convert.ToDouble(args[4]), Convert.ToDouble(args[5]), Convert.ToDouble(args[6]), Convert.ToDouble(args[7]),
            Convert.ToDouble(args[8]), Convert.ToDouble(args[9]), Convert.ToDouble(args[10]), Convert.ToDouble(args[11]),
            Convert.ToDouble(args[12]), Convert.ToDouble(args[13]), Convert.ToDouble(args[14]), Convert.ToDouble(args[15]));

            motherPosition = new Vector3D(motherMatrixD.M41, motherMatrixD.M42, motherMatrixD.M43);

            MatrixD motherLookAtMatrix = MatrixD.CreateLookAt(new Vector3D(0, 0, 0), motherMatrixD.Forward, motherMatrixD.Up);

            motherVelocity = new Vector3D(Convert.ToDouble(args[16]), Convert.ToDouble(args[17]), Convert.ToDouble(args[18]));

            // TODO fly by aim

            /*
            needFlyByAim = false;
            if (susMode)
            {
                flyByAimPosition = estShipPosition();
                flyByAimSpeed = Vector3D.Zero;
                needFlyByAim = true;
            }

            if (!flyByOn) return;

            if (args.Count() >= 25)
            {
                flyByAimPosition = new Vector3D(Convert.ToDouble(args[19]), Convert.ToDouble(args[20]), Convert.ToDouble(args[21]));
                flyByAimSpeed = new Vector3D(Convert.ToDouble(args[22]), Convert.ToDouble(args[23]), Convert.ToDouble(args[24]));
                needFlyByAim = true;
                callComputer(fighterFcs, "FLYBYAIM:" + flyByAimPosition.X + "," + flyByAimPosition.Y + "," + flyByAimPosition.Z);

                if (args.Count() >= 26)
                {
                    Vector3D dir = flyByAimPosition - estShipPosition();
                    dir = Vector3D.Normalize(dir);
                    if (!isBig)
                    {
                        double standardAttackAngle = Convert.ToDouble(args[25]);
                        MatrixD aimMatrix;
                        if (naturalGravityLength > 0.01f)
                        {
                            dir = naturalGravity;
                            aimMatrix = MatrixD.CreateFromDir(Vector3D.Normalize(naturalGravity), shipMatrix.Forward);
                        }
                        else
                        {
                            aimMatrix = MatrixD.CreateFromDir(dir, shipMatrix.Up);
                        }

                        var angle = standardAttackAngle + (commandWaitTic * 1d / commandAllTic) * MathHelper.TwoPi;
                        Vector3D upBaseAim = new Vector3D(Math.Cos(angle), Math.Sin(angle), 0);
                        Vector3D up = Vector3D.TransformNormal(upBaseAim, aimMatrix);
                        var ad = t % 600 / 600f * adm;
                        flyByAttackPosition = flyByAimPosition + 800 * up - (droneAttackRange + ad) * dir;
                        var tp2m = flyByAimPosition - MePosition;
                        var tp2mn = Vector3D.Normalize(tp2m);
                        var fp2m = flyByAttackPosition - MePosition;
                        var fp2ml = fp2m.Dot(tp2mn);
                        if (fp2ml > tp2m.Length())
                        {
                            var nap2m = tp2m * (tp2m.Length() - 800) / tp2m.Length();
                            flyByAttackPosition = MePosition + nap2m;
                        }
                    }
                    else
                    {
                        Vector3D tmp = Vector3D.Reject(dir, shipMatrix.Up);
                        if (tmp.Equals(Vector3D.Zero))
                        {
                            tmp = shipMatrix.Forward;
                        }
                        else
                        {
                            tmp = Vector3D.Normalize(tmp);
                        }
                        MatrixD rd = MatrixD.CreateFromDir(tmp, shipMatrix.Up);

                        Vector3D off;

                        switch (flyByOffsetDirection)
                        {
                            case "LEFT":
                                off = rd.Left;
                                break;
                            case "RIGHT":
                                off = rd.Right;
                                break;
                            default:
                                off = rd.Up;
                                break;
                        }

                        flyByAttackPosition = flyByAimPosition + 1500 * off - 100 * dir;

                    }
                }

            }
            if (needFlyByAim == false && radarHighThreatPosition != Vector3D.Zero)
            {
                flyByAimPosition = radarHighThreatPosition;
                needFlyByAim = true;
            }
            */
        }

        #endregion parserader

        #region calcFollowNA
        void calcFollowNA() {
            Vector3D pd = motherPosition - shipPosition;
            Vector3D nv = motherVelocity + pd * 0.1;
            Vector3D naLocal = (Vector3D.TransformNormal(nv, shipRevertMat) - shipVelLocalGet()) * 0.1;
            double ma = shipMaxForce / shipMass;
            double sideALimit = Math.Sqrt(ma * ma - pGravity.Length() * pGravity.Length()) * 0.5;
            if (naLocal.Length() > sideALimit) naLocal *= sideALimit/naLocal.Length();
            naLocal -= pGravity;

            // avoid TODO

        }
        #endregion calcFollowNA

        #endregion ingamescript

    }
}
