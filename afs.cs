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
using VRage.Game.ModAPI.Ingame.Utilities;

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

      // parse command
      parseRadar(arguments);

      if ((updateType & UpdateType.Update1) == 0) {
        return;
      }
      debugClear();
      tickPlus();
      if (autoFollow && tickGet() % 15 != 0) return;

      // check ship
      checkShip();
      if (shipThrusts.Count == 0) return;

      // parse input
      parseInput();
      debug("vr: " + vtRotors.Count + " " + shipThrusts[1][0].Count);

      // decide mode
      decideMode();
      debug("ab: " + autoBalance + " ad: " + autoDown + " acc: " + isAcc + " af: " + (autoFollow ? Math.Round((motherPositionGet() + Vector3D.TransformNormal(followGetFP(), motherMatrixD) - shipPosition).Length(),2)+"" : "False") + " do: " + fpIdx);

      if (!isStandBy && !docked)
      {
        // follow position
        followPosition();

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
    string[] runIndiStr = new string[] { "|", "/", "-", "\\" };
    void debugShow()
    {
      Echo("Kaien Automatic Fly System V0.1 " + runIndiStr[tickGet() / 10 % 4] + "\n");
      Echo(debugInfo);
    }
    double[] minmax = new double[100];
    int minmaxIdx = 0;
    double[] reMinMax(double i) {
      minmax[minmaxIdx] = Math.Round(i, 2);
      minmaxIdx = (minmaxIdx + 1) % 100;
      return new double[] {minmax.Min(), minmax.Max()};
    }
    string dcString;
    void debugCondition(string info, bool condi) {
      if (condi) dcString = info + "\n";
      if(tickGet() % 600 == 0) dcString = "";
      debugInfo += dcString;
    }

    #endregion debug

    #region checkship
    IMyShipController mainShipCtrl = null;
    Vector3D shipVel = Vector3D.Zero;
    Vector3D shipVelLocal = Vector3D.Zero;
    Vector3D shipPosition = Vector3D.Zero;
    MatrixD shipMatrix;
    MatrixD shipRevertMat;
    List<List<List<IMyThrust>>> shipThrusts = new List<List<List<IMyThrust>>>();
    List<IMyGyro> shipGyros = new List<IMyGyro>();
    List<IMyShipConnector> shipConns = new List<IMyShipConnector>();
    List<List<string>> gyroFields = new List<List<string>>();
    List<List<int>> gyroFactors = new List<List<int>>();
    const int G_YAW = 0;
    const int G_PITCH = 1;
    const int G_ROLL = 2;
    Vector3D pGravity = Vector3D.Zero;
    Vector3D pGravityLocal = Vector3D.Zero;
    double shipMaxForce = 0;
    double shipMass = 0;
    void checkShip()
    {
      getBlocks();
      if (shipThrusts.Count == 0) return;

      if (mainShipCtrl == null) return;

      shipVel = mainShipCtrl.GetShipVelocities().LinearVelocity;
      debug("shipv: " + shipVel.Length());
      shipRevertMat = MatrixD.CreateLookAt(new Vector3D(), mainShipCtrl.WorldMatrix.Forward, mainShipCtrl.WorldMatrix.Up);
      shipVelLocal = Vector3D.TransformNormal(shipVel, shipRevertMat);
      shipPosition = mainShipCtrl.GetPosition();
      shipMatrix = mainShipCtrl.WorldMatrix;

      pGravity = mainShipCtrl.GetNaturalGravity();
      pGravityLocal = Vector3D.TransformNormal(pGravity, shipRevertMat);
      shipMaxForce = 0;
      foreach (IMyThrust t in shipThrusts[0][T_UP])
      {
        shipMaxForce += t.MaxEffectiveThrust;
      }
      foreach (IMyThrust t in shipThrusts[1][0])
      {
        shipMaxForce += t.MaxEffectiveThrust;
      }
      shipMass = mainShipCtrl.CalculateShipMass().PhysicalMass;

      long? foundId = 0;
      if (aiOffensive != null) {
        foundId = aiOffensive.SearchEnemyComponent.FoundEnemyId;
      }
      debug("found enemy: " + foundId);
      MyDetectedEntityInfo te;
      if (turret != null) {
        te = turret.GetTargetedEntity();
        debug("te: " + te.EntityId);
      }
    }
    double shipMaxForceGet() {
      return shipMaxForce;
    }

    Vector3D shipVelLocalGet()
    {
      return shipVelLocal;
    }
    Vector3D shipVelGet()
    {
      return shipVel;
    }
    MyIni cfg;
    const string CFG_GENERAL = "AFS - General";
    IMyOffensiveCombatBlock aiOffensive; // TODO how to get hitpoint?
    IMyTurretControlBlock turret;
    void getBlocks()
    {
      List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
      if (mainShipCtrl == null)
      {
        GridTerminalSystem.GetBlocksOfType<IMyShipController>(blocks, b => b.CubeGrid == Me.CubeGrid);
        mainShipCtrl = (IMyShipController)matchNameOrFirst(blocks, "main");
        cfg = new MyIni();
        cfg.TryParse(Me.CustomData);
      }
      if (mainShipCtrl == null) return;

      bool cleanAll = tickGet() % 300 == 0;

      if (cleanAll)
      {
        shipThrusts.Clear();
        shipGyros.Clear();
        gyroFields.Clear();
        gyroFactors.Clear();
        vtRotors.Clear();
        shipWeapons.Clear();
      }
      
      if (shipGyros.Count == 0) getGyros();
      if (shipConns.Count == 0) getConns();

      bool connected = shipConns.Any(c => c.Status.ToString().Equals("Connected"));
      
      if (shipThrusts.Count == 0 && !connected) getThrusts();

      if (cleanAll && !connected) {
        GridTerminalSystem.GetBlocksOfType<IMyUserControllableGun>(shipWeapons);
      }

      if (cleanAll) { 
        List<IMyOffensiveCombatBlock> ofBlocks = new List<IMyOffensiveCombatBlock>();
        GridTerminalSystem.GetBlocksOfType<IMyOffensiveCombatBlock>(ofBlocks, b => b.CubeGrid == Me.CubeGrid);
        if(ofBlocks.Count > 0) {
          aiOffensive = ofBlocks[0];
        }
      }

      if (cleanAll && !connected) {
        List<IMyTurretControlBlock> tuBlocks = new List<IMyTurretControlBlock>();
        GridTerminalSystem.GetBlocksOfType<IMyTurretControlBlock>(tuBlocks);
        if (tuBlocks.Count > 0) {
          turret = tuBlocks[0];
        }
      }
    }
    void getGyros()
    {
      List<IMyGyro> blocks = new List<IMyGyro>();
      GridTerminalSystem.GetBlocksOfType<IMyGyro>(blocks, b => b.CubeGrid == Me.CubeGrid);
      foreach (IMyGyro g in blocks)
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
    void getConns() {
      GridTerminalSystem.GetBlocksOfType<IMyShipConnector>(shipConns, b => b.CubeGrid == Me.CubeGrid);
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
    string VT_TAG = "[VT]";
    class VTRotor {
      public IMyMotorStator rotor;
      public bool isP;
      public float ps,pe,ns,ne;
    }
    List<VTRotor> vtRotors = new List<VTRotor>();
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

      List<List<IMyThrust>> l1Thrusts = new List<List<IMyThrust>>();
      shipThrusts.Add(l1Thrusts);
      GridTerminalSystem.GetBlocksOfType<IMyThrust>(blocks, b => b.CubeGrid != Me.CubeGrid && ((IMyTerminalBlock)b).CustomName.Contains(VT_TAG));
      l1Thrusts.Add(blocks);
      List<IMyMotorStator> rotors = new List<IMyMotorStator>();
      GridTerminalSystem.GetBlocksOfType<IMyMotorStator>(rotors, b => b.CubeGrid == Me.CubeGrid && ((IMyTerminalBlock)b).CustomName.Contains(VT_TAG));
      foreach( var r in rotors) {
        VTRotor vr = new VTRotor();
        vr.rotor = r;
        MyIni vi = new MyIni();
        vi.TryParse(((IMyTerminalBlock)r).CustomData);
        String clockWise = vi.Get("VTRotor", "ClockWise").ToString();
        if (clockWise == "") {
          clockWise = "T";
          vi.Set("VTRotor", "ClockWise", clockWise);
          ((IMyTerminalBlock)r).CustomData = vi.ToString();
        }
        String pspensne = vi.Get("VTRotor", "PsPeNsNe").ToString();
        if (pspensne == "") {
          pspensne = "0,90,0,-90";
          vi.Set("VTRotor", "PsPeNsNe", pspensne);
          ((IMyTerminalBlock)r).CustomData = vi.ToString();
        }
        vr.isP = clockWise.Equals("T");
        String[] pa = pspensne.Split(',');
        float.TryParse(pa[0], out vr.ps);
        float.TryParse(pa[1], out vr.pe);
        float.TryParse(pa[2], out vr.ns);
        float.TryParse(pa[3], out vr.ne);
        vtRotors.Add(vr);
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
    Vector3D moveInputDam = Vector3D.Zero;
    bool shipDam = true;
    void parseInput()
    {
      if (mainShipCtrl == null) return;
      angleInput = new Vector3D(mainShipCtrl.RotationIndicator.X, mainShipCtrl.RotationIndicator.Y, mainShipCtrl.RollIndicator);
      moveInput = mainShipCtrl.MoveIndicator;
      moveInputDam = moveInput;
      var sv = shipVelLocalGet();
      sv.Z = 0;
      moveInputDam += sv * -0.1;
    }
    #endregion parseInput

    #region decideMode
    long spaceStart = 0;
    bool autoBalance = false;
    long downStart = 0;
    bool autoDown = false;
    bool autoFollow = false;
    bool isDocking = false;
    bool needBalance = false;
    bool docked = false;
    bool isAcc = false;
    long backStart = 0;
    void decideMode()
    {
      bool lastAcc = isAcc;
      shortClick(ref backStart, moveInput.Z, true, 0.5, 0.1, ref isAcc);
      if (lastAcc && !isAcc) {
        shipThrusts[0][T_FRONT].ForEach(t => {t.Enabled = true; t.ThrustOverridePercentage = 0;});
        shipThrusts[0][T_BACK].ForEach(t => {t.Enabled = true; t.ThrustOverridePercentage = 0;});
      }

      bool turnOn = shortClick(ref spaceStart, moveInput.Y, true, 0.5, 0.1, ref autoBalance);
      if (turnOn) {
        shipThrusts[0][T_FRONT].ForEach(t => {t.Enabled = true; t.ThrustOverridePercentage = 0;});
        shipThrusts[0][T_BACK].ForEach(t => {t.Enabled = true; t.ThrustOverridePercentage = 0;});
      }

      if (moveInput.Y < -0.5) autoBalance = false;
      if (Math.Abs(angleInput.Z) > 0.5) autoBalance = false;

      if(shipDam && !isAcc)
        shortClick(ref downStart, moveInput.Y, false, -0.5, -0.1, ref autoDown);
      else autoDown = false;
      
      if (moveInput.Y > 0.5) autoDown = false;
      if (Math.Abs(angleInput.Z) > 0.5) autoDown = false;

      // follow mode
      if (cmdFollow)
      {
        autoBalance = false;
        autoDown = false;
        autoFollow = true;
        cmdFollow = false;
        isDocking = false;
        if(fpIdx == fpList.Count - 1) {
          if ((shipPosition - motherPosition).Length() > fpList[0].Length())
            fpIdx = 0;
          else
            fpIdx --;
        }
        shipConns.ForEach(c => c.Disconnect());
        docked = false;
        setDampenersOverride(mainShipCtrl, false);
      }

      if (cmdDock) {
        if(fpIdx == 0) fpIdx ++;
        autoBalance = false;
        autoDown = false;
        autoFollow = true;
        cmdDock = false;
        isDocking = true;
        setDampenersOverride(mainShipCtrl, false);
      }

      if (cmdControl)
      {
        autoFollow = false;
        autoDown = false;
        cmdControl = false;
        isAcc = false;
        foreach (IMyThrust t in shipThrusts[0][T_FRONT])
        {
          t.ThrustOverridePercentage = 0;
          t.Enabled = true;
        }
        foreach (IMyThrust t in shipThrusts[0][T_UP])
        {
          t.ThrustOverridePercentage = 0;
          t.Enabled = true;
        }
        setDampenersOverride(mainShipCtrl, true);
      }
      needBalance = autoBalance || autoDown;

      if (isDocking && ! docked) {
        if (shipConns.Any(c => c.Status.ToString().Equals("Connectable"))) {
          shipConns.ForEach(c => c.Connect());
          shipThrusts.ForEach(tl => tl.ForEach(tll => tll.ForEach(t => t.ThrustOverridePercentage = 0)));
          shipGyros.ForEach(g => g.GyroOverride = false);
          setDampenersOverride(mainShipCtrl, false);
          docked = true;
        }
      }
    }
    bool shortClick(ref long si, double inp, bool isP, double tl, double dl, ref bool mode)
    {
      if (si == 0)
      {
        if ((isP && inp > tl) || (!isP && inp < tl))
        {
          si = tickGet();
          mode = false;
        }
      }
      else
      {
        if ((isP && inp < dl) || (!isP && inp > dl))
        {
          if (tickGet() - si > 30)
          {
            mode = false;
            si = 0;
          }
          else
          {
            mode = true;
            si = 0;
            return true;
          }
        }
      }
      return false;
    }
    
    #endregion decideMode

    #region balanceGravity
    double WEAPON_MAX_RANGE = 800;
    double axisYOffset = 0.5;
    double axisBs = 350;
    double axisGr = 0.75;
    double axisBr = 800;
    double axisCr = -0.13;
/*     MAIN_CANNON_BS=350
    MAIN_CANNON_BR=800
    MAIN_CANNON_GR=0.75
    MAIN_CANNON_YOFF=0
    MAIN_CANNON_CR=-0.1 */
    List<IMyUserControllableGun> shipWeapons = new List<IMyUserControllableGun>();
    // IMySmallGatlingGun    IMySmallMissileLauncher
    double GYRO_RATE = 1;
    double AIM_LIMIT = 0.999;
    void balanceGravity()
    {
      if (vtRotors.Count > 0) {
        // vtRotors
        float fbAngle = 0;
        if (pGravity.Length() > 0.01) {
          Vector3D graNoLR = Vector3D.Reject(pGravityLocal, new Vector3D(1, 0, 0));
          fbAngle = (float)(Math.Atan2(-graNoLR.Y, -graNoLR.Z) - Math.PI * 0.5);
        }
        foreach( var vr in vtRotors) {
          float v = vr.isP ? fbAngle + vr.rotor.Angle : fbAngle - vr.rotor.Angle;
          if (vr.isP) v = -v;
          vr.rotor.SetValueFloat("Velocity", v * 10f);
        }
      }

      bool[] needRYP = new bool[] { false, false, false };
      if (mainShipCtrl == null) return;
      if (pGravity.Length() < 0.01) return;
      double ma = shipMaxForceGet() / shipMass;
      double sideALimit = Math.Sqrt(ma * ma - pGravity.Length() * pGravity.Length()) * 0.8;

      bool haveTarget = false;
      if (tickGet() - mainTarget.lastTime < 120 ) {
        Vector3D tp = mainTarget.estPosition(tickGet());
        if ((shipPosition - tp).Length() < WEAPON_MAX_RANGE) {
          haveTarget = true;
        }
      }
      if (tickGet() - suspendStart < 300) haveTarget = false;
      String aostr = cfg.Get(CFG_GENERAL, "AIM_OFFSET").ToString();
      if (aostr == "") {
        aostr = "0.5";
        cfg.Set(CFG_GENERAL, "AIM_OFFSET", aostr);
        Me.CustomData = cfg.ToString();
      }
      double.TryParse(aostr, out axisYOffset);
      String alstr = cfg.Get(CFG_GENERAL, "AIM_LIMIT").ToString();
      if (alstr == "") {
        alstr = "0.999";
        cfg.Set(CFG_GENERAL, "AIM_LIMIT", alstr);
        Me.CustomData = cfg.ToString();
      }
      double.TryParse(alstr, out AIM_LIMIT);
      if (haveTarget) {
        // aim 
        Vector3D HitPoint = HitPointCaculate(shipPosition, shipVelGet(), Vector3D.Zero, mainTarget.estPosition(tickGet()) + shipMatrix.Up * axisYOffset, mainTarget.velocity, Vector3D.Zero, axisBs, 0, axisBs, (float)axisGr, pGravity, axisBr, axisCr);
        Vector3D tarN = Vector3D.Normalize(HitPoint - shipPosition);
		    tarN = Vector3D.Transform(tarN, shipRevertMat);
        SetGyroYaw( (Math.Atan2(tarN.Z, tarN.X) + Math.PI * 0.5) * 0.6 * GYRO_RATE);
        needRYP[1] = true;
        SetGyroPitch((Math.Atan2(tarN.Z, tarN.Y) + Math.PI * 0.5) * 0.6 * GYRO_RATE);
        needRYP[2] = true;
        
        // fire
        if (tarN.Z < -AIM_LIMIT) shipWeapons.ForEach(w => w.ShootOnce());
      }

      if (needBalance)
      {
        Vector3D graNoFB = Vector3D.Reject(pGravityLocal, new Vector3D(0, 0, 1));

        Vector3D sv = shipVelLocalGet();
        double nv = sv.X * -0.5;
        nv = utilMyClamp(nv, sideALimit);

        double lrAngle = Math.Atan2(-graNoFB.Y, -graNoFB.X + nv) - Math.PI * 0.5;
        lrAngle = -lrAngle * 1;
        double cRoll = Math.Atan2(pGravityLocal.Y, pGravityLocal.X) + Math.PI * 0.5;

        SetGyroRoll((lrAngle - cRoll) * -0.15);
        needRYP[0] = true;
      }

      if (autoDown && !haveTarget)
      {
        Vector3D graNoLR = Vector3D.Reject(pGravityLocal, new Vector3D(1, 0, 0));
        Vector3D sv = shipVelLocalGet();
        if (nabfHave[0] && sv.Z > 0) {
          sv.Z = 0;
        }
        if (nabfHave[1] && sv.Z < 0) {
          sv.Z = 0;
        }
        double nv = sv.Z * -0.5;
        nv = utilMyClamp(nv, sideALimit);
        double fbAngle = Math.Atan2(-graNoLR.Y, -graNoLR.Z + nv) - Math.PI * 0.5;
        fbAngle = -fbAngle * 1;
        double cPitch = Math.Atan2(pGravityLocal.Y, pGravityLocal.Z) + Math.PI * 0.5;

        SetGyroPitch((fbAngle - cPitch) * 0.15);
        needRYP[2] = true;
      }

      // follow mode
      if (autoFollow)
      {
        double nv = naL1MainLocal.Z * -0.5;
        if (!haveTarget) {
          // yaw
          var motherPoint = Vector3D.TransformNormal(followGetForward(), shipRevertMat);
          var angle = Math.Atan2(motherPoint.Z, motherPoint.X);
          angle = Math.PI * 0.5 + angle;
          angle = utilMyClamp(angle, 0.2);
          SetGyroYaw(angle * 0.2);
          needRYP[1] = true;

          // pitch
          Vector3D graNoLR = Vector3D.Reject(pGravityLocal, new Vector3D(1, 0, 0));

          double fbAngle = Math.Atan2(-graNoLR.Y, -graNoLR.Z + nv) - Math.PI * 0.5;
          fbAngle = fbAngle * 1;
          double cPitch = Math.Atan2(pGravityLocal.Y, pGravityLocal.Z) + Math.PI * 0.5;
          SetGyroPitch((fbAngle - cPitch) * 0.3);
          needRYP[2] = true;
        }

        // roll
        Vector3D graNoFB = Vector3D.Reject(pGravityLocal, new Vector3D(0, 0, 1));
        nv = naL1MainLocal.X * -0.5;
        nv = utilMyClamp(nv, sideALimit);

        double lrAngle = Math.Atan2(-graNoFB.Y, -graNoFB.X + nv) - Math.PI * 0.5;
        lrAngle = lrAngle * 1;
        double cRoll = Math.Atan2(pGravityLocal.Y, pGravityLocal.X) + Math.PI * 0.5;

        SetGyroRoll((lrAngle - cRoll) * -0.3);
        needRYP[0] = true;
      }

      if (!needRYP[0]) SetGyroRoll(angleInput.Z * -0.06 * GYRO_RATE);
      if (!needRYP[1]) SetGyroYaw(angleInput.Y * 0.03 * GYRO_RATE);
      if (!needRYP[2]) SetGyroPitch(angleInput.X * -0.03 * GYRO_RATE);
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
      Vector3D l2Provide = Vector3D.Zero;
      if (shipThrusts[1][0].Count > 0 && pGravity.Length() > 0.01) {
        var pgn = Vector3D.Normalize(pGravity);
        double mf = 0;
        foreach(var t in shipThrusts[1][0]) {
          mf += t.MaxEffectiveThrust * Vector3D.Dot(t.WorldMatrix.Forward, pgn);
        }
        float per = 0;
        Vector3D pNoLR = -Vector3D.Reject(pGravityLocal, new Vector3D(1,0,0));
        Vector3D pgln = Vector3D.Normalize(pNoLR);
        Vector3D mi = pgln * Vector3D.Dot(moveInputDam, pgln);
        if (Math.Abs(moveInputDam.Y) > -.7)
          pNoLR += mi * 5.0;
        double needF = 0;
        if (autoDown) {
          var vd = -1.5 - shipVelLocalGet().Y;
          var naDown = vd * 0.8;
          pNoLR.Y += naDown;
        }
        if (autoFollow) {
          double dot = 0;
          if (naL1MainLocal.Length() > 0.01)
            dot = Vector3D.Dot(Vector3D.Normalize(naL1MainLocal), pgln);
          pNoLR = naL1MainLocal / dot;
        }

        if (pNoLR.Y > 0) needF = shipMass * pNoLR.Length();
        if (mf > 0) {
          per = (float)(needF / mf);
        }
        if (per > 1) per = 1;
        debug("l2per: " + per);
        foreach(var t in shipThrusts[1][0]) {
          t.Enabled = per > 0;
          t.ThrustOverridePercentage = per;
          l2Provide += (t.MaxEffectiveThrust * per) * t.WorldMatrix.Backward;
        }
      }
      if (autoDown)
      {
        double vy = shipVelLocalGet().Y;
        double vd = -1.5 - vy;

        double ga = pGravityLocal.Y;
        double na = vd * 10 - ga;

        double nf = shipMass * na;

        double per = 0;
        if (shipMaxForceGet() > 0)
          per = nf / shipMaxForceGet();

        foreach (IMyThrust t in shipThrusts[0][T_UP])
        {
          t.ThrustOverridePercentage = (float)per;
        }
        
      }
      else if (autoFollow)
      {
        double dot = 0;
        if (naL1MainLocal.Length() > 0.01)
          dot = Vector3D.Dot(Vector3D.Normalize(naL1MainLocal), new Vector3D(0, 1, 0));
        var nf = shipMass * naL1MainLocal.Length();
        double per = 0;
        if (shipMaxForceGet() > 0) per = nf / shipMaxForceGet();
        per *= dot;
        var mmm = reMinMax(shipMaxForceGet());
        debug("shipMaxForce: " + mmm[0] + " " + mmm[1]);
        foreach (IMyThrust t in shipThrusts[0][T_UP])
        {
          t.ThrustOverridePercentage = (float)per;
        }
      }
      else if (isAcc) {
        if (mainShipCtrl.DampenersOverride) {
          var accMove = moveInput;
          accMove -= shipVelLocalGet() * 0.1;
          bool isp = accMove.Z > 0;
          double mf = 0;
          foreach(var t in shipThrusts[0][isp ? T_BACK : T_FRONT]) {
            mf += t.MaxEffectiveThrust;
          }
          var nf = shipMass * accMove.Z * (isp ? 1 : -1);
          float per = 0;
          if(mf > 0) {
            per = (float)(nf / mf);
          }
          foreach(var t in shipThrusts[0][isp ? T_BACK : T_FRONT]) {
            if(per == 0) t.Enabled = false;
            else t.Enabled = true;
            t.ThrustOverridePercentage = per;
          }
          foreach(var t in shipThrusts[0][isp ? T_FRONT : T_BACK]) {
            t.Enabled = false;
            t.ThrustOverridePercentage = 0;
          }
        } else {
          shipThrusts[0][T_FRONT].ForEach(t => t.ThrustOverridePercentage = 0);
          shipThrusts[0][T_BACK].ForEach(t => t.ThrustOverridePercentage = 0);
        }
      }
      else
      {
        foreach (IMyThrust t in shipThrusts[0][T_UP])
        {
          t.ThrustOverridePercentage = 0;
        }
        shipThrusts[0][T_FRONT].ForEach(t => t.ThrustOverridePercentage = 0);
        if (moveInput.Z <= 0 && !isAcc) {
          shipThrusts[0][T_BACK].ForEach(t => {t.Enabled = false;t.ThrustOverridePercentage = 0;});
        } else {
          shipThrusts[0][T_BACK].ForEach(t => {t.Enabled = true;t.ThrustOverridePercentage = 0;});
        }
      }

      if (autoFollow || autoDown) {
        bool needOpZ = Math.Abs(moveInput.Z) > 0.01;
        double maxBack = 0;
        int tidx = T_FRONT;
        int tOther = T_BACK;
        Vector3D vidx = new Vector3D(0, 0, -1);
        if (naL1BackLocal.Z > 0) {
          tidx = T_BACK;
          tOther = T_FRONT;
          vidx = new Vector3D(0, 0, 1);
        }
        foreach (IMyThrust t in shipThrusts[0][tidx])
        {
          maxBack += t.MaxEffectiveThrust;
        }
        var nf = shipMass * naL1BackLocal.Length();
        double dot = 0;
        if (naL1BackLocal.Length() > 0.01)
          dot = Vector3D.Dot(Vector3D.Normalize(naL1BackLocal), vidx);
        double per = 0;
        if (maxBack > 0) per = nf / maxBack;
        per *= dot;
        bool needOpTidx = needOpZ && ((tidx == T_FRONT && moveInput.Z < 0) || (tidx == T_BACK && moveInput.Z > 0));
        bool needOpTother = needOpZ && !needOpTidx;
        
        foreach (IMyThrust t in shipThrusts[0][tidx])
        {
          if (needOpTidx) {
            t.Enabled = true;
            t.ThrustOverridePercentage = 0;
            continue;
          }
          if (per == 0)  {
            t.Enabled = false;
          }
          else
          {
            t.Enabled = true;
            t.ThrustOverridePercentage = (float)per;
          }
        }
        foreach (IMyThrust t in shipThrusts[0][tOther])
        {
          if (needOpTother) {
            t.Enabled = true;
            t.ThrustOverridePercentage = 0;
            continue;
          }
          t.Enabled = false;
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
    long motherLastTime = 0;
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
    bool cmdControl;
    long suspendStart = 0;
    class MainTarget {
      public Vector3D position;
      public Vector3D velocity;
      public long lastTime;
      public Vector3D estPosition(long t) { 
        return this.position + (1D/60)*(t-this.lastTime)*this.velocity;
      }
    }
    MainTarget mainTarget = new MainTarget();
    void parseRadar(string arguments)
    {
      debug("standby: " + isStandBy);
      debug("mother: " + display3D(Vector3D.TransformNormal(motherPositionGet() - shipPosition, shipRevertMat)));
      if (arguments == null) return;
      String[] kv = arguments.Split(':');
      if (kv.Length == 1)
      {
        switch (arguments)
        {
          case "CONTROL":
            cmdControl = true;
            break;
          case "SUSPEND":
            suspendStart = tickGet();
            break;
        }
        return;
      }
      String[] args;

      if (kv[0].Equals(sonCode + "-AVOID"))
      {
        args = kv[1].Split(',');
        avoidMap[Convert.ToInt64(args[0])] = new Vector3D(Convert.ToDouble(args[1]), Convert.ToDouble(args[2]), Convert.ToDouble(args[3]));
        avoidLifeTimeMap[Convert.ToInt64(args[0])] = tickGet();
        
      }

      debugCondition("enemy: " + kv[1], kv[0].Equals(sonCode + "-ENEMY"));
      if (kv[0].Equals(sonCode + "-ENEMY"))
      {
        args = kv[1].Split(',');
        mainTarget.position = new Vector3D(Convert.ToDouble(args[0]), Convert.ToDouble(args[1]), Convert.ToDouble(args[2]));
        mainTarget.velocity = new Vector3D(Convert.ToDouble(args[3]), Convert.ToDouble(args[4]), Convert.ToDouble(args[5]));
        mainTarget.lastTime = tickGet();
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
          shipThrusts[1][0].ForEach(t => t.Enabled = false);
          SetGyroOverride(false);
          break;
        case "STANDBYOFF":
          isStandBy = false;
          autoDown = false;
          setDampenersOverride(mainShipCtrl, true);
          shipThrusts[0].ForEach(l => l.ForEach(t => t.Enabled = true));
          shipThrusts[1][0].ForEach(t => t.Enabled = true);
          break;
      }
      if (isStandBy) return;


      switch (args[0])
      {
        case "FLYBYON":
          if (motherPosition == Vector3D.Zero) break;
          cmdFollow = true;
          break;
        case "DOCKINGON":
          if (motherPosition == Vector3D.Zero) break;
          cmdDock = true;
          break;
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
      motherLastTime = tickGet();

      MatrixD motherLookAtMatrix = MatrixD.CreateLookAt(new Vector3D(0, 0, 0), motherMatrixD.Forward, motherMatrixD.Up);

      motherVelocity = new Vector3D(Convert.ToDouble(args[16]), Convert.ToDouble(args[17]), Convert.ToDouble(args[18]));

      
    }

    Vector3D motherPositionGet() {
      return motherPosition + (motherVelocity * (tickGet() - motherLastTime) / 60.0);
    }

    #endregion parserader

    #region calcFollowNA
    Vector3D naL1MainLocal;
    Vector3D naL1BackLocal;
    bool[] nabfHave = new bool[]{false, false};
    void calcFollowNA()
    {
      if (!autoFollow && !autoDown) return;
      Vector3D na = Vector3D.Zero;
      if (autoFollow) {
        Vector3D pd = motherPositionGet() + Vector3D.TransformNormal(followGetFP(), motherMatrixD) - shipPosition;
        if (pd.Length() < 20) pd *= 0.23;
        else pd = Vector3D.Normalize(pd) * Math.Sqrt(pd.Length() - 10) * 1.5;
        Vector3D nv = motherVelocity + pd;
        na = (nv - shipVelGet()) * 0.5;
        double ma = shipMaxForceGet() / shipMass;
        double sideALimit = Math.Sqrt(ma * ma - pGravity.Length() * pGravity.Length()) * 0.4;
        if (na.Length() > sideALimit) na *= sideALimit / na.Length();

        // avoid 
        foreach (var a in avoidMap)
        {
          var tpd = shipPosition - a.Value;
          if (tpd.Length() >= 50) continue;
          var al = MathHelper.Clamp(50 - tpd.Length(), 0, 10) * 2.0;
          var aa = tpd / tpd.Length() * al;
          if(fpIdx == 0)na += aa;
        }

        bool needAvoidMother = false;
        if (isDocking && fpIdx<= fpList.Count - 2) needAvoidMother = true;
        if (!isDocking && fpIdx < fpList.Count - 2) needAvoidMother = true;
        if (needAvoidMother) {
          double range = fpList[fpList.Count - 2].Length();
          range *= 0.8;
          var tpd = shipPosition - motherPosition;
          var tpdn = Vector3D.Normalize(tpd);
          double av = Vector3D.Dot(motherVelocity - shipVelGet(), tpdn);
          if (av > 0) range += av * 5;
          if (tpd.Length() < range) {
            var al = MathHelper.Clamp(range - tpd.Length(), 0, 10) * 2.0;
            var aa = tpdn * al;
            na += aa;
          }
        }
        if (fpIdx < fpList.Count - 2) {
          // avoid planet surface
          double height = 500;
          mainShipCtrl.TryGetPlanetElevation(MyPlanetElevation.Surface, out height);
          var needH = MathHelper.Clamp(500 - height, 0, 200) * 0.1;
          var needD = Vector3D.Normalize(-pGravity);
          needH -= Vector3D.Dot(shipVelGet() * 0.5, needD);
          na += needD * needH;
        }
      }

      if (autoDown) {
        na = shipVelGet() * -0.5;
      }

      // calc plane need (reject gravity dir)
      var pgn = Vector3D.Normalize(pGravity);
      var planeNeed = Vector3D.Reject(na, pgn);
      var ba = Vector3D.Zero;

      // calc plan z need
      nabfHave[0] = shipThrusts[0][T_FRONT].Count > 0;
      nabfHave[1] = shipThrusts[0][T_BACK].Count > 0;
      if (nabfHave[0] && Vector3D.Dot(planeNeed, shipMatrix.Forward) > 0)
      {
        var pbn = Vector3D.Normalize(Vector3D.Reject(shipMatrix.Backward, pgn));
        var nna = Vector3D.Reject(na, pbn);
        ba = na - nna;
        na = na - ba;
      }
      if (nabfHave[1] && Vector3D.Dot(planeNeed, shipMatrix.Forward) < 0)
      {
        var pfn = Vector3D.Normalize(Vector3D.Reject(shipMatrix.Forward, pgn));
        var nna = Vector3D.Reject(na, pfn);
        ba = na - nna;
        na = na - ba;
      }

      na -= pGravity;

      naL1MainLocal = Vector3D.TransformNormal(na, shipRevertMat);
      naL1BackLocal = Vector3D.TransformNormal(ba, shipRevertMat);

      debug("m: " + display3D(naL1MainLocal));
      debug("b: " + display3D(naL1BackLocal));
    }
    #endregion calcFollowNA

    #region followPosition

    void parseV3L(string tmps, List<Vector3D> l)
    {
      string[] ps = tmps.Split(';');
      foreach (var s in ps)
      {
        string[] ss = s.Split(',');
        if (ss.Length < 3) continue;
        Vector3D v;
        try
        {
          v = new Vector3D(Convert.ToDouble(ss[0]), Convert.ToDouble(ss[1]), Convert.ToDouble(ss[2]));
        }
        catch
        {
          continue;
        }
        l.Add(v);
      }

    }
    List<Vector3D> fpList = new List<Vector3D>();
    int fpIdx = 0;
    void followPosition()
    {
      if (fpList.Count == 0)
      {
        string tmps = "";
        tmps = cfg.Get(CFG_GENERAL, "followPositions").ToString("");
        parseV3L(tmps, fpList);
        if (fpList.Count == 0)
        {
          fpList.Add(new Vector3D(0, 10, 100));
          fpList.Add(new Vector3D(0, 0, 50));
          fpList.Add(new Vector3D(0, 0, 0));
          cfg.Set(CFG_GENERAL, "followPositions", "0,10,100;0,0,50;0,0,0");
          Me.CustomData = cfg.ToString();
        }
        fpIdx = fpList.Count - 1;
        isDocking = true;
      }

      if (!autoFollow) return;
      if (isDocking && fpIdx < fpList.Count - 1) {
        var diff = (shipPosition) - (motherPositionGet() + Vector3D.TransformNormal(fpList[fpIdx], motherMatrixD));
        if (diff.Length() < 1.5) {
            fpIdx ++;
        }
      }
      if (!isDocking && fpIdx > 0) {
        var diff = (shipPosition) - (motherPositionGet() + Vector3D.TransformNormal(fpList[fpIdx], motherMatrixD));
        if (diff.Length() < 1.5) {
            fpIdx --;
        }
      }

    }

    Vector3D followGetFP() {
      return fpList[fpIdx];
    }

    string dockHeadPoint = null;
    Vector3D dhp;
    Vector3D followGetForward() {
      if (fpIdx < fpList.Count - 2) return motherMatrixD.Forward;
      string tmps = "";
      if (dockHeadPoint == null) {
        tmps = cfg.Get(CFG_GENERAL, "dockHeadPoint").ToString("");
        if (tmps == "") {
          tmps = "F";
          cfg.Set(CFG_GENERAL, "dockHeadPoint", tmps);
          Me.CustomData = cfg.ToString();
        }
        switch(tmps) {
          case "F":
            dhp = motherMatrixD.Forward;
            break;
          case "B":
            dhp =  motherMatrixD.Backward;
            break;
          case "L":
            dhp =  motherMatrixD.Left;
            break;
          case "R":
            dhp =  motherMatrixD.Right;
            break;
          default:
            cfg.Set(CFG_GENERAL, "dockHeadPoint", "F");
            Me.CustomData = cfg.ToString();
            dhp =  motherMatrixD.Forward;
            break;
        }
      }
      return dhp;
    }

    #endregion followPosition

    #region aim
    static Vector3D HitPointCaculate(Vector3D Me_Position, Vector3D Me_Velocity, Vector3D Me_Acceleration, Vector3D Target_Position, Vector3D Target_Velocity, Vector3D Target_Acceleration,
              double Bullet_InitialSpeed, double Bullet_Acceleration, double Bullet_MaxSpeed,
              float gravityRate, Vector3D ng, double bulletMaxRange, double curvationRate)
    {
      string debugString = "";
      //GravityHitPointCaculate(new Vector3D(1, 1, 0), new Vector3D(0,0,-1), new Vector3D(0,-1,0), 3D, out debugString);
      //debugInfo += "\nghpc\n" + debugString + "\n";
      if (gravityRate > 0 && ng.Length() != 0)
      {
        var ret = GravityHitPointCaculate(Target_Position - Me_Position, Target_Velocity - Me_Velocity, ng * gravityRate, Bullet_InitialSpeed, bulletMaxRange, curvationRate, out debugString);
        if (ret == Vector3D.Zero) return Vector3D.Zero;
        ret += Me_Position;
        // debugInfo += "\nghpc\n" + debugString + "\n";
        return ret;
      }
      //迭代算法   
      Vector3D HitPoint = new Vector3D();
      Vector3D Smt = Target_Position - Me_Position;//发射点指向目标的矢量   
      Vector3D Velocity = Target_Velocity - Me_Velocity; //目标飞船和自己飞船总速度   
      Vector3D Acceleration = Target_Acceleration; //目标飞船和自己飞船总加速度   

      double AccTime = (Bullet_Acceleration == 0 ? 0 : (Bullet_MaxSpeed - Bullet_InitialSpeed) / Bullet_Acceleration);//子弹加速到最大速度所需时间   
      double AccDistance = Bullet_InitialSpeed * AccTime + 0.5 * Bullet_Acceleration * AccTime * AccTime;//子弹加速到最大速度经过的路程   

      double HitTime = 0;

      if (AccDistance < Smt.Length())//目标在炮弹加速过程外   
      {
        HitTime = (Smt.Length() - Bullet_InitialSpeed * AccTime - 0.5 * Bullet_Acceleration * AccTime * AccTime + Bullet_MaxSpeed * AccTime) / Bullet_MaxSpeed;
        HitPoint = Target_Position + Velocity * HitTime + 0.5 * Acceleration * HitTime * HitTime;
      }
      else//目标在炮弹加速过程内 
      {
        double HitTime_Z = (-Bullet_InitialSpeed + Math.Pow((Bullet_InitialSpeed * Bullet_InitialSpeed + 2 * Bullet_Acceleration * Smt.Length()), 0.5)) / Bullet_Acceleration;
        double HitTime_F = (-Bullet_InitialSpeed - Math.Pow((Bullet_InitialSpeed * Bullet_InitialSpeed + 2 * Bullet_Acceleration * Smt.Length()), 0.5)) / Bullet_Acceleration;
        HitTime = (HitTime_Z > 0 ? (HitTime_F > 0 ? (HitTime_Z < HitTime_F ? HitTime_Z : HitTime_F) : HitTime_Z) : HitTime_F);
        HitPoint = Target_Position + Velocity * HitTime + 0.5 * Acceleration * HitTime * HitTime;
      }
      //迭代，仅迭代更新碰撞时间，每次迭代更新右5位数量级   
      for (int i = 0; i < 3; i++)
      {
        if (AccDistance < Vector3D.Distance(HitPoint, Me_Position))//目标在炮弹加速过程外   
        {
          HitTime = (Vector3D.Distance(HitPoint, Me_Position) - Bullet_InitialSpeed * AccTime - 0.5 * Bullet_Acceleration * AccTime * AccTime + Bullet_MaxSpeed * AccTime) / Bullet_MaxSpeed;
          HitPoint = Target_Position + Velocity * HitTime + 0.5 * Acceleration * HitTime * HitTime;
        }
        else//目标在炮弹加速过程内   
        {
          double HitTime_Z = (-Bullet_InitialSpeed + Math.Pow((Bullet_InitialSpeed * Bullet_InitialSpeed + 2 * Bullet_Acceleration * Vector3D.Distance(HitPoint, Me_Position)), 0.5)) / Bullet_Acceleration;
          double HitTime_F = (-Bullet_InitialSpeed - Math.Pow((Bullet_InitialSpeed * Bullet_InitialSpeed + 2 * Bullet_Acceleration * Vector3D.Distance(HitPoint, Me_Position)), 0.5)) / Bullet_Acceleration;
          HitTime = (HitTime_Z > 0 ? (HitTime_F > 0 ? (HitTime_Z < HitTime_F ? HitTime_Z : HitTime_F) : HitTime_Z) : HitTime_F);
          HitPoint = Target_Position + Velocity * HitTime + 0.5 * Acceleration * HitTime * HitTime;
        }
      }
      return HitPoint;
    }

    static Vector3D GravityHitPointCaculate(Vector3D tp, Vector3D tv, Vector3D g, double aV, double bulletMaxRange, double curvationRate, out string debugString)
    {
      debugString = "";
      // 问题5 怀疑星球曲率原因，对g采取近小远大处理
      var gd = Vector3D.Normalize(g);
      var ngtpr = Vector3D.Reject(tp, gd).Length();
      g = g * (1 + ((ngtpr - bulletMaxRange * 0.5) * 2 / bulletMaxRange) * curvationRate);

      /*
      目标速度为  tvx, tvy, tvz. 目标位置  tpx, tpy, tpz.
      重力加速度 gax, gay, gaz.
      我方位置 mpx, mpy, mpz, 我方速度 mvx, mvy, mvz
      炮弹速度标量 aV, 
      假设炮弹速度为 avx, avy, avz
      命中时间为 n

      则有
      tpx + tvx * n = mpx + mvx * n + avx * n +  0.5 * gax * n * n 方程1
      tpy + tvy * n = mpy + mvy * n + avy * n + 0.5 * gay * n * n 方程2
      tpz + tvz * n = mpz + mvz * n + avz * n + 0.5 * gaz * n * n 方程3
      avx * avx + avy * avy + avz * avz = aV * aV 方程4

      以重力方向向下, 面朝目标方向建立座标系, 则有 gax = gaz = 0 gay = -重力加速度
      且 tpx = mpx = 0
      由于接受参数时, 已改用本机位置和速度作为位置和速度基准, 所以还有mp = 0和mv=0

      步骤1, 由方程1
      则可以直接求出avx = tvx - mvx

      由方程3
      tpz - mpz = (avz + mvz - tvz) * n
      可知 n大 则 avz 小, n小, 则avz大

      由方程4
      avz * avz + avy * avy = aV * aV  - avx * avx 
      由于avx已知, 可知 avz 和 avy 形成一个圆形

      假设gay = 0 先求一个n出来, 再把gay代入, 重新计算avy avz n 迭代多次逼近正确的n值

      */

      if (tp == Vector3D.Zero) return Vector3D.Zero;
      // 1 建立座标系
      // 1.1 检查 tp方向 与 g 方向是否 完全同向/异向 算法无法处理这种情况 , 不攻击 (缺陷1)
      var dot = Vector3D.Dot(Vector3D.Normalize(tp), gd);
      if (dot == 1 || dot == -1) return Vector3D.Zero;
      // 1.2 构建座标转换矩阵
      var forward = Vector3D.Normalize(Vector3D.Reject(tp, gd));
      var tranmt = MatrixD.CreateLookAt(new Vector3D(), forward, -gd);

      // 1.3 转换座标
      var tp2 = Vector3D.TransformNormal(tp, tranmt);
      var tv2 = Vector3D.TransformNormal(tv, tranmt);
      //debugString = displayVector3D(tp2);
      //debugString += "\n" + displayVector3D(tv2);

      // 2 求 avx
      double avx = tv2.X;
      if (Math.Abs(avx) > aV) return Vector3D.Zero; // 炮速赶不上tv2.X 无法追踪
                                                    //debugString += "\navx: " + avx;

      // 3 无视g 先算一个avy avz
      // 3.1 假设减掉tvYZ, Z轴剩余速度有avzd, 则Y轴需要的剩余速度为 (tpy/tpz)*avzd
      // 有 (tvz+avzd)2 + (tvy + (tpy/tpz)avzd)2 = aVyz2
      double aVyz = Math.Sqrt(aV * aV - avx * avx);
      if (tv2.Z * tv2.Z + tv2.Y * tv2.Y > aVyz * aVyz) return Vector3D.Zero; // yz面目标速度大于炮弹速度 追不上, 不考虑利用重力加速度追 (缺陷2)
                                                                             // tpz 不可能为0 因为能建座标系就表示有向前的分量, 即z方向分量)
                                                                             // 采用一元二次方程 ax2 +bx + c = 0方式, 整理a , b, c
      double fa = 1 + ((tp2.Y * tp2.Y) / (tp2.Z * tp2.Z));
      double fb = 2 * tv2.Z + 2 * tv2.Y * (tp2.Y / tp2.Z);
      double fc = tv2.Z * tv2.Z + tv2.Y * tv2.Y - aVyz * aVyz;
      if (fb * fb - 4 * fa * fc < 0) return Vector3D.Zero; // 无解, 返回
                                                           // 根据 一元二次方程求根公式, 计算x (即avzd)
      double x = (-fb + Math.Sqrt(fb * fb - 4 * fa * fc)) / (2 * fa);
      double avz = 0;
      if (tv2.Z + x < 0)
      {
        avz = tv2.Z + x;
      }
      else
      {
        x = (-fb - Math.Sqrt(fb * fb - 4 * fa * fc)) / (2 * fa);
        avz = tv2.Z + x;
      }
      double avy = tv2.Y + (tp2.Y / tp2.Z) * x;

      // 3.2 根据z轴, 算追及时间 n
      double zdelta = avz - tv2.Z;
      double n = tp2.Z / zdelta;
      if (n < 0) return Vector3D.Zero; // Z轴追及时间为负, 无法追踪
                                       //debugString += "\naVyz: " + aVyz;
                                       //debugString += "\navy: " + avy;
                                       //debugString += "\navz: " + avz;
                                       //debugString += "\nn: " + n;

      // 4 循环逼近多次(这个方程应该有解, 但这里采取逼近法简化)(缺陷3) 加速度的问题 主要是计算avyg分量应该取多少
      double avyg = 0;
      double avyp = avy;
      for (int i = 0; i < 4; i++)
      {
        // 4.1 按当前n 计算avyg, 取avyg = 0.5 * (-g) * n ; 举例, g为 向下10, 时间1秒, 我们需要向上5, 这样前0.5秒为上升期, 后0.5秒为下降期, 上升距离=下降距离, 不影响瞄准
        avyg = 0.5 * g.Length() * n;
        // 4.2 由于时间n变长了, 需要重新计算avy
        avyp = tv2.Y + (tp2.Y / n);
        // 4.3 加上avyg
        avyp = avyp + avyg;
        if (Math.Abs(avyp) > aVyz) return Vector3D.Zero;
        double avzL = Math.Sqrt(aVyz * aVyz - avyp * avyp);
        avz = -avzL;
        zdelta = avz - tv2.Z;
        double nn = tp2.Z / zdelta;
        if (nn > n) n = nn;
        else n = (nn + n) / 2;
        if (n < 0) return Vector3D.Zero; // Z轴追及时间为负, 无法追踪

        //debugString += "\navy: " + avyp;
        //debugString += "\navz: " + avz;
        //debugString += "\nn: " + n;

      }

      // 5 将avx avy avz 转回绝对座标系 并输出(注意本来这里应该输出碰撞点位置 , 但这里以发射速度代替, 所以还要加上本机位置, 调用者处理)
      avyp *= 0.98;
      Vector3D av2m = new Vector3D(avx, avyp, avz);
      Vector3D av = Vector3.Transform(av2m, Matrix.Transpose(tranmt));
      //debugString += "\nav: " + displayVector3D(av);
      return av;
    }
    #endregion aim

    #endregion ingamescript

  }
}