﻿using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Text.RegularExpressions;
using VRage;
using VRage.Game;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;
using VRageRender;
namespace RadarDisplay { 
public class Program:MyGridProgram { 
#region In-game Script 
/* 
/ //// / Whip's Turret Based Radar Systems / //// / 
 
HOW DO I USE THIS? 
 
1. Place this script in a programmable block. 
2. Place some turrets on your ship. 
3. Place a seat on your ship. 
4. Place some text panels with "Radar" in their name somewhere. 
5. Enjoy! 
 
 
 
 
================================================= 
    DO NOT MODIFY VARIABLES IN THE SCRIPT! 
 
 USE THE CUSTOM DATA OF THIS PROGRAMMABLE BLOCK! 
================================================= 
 
 
 
 
 
 
 
 
 
 
 
 
 
 
 
 
 
 
 
 
 
 
 
 
 
 
HEY! DONT EVEN THINK ABOUT TOUCHING BELOW THIS LINE! 
 
*/

//#region Fields 
const string VERSION = "30.5.2";
const string DATE = "04/08/2019";

enum TargetRelation : byte { Neutral = 0, Enemy = 1, Friendly = 2 }

const string IGC_TAG = "IGC_IFF_MSG";

const string INI_SECTION_GENERAL = "Radar - General";
const string INI_BCAST = "Share own position";
const string INI_MASTER_CODE = "Master code";
const string INI_SLAVE_CODE = "Slave code";
const string INI_START_WHEN_COMPILED = "Start when compiled";
const string INI_KRADAR_PANEL_NAME = "KRadar panel name";
const string INI_AIM_MARK_NAME = "Aim mark name";
const string INI_ALERT_RANGE = "Alert range";

const string INI_NETWORK = "Share targets";
const string INI_USE_RANGE_OVERRIDE = "Use radar range override";
const string INI_RANGE_OVERRIDE = "Radar range override (m)";
const string INI_PROJ_ANGLE = "Radar projection angle in degrees (0 is flat)";

const string INI_SECTION_COLORS = "Radar - Colors";
const string INI_TEXT = "Text";
const string INI_BACKGROUND = "Background";
const string INI_RADAR_LINES = "Radar lines";
const string INI_PLANE = "Radar plane";
const string INI_ENEMY = "Enemy icon";
const string INI_ENEMY_ELEVATION = "Enemy elevation";
const string INI_NEUTRAL = "Neutral icon";
const string INI_NEUTRAL_ELEVATION = "Neutral elevation";
const string INI_FRIENDLY = "Friendly icon";
const string INI_FRIENDLY_ELEVATION = "Friendly elevation";

const string INI_SECTION_TEXT_SURF_PROVIDER = "Radar - Text Surface Config";
const string INI_TEXT_SURFACE_TEMPLATE = "Show on screen {0}";

const int MAX_REBROADCAST_INGEST_COUNT = 2;

string MOTHER_CODE = "2ndUsagi1";
static string debugInfo = "";
static string debugOnce = "";
static string debugInterval = "";
string COCKPIT_NAME = "Reference";
static int t = 0;
MatrixD refLookAtMatrix = new MatrixD();
bool motherSignalRotation = false;
static Vector3D MePosition = new Vector3D();
IMyShipController msc;
bool inited = false;
string SON_CODE = "2ndUsagi";
string DCS_NAME = "afs";
IMyProgrammableBlock dcsComputer = null;
IMyProgrammableBlock fcsComputer = null;
string FCS_NAME = "fcs";
IMyTerminalBlock fcsReference = null;
string FCS_REFERENCE_NAME = "Lidar FCS#R";
string KRADAR_PANEL_NAME = "KRadar Panel";
IMyTextSurface kradarPanel = null;
IMyTerminalBlock aimMark = null;
string AIM_MARK_NAME = "Projector Lidar";

IMyBroadcastListener broadcastListener;

float rangeOverride = 1000;
bool useRangeOverride = false;
bool startWhenCompiled = true;
bool networkTargets = false;
bool broadcastIFF = true;

Color backColor = new Color(0, 0, 0, 255);
Color lineColor = new Color(255, 100, 0, 50);
Color planeColor = new Color(100, 30, 0, 5);
Color enemyIconColor = new Color(150, 100, 40, 255);
Color enemyElevationColor = new Color(75, 50, 20, 255);
Color enemyHighIconColor = new Color(150, 0, 0, 255);
static Color enemyLockIconColor = new Color(250, 0, 0, 255);
Color enemyHighElevationColor = new Color(75, 0, 0, 255);
Color enemyDisableIconColor = new Color(100, 100, 100, 255);
Color enemyDisableElevationColor = new Color(50, 50, 50, 255);
Color neutralIconColor = new Color(150, 150, 0, 255);
Color neutralElevationColor = new Color(75, 75, 0, 255);
Color allyIconColor = new Color(0, 50, 150, 255);
Color allyElevationColor = new Color(0, 25, 75, 255);
Color textColor = new Color(255, 100, 0, 100);

const int dsInterval = 60;
static int dsFrame = new Random().Next(dsInterval);


float MaxRange
{
    get
    {
        return Math.Max(1, useRangeOverride ? rangeOverride : (turrets.Count == 0 ? rangeOverride : turretMaxRange));
    }
}

string textPanelName = "[R_LCD]";
string cmdPanelName = "[R_LCD_CMD]";
float projectionAngle = 60f;
float turretMaxRange = 800f;

Scheduler scheduler;
RuntimeTracker runtimeTracker;
//ScheduledAction grabBlockAction; 

Dictionary<long, TargetData> targetDataDict = new Dictionary<long, TargetData>();
List<IMyLargeTurretBase> turrets = new List<IMyLargeTurretBase>();
List<IMyTurretControlBlock> turretControls = new List<IMyTurretControlBlock>();
List<IMyTextSurface> textSurfaces = new List<IMyTextSurface>();
List<IMyTextSurface> cmdSurfaces = new List<IMyTextSurface>();
List<IMyShipController> controllers = new List<IMyShipController>();
IMyShipController reference;
IMyShipController lastActiveShipController = null;
long pickedIdx = 0;

const double cycleTime = 1.0 / 60.0;
string lastSetupResult = "";
bool isSetup = false;

RadarSurface radarSurface;
readonly MyIni generalIni = new MyIni();
readonly MyIni textSurfaceIni = new MyIni();

string droneRadarName = "radar drone";
IEnumerator<bool> LaunchStateMachine;

double ALERT_RANGE = 5000;
static long kradarLastUpdate = 0;
static Vector3D testVec = new Vector3D();
static Vector3D testPosLast = new Vector3D();
static long testTLast = 0;
static Queue<Vector3D> testQueue = new Queue<Vector3D>();

//#endregion 

//#region Main Routine 
Program()
{
    ParseCustomDataIni();

    if (startWhenCompiled)
    {
        Runtime.UpdateFrequency = UpdateFrequency.Update1;
        // IGC Register 
        broadcastListener = IGC.RegisterBroadcastListener(IGC_TAG);
        broadcastListener.SetMessageCallback(IGC_TAG);
    }

}

void updateMotion()
{
    msc = reference;

    refLookAtMatrix = MatrixD.CreateLookAt(new Vector3D(), msc.WorldMatrix.Forward, msc.WorldMatrix.Up);
    MePosition = msc.GetPosition();
}

string[] DP_DEF = new string[]{"LL", "RR", "LU", "RU", "LB", "RB", "L0", "R0", "L1", "R1", "L2", "R2", "L3", "R3", "L4", "R4", };
int MAX_DP = 6;

static float toRadius(float i) {
return (i / 180F) * (float)Math.PI;
}

public int getBlankDPIdx() {
    for (int i = 0; i < MAX_DP; i++) {
        string haveStr = "[DP_" + DP_DEF[i] + "]";
        bool have = false;
        foreach(var t in targetDataDict) {
            if (t.Value.Code.Contains(haveStr) && t.Value.live()) {
                have = true;
                break;
            }
        }
        if (!have) return i;
    }
    return 0;
}
public string getFp0(int idx) {
    long r = 200 + (idx >= 6 ? 200 : 0);
    long f = -50;
    float a = toRadius(180);
    switch(idx) {
    case 0:
        a = toRadius(180);
    break;
    case 1:
        a = toRadius(0);
    break;
    case 2:
        a = toRadius(135);
    break;
    case 3:
        a = toRadius(45);
    break;
    case 4:
        a = toRadius(-135);
    break;
    case 5:
        a = toRadius(-45);
    break;
    case 6:
        a = toRadius(180);
    break;
    case 7:
        a = toRadius(0);
    break;
    case 8:
        a = toRadius(135);
    break;
    case 9:
        a = toRadius(45);
    break;
    case 10:
        a = toRadius(-135);
    break;
    case 11:
        a = toRadius(-45);
    break;
    case 12:
        a = toRadius(158);
    break;
    case 13:
        a = toRadius(22);
    break;
    case 14:
        a = toRadius(-158);
    break;
    case 15:
        a = toRadius(-22);
    break;

    }
    string ret = "";
    ret += (long)(r * Math.Cos(a)) + ",";
    ret += (long)(r * Math.Sin(a)) + ",";
    ret += f;
    return ret;
}

public IEnumerable<bool> DroneLaunchHandler()
{
    List<IMyProgrammableBlock> droneRadarList = getBlockListByName<IMyProgrammableBlock>(droneRadarName, false, false);
    if (droneRadarList.Count == 0) yield return false;
    List<IMyShipMergeBlock> mergeList = getBlockListByName<IMyShipMergeBlock>("Drone", false, false);
    if (mergeList.Count == 0) yield return false;
    List<IMyProgrammableBlock> droneDcsList = getBlockListByName<IMyProgrammableBlock>("afs drone", false, false);
    if (droneRadarList.Count == 0) yield return false;
    if (droneDcsList.Count == 0) yield return false;


    var droneRadar = droneRadarList[0];
    mergeList.Sort((x, y) => (x.GetPosition() - droneRadar.GetPosition()).Length().CompareTo((y.GetPosition() - droneRadar.GetPosition()).Length()));
    var merge = mergeList[0];
    merge.Enabled = false;
    yield return true;
    yield return true;
    PlayAction((IMyTerminalBlock)droneRadar, "Run", "TurnOn");
    for (int i = 0; i < 60; i++) yield return true;
    PlayAction((IMyTerminalBlock)droneDcsList[0], "Run", "RADAR:STANDBYOFF");
    for (int i = 0; i < 60; i++) yield return true;
    PlayAction((IMyTerminalBlock)droneDcsList[0], "Run", "RADAR:FLYBYON");
    yield return true;
    int DP_idx = getBlankDPIdx();
    PlayAction((IMyTerminalBlock)droneRadar, "Run", "SETUP_DRONE:" + DP_DEF[DP_idx]);
    yield return true;
    PlayAction((IMyTerminalBlock)droneDcsList[0], "Run", "RADAR:RESET_FP0," + getFp0(DP_idx));
}

void Main(string arg, UpdateType updateSource)
{
    if(arg == null || arg.Equals("")) t++;
    debugInfo = $"{runningSymbols[t%8]}";
    if (!inited)
    {
        init();
        if (arg.Equals("TurnOn"))
        {
            startWhenCompiled = true;
            // IGC Register 
            broadcastListener = IGC.RegisterBroadcastListener(IGC_TAG);
            broadcastListener.SetMessageCallback(IGC_TAG);
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            WriteCustomDataIni();
        }
        return;
    }
    else if (arg.Equals("RROW"))
    {
        if (droneSelectRow >= droneCodeList.Count - 1) droneSelectRow = -1;
        else droneSelectRow++;
    }
    else if (arg.Equals("RCOL"))
    {
        droneSelectColumn = (droneSelectColumn + 1) % 6;
    }
    else if (arg.Equals("REXE"))
    {
        var code = MOTHER_CODE;
        if (droneSelectRow >= 0)
        {
            code += "|" + droneCodeList[droneSelectRow];
        }
        string command = "";
        switch (droneSelectColumn)
        {
            case 0:
                command = "FLYBYON";
                break;
            case 1:
                command = "DOCKINGON";
                break;
            case 2:
                command = "DKMVF6";
                break;
            case 3:
                command = "DKMVF5";
                break;
            case 4:
                command = "DKMVB5";
                break;
            case 5:
                command = "DKMVF7";
                break;
            default:
                break;
        }
        var myTuple = new MyTuple<string, string>(code, command);
        IGC.SendBroadcastMessage(IGC_TAG, myTuple);
    }
    else if (arg.Equals("RCLEAR"))
    {
        droneCodeList.Clear();
        droneSelectRow = -1;
    }
    else if (arg.Equals("ActivateDrone"))
    {
        if (LaunchStateMachine == null)
        {
            LaunchStateMachine = DroneLaunchHandler().GetEnumerator();
        }
    }
    else if (arg.Contains("SETUP_DRONE:")) {
        var paras = arg.Split(':')[1].Split(',');
        var mc_suffix = paras[0];
        if (MOTHER_CODE.Contains("[DP_")) {
            var si = MOTHER_CODE.IndexOf("[DP_");
            MOTHER_CODE = MOTHER_CODE.Substring(0, si) + "[DP_" + mc_suffix + "]";
        } else {
            MOTHER_CODE += " [DP_" + mc_suffix + "]";
        }
        WriteCustomDataIni();
    }
    else if (arg.Equals("PICK_NEAR"))
    {
        pickClear();
        var ktl = targetDataDict.Values.Where(x => x is KRadarTargetData).OrderBy(x => (x.estPosition() - MePosition).Length());
        if (ktl.Any())
        {
            var ktt = (KRadarTargetData)ktl.First();
            ktt.isSelected = true;
            pickedIdx = ktt.id;
        }
    }
    else if (arg.Equals("PICK_NEXT"))
    {
        var ktl = targetDataDict.Values.Where(x => x is KRadarTargetData).OrderBy(x => (x.estPosition() - MePosition).Length());
        bool found = false;
        bool set = false;
        foreach (var kt in ktl)
        {
            var ktt = (KRadarTargetData)kt;
            if (found == true)
            {
                ktt.isSelected = true;
                pickedIdx = ktt.id;
                set = true;
                break;
            }

            if (ktt.id == pickedIdx)
            {
                ktt.isSelected = false;
                pickedIdx = 0;
                found = true;
            }
        }
        if (set == false && ktl.Any())
        {
            var ktt = (KRadarTargetData)ktl.First();
            ktt.isSelected = true;
            pickedIdx = ktt.id;
        }
    }
    else if (arg.Equals("PICK_NEAR_HIGH"))
    {
        pickClear();
        var ktl = targetDataDict.Values.Where(x => x is KRadarTargetData && ((KRadarTargetData)x).isHighThreaten).OrderBy(x => (x.estPosition() - MePosition).Length());
        if (ktl.Any())
        {
            var ktt = (KRadarTargetData)ktl.First();
            ktt.isSelected = true;
            pickedIdx = ktt.id;
        }
    }
    else if (arg.Equals("PICK_CLEAR"))
    {
        pickClear();
    }
    else if (arg.Equals("PICK_DISABLE"))
    {
        bool cannot = pickedIdx == 0 || !targetDataDict.Any(x => x.Key == pickedIdx);
        if (!cannot) { 
            var kt = targetDataDict[pickedIdx];
            if (kt is KRadarTargetData) { 
                var ktt = (KRadarTargetData) kt;
                ktt.isDisabled = !ktt.isDisabled;
            }
        }
    }
    updateMotion();

    runtimeTracker.AddRuntime();

    scheduler.Update();
    // kaien
    parseFcsTarget();
    debug($"pr, {runningSymbols[t%8]}");
    parseKRadarTarget();
    doAimMark();

    if (arg.Equals(IGC_TAG))
    {
        ProcessNetworkMessage();
    }
    else
    {
        string command = arg;
        var myTuple = new MyTuple<string, string>(MOTHER_CODE, command);
        IGC.SendBroadcastMessage(IGC_TAG, myTuple);
    }

    runtimeTracker.AddInstructions();

    callDcsSendPosition();
    callDcsSendAvoid();
    callDcsSendEnemy();

    if (LaunchStateMachine != null)
    {
        if (!LaunchStateMachine.MoveNext() || !LaunchStateMachine.Current)
        {
            LaunchStateMachine.Dispose();
            LaunchStateMachine = null;
        }
    }

    PrintDetailedInfo();
}

bool lastAimMarkNeed = false;
private void doAimMark()
{
    if (aimMark == null) return;
    bool need = pickedIdx != 0 && targetDataDict.ContainsKey(pickedIdx);
    if (need == lastAimMarkNeed) return;
    if (need) { 
        PlayAction(aimMark, "OnOff_On");
    } else { 
        PlayAction(aimMark, "OnOff_Off");
    }
    lastAimMarkNeed = need;
}

void pickClear()
{
    TargetData td;
    if (targetDataDict.TryGetValue(pickedIdx, out td))
    {
        if (td is KRadarTargetData)
        {
            ((KRadarTargetData)td).isSelected = false;
        }
    }
    pickedIdx = 0;
}

void callDcsSendAvoid()
{
    if (t % dsInterval != dsFrame) return;
    List<KeyValuePair<long, TargetData>> s = targetDataDict.Where(item => {
        var k = item.Key;
        var v = item.Value;
        if (v.Relation != TargetRelation.Friendly) return false;
        if ((MePosition - v.estPosition()).Length() > 100) return false;
        if (k == Me.CubeGrid.EntityId) return false;
        return true;
    }).ToList();
    Vector3D force = Vector3D.Zero;
    s.ForEach(f =>
    {
        var d = MePosition - f.Value.estPosition();
        var dir = Vector3D.Normalize(d);
        var len = d.Length();
        var fLen = 100D / len;
        force += dir * fLen;
    });
    if(force != Vector3D.Zero){
        var dir = Vector3D.Normalize(force);
        dir = -dir;
        var len = 100D / force.Length();
        sendAvoid(MePosition + dir*len);
    }
//    s.Sort((l, r) =>
//        (int)((MePosition - r.Value.estPosition()).Length() -
//    (MePosition - l.Value.estPosition()).Length())
//    );
//    if (s.Count > 0) sendAvoid(s[0]);
}

void sendAvoid(Vector3D p)
{
    string message = "RADAR" + "-AVOID:" + "9" + "," + p.X + "," + p.Y + "," + p.Z;
    PlayAction(dcsComputer, "Run", message);
}

void callDcsSendPosition()
{
    if (t % dsInterval != dsFrame) return;
    //debugOnce = $"target {t} count {targetDataDict.Count}";
    foreach (var item in targetDataDict)
    {
        var k = item.Key;
        var v = item.Value;
        if (v.Relation == TargetRelation.Friendly && v.Code == SON_CODE)
        {
            sendPosition(k, v);
            return;
        }
    }
    PlayAction(dcsComputer, "Run", null);

}

void callDcsSendEnemy()
{
    //debugOnce = $"target {t} count {targetDataDict.Count}";
    List<KeyValuePair<long, TargetData>> enemyList = targetDataDict.Where(i => i.Value.Relation == TargetRelation.Enemy && (MePosition - i.Value.estPosition()).Length() < ALERT_RANGE).ToList();
    //debug("el: " + String.Join("\n", enemyList.Select(e => "e" + e.Value.priority + " " + (MePosition - e.Value.estPosition()).Length())));
    enemyList.Sort((l, r) => {
        if (l.Value.priority != r.Value.priority) return l.Value.priority - r.Value.priority;
        else if (l.Value is KRadarTargetData && r.Value is KRadarTargetData && ((KRadarTargetData)l.Value).isSelected != ((KRadarTargetData)r.Value).isSelected) {
            if (((KRadarTargetData)l.Value).isSelected) return -1;
            else return 1;
        }
        else return 
        (int)((MePosition - l.Value.estPosition()).Length() -
        (MePosition - r.Value.estPosition()).Length());
    });
    string message = "";
    if (enemyList.Count > 0)
    {
        var k = enemyList[0].Key;
        var v = enemyList[0].Value;
        Vector3D pos = v.estPosition();
        Vector3D vel = v.Velocity;
        message = "RADAR-ENEMY:" +pos.X + "," + pos.Y + "," + pos.Z + "," + vel.X + "," + vel.Y + "," + vel.Z + "," + v.Round;
        PlayAction(dcsComputer, "Run", message);
    }

}

void sendPosition(long entityId, TargetData td, string cmd = "RADAR")
{
    MatrixD refWorldMatrix = td.Mat;
    var f = td.Mat.Forward;
    if (f == Vector3D.Zero) return;
    Vector3D currentPos = td.estPosition();
    if (currentPos == null || currentPos.X == 0)
    {
        // debugInfo = "error";
    }
    Vector3D speed = td.Velocity;
    string motherCode = td.Code;
    string message = cmd + ":" + refWorldMatrix.M11 + "," + refWorldMatrix.M12 + "," + refWorldMatrix.M13 + "," + refWorldMatrix.M14 + "," +
    refWorldMatrix.M21 + "," + refWorldMatrix.M22 + "," + refWorldMatrix.M23 + "," + refWorldMatrix.M24 + "," +
    refWorldMatrix.M31 + "," + refWorldMatrix.M32 + "," + refWorldMatrix.M33 + "," + refWorldMatrix.M34 + "," +
    currentPos.X + "," + currentPos.Y + "," + currentPos.Z + "," + refWorldMatrix.M44 + "," +
    speed.X + "," + speed.Y + "," + speed.Z;

    PlayAction(dcsComputer, "Run", message);

}

void ProcessNetworkMessage()
{
    while (broadcastListener.HasPendingMessage)
    {
        object messageData = broadcastListener.AcceptMessage().Data;
        if (messageData is MyTuple<byte, long, Vector3D, byte, string>)
        {
            var myTuple = (MyTuple<byte, long, Vector3D, byte, string>)messageData;
            byte relationship = myTuple.Item1;
            long entityId = myTuple.Item2;
            Vector3D position = myTuple.Item3;
            if(targetDataDict.Any(x=> (x.Value.estPosition() - position).Length() < 1)) continue;
            byte ingestCount = ++myTuple.Item4;
            string motherCode = "";
            MatrixD targetMatrix = new MatrixD();
            Vector3D velocity = new Vector3D();
            double roundRotationCount = 0;
            decodeMessage(myTuple.Item5, out motherCode, out targetMatrix, out velocity, out roundRotationCount);

            if ((byte)TargetRelation.Friendly == relationship)
            {
                putTargetDict(entityId, new TargetData(position, TargetRelation.Friendly, ingestCount, velocity, motherCode, targetMatrix, roundRotationCount,t, TargetData.PR_ANTENNA));
            }
            else if ((byte)TargetRelation.Neutral == relationship)
            {
                putTargetDict(entityId, new TargetData(position, TargetRelation.Neutral, ingestCount, velocity, "", targetMatrix, 0,t, TargetData.PR_ANTENNA));
            }
            else
            {
                putTargetDict(entityId, new TargetData(position, TargetRelation.Enemy, ingestCount, velocity, motherCode, targetMatrix, 0,t, TargetData.PR_ANTENNA));
            }
        }
        else if (messageData is MyTuple<string, string>)
        {
            var myTuple = (MyTuple<string, string>)messageData;
            string motherCode = myTuple.Item1;
            if (motherCode == SON_CODE || motherCode == SON_CODE + "|" + MOTHER_CODE)
            {
                string command = myTuple.Item2;
                string[] ckv = command.Split(':');
                if(ckv.Length == 1) { 
                    PlayAction(dcsComputer, "Run", "RADAR:" + command);
                } else { 
                    switch(ckv[0]) {
                                case "ArmLock":
                                case "ArmUnlock":
                                    String[] posa = ckv[1].Split(',');
                                    double x, y, z;
                                    double.TryParse(posa[0], out x);
                                    double.TryParse(posa[1], out y);
                                    double.TryParse(posa[2], out z);
                                    var pos = new Vector3D(x, y, z);
                                    if ((pos - Me.GetPosition()).Length() < 5)
                                    {
                                        if(ckv[0].Equals("ArmLock"))
                                            PlayAction(dcsComputer, "Run", "RADAR:STANDBYON");
                                        else
                                            PlayAction(dcsComputer, "Run", "RADAR:STANDBYOFF");
                                    }
                                    break;
                                default:
                                    break;
                    }
                }
            }
        }
    }
}
void NetworkTargets()
{
    if (broadcastIFF)
    {
        MatrixD refWorldMatrix = reference.WorldMatrix;
        if (motherSignalRotation)
        {
            Vector3D tmp = new Vector3D(0, 1, 0);
            var rm = MatrixD.CreateRotationZ(t % 5000 / 5000f * MathHelper.TwoPi);
            tmp = Vector3D.Rotate(tmp, rm);

            var newUp = Vector3D.TransformNormal(tmp, refLookAtMatrix);

            var rd = MatrixD.CreateFromDir(reference.WorldMatrix.Forward, newUp);
            refWorldMatrix = rd;
        }
        if (true && fcsReference != null)
        { // use lider
            refWorldMatrix = fcsReference.WorldMatrix;
        }
        var ktl = targetDataDict.Values.Where(x => x is KRadarTargetData && ((KRadarTargetData)x).isSelected);
        var vel = reference.GetShipVelocities().LinearVelocity;

        var myTuple = new MyTuple<byte, long, Vector3D, byte, string>((byte)TargetRelation.Friendly, Me.CubeGrid.EntityId, reference.GetPosition(), 0, encodeMessage(MOTHER_CODE, refWorldMatrix, vel, (t % 50000 / 50000f) * MathHelper.TwoPi));
        IGC.SendBroadcastMessage(IGC_TAG, myTuple);
    }

    // if (networkTargets) 
    // { 
    foreach (var kvp in targetDataDict)
    {
        var targetData = kvp.Value;
        if (targetData.IngestCount > MAX_REBROADCAST_INGEST_COUNT)
            continue;
        if (!networkTargets && targetData.Relation == TargetRelation.Friendly) continue;

        var myTuple = new MyTuple<byte, long, Vector3D, byte, string>((byte)targetData.Relation, kvp.Key, targetData.estPosition(), targetData.IngestCount, encodeMessage(targetData.Code, new MatrixD(), targetData.Velocity, 0));
        IGC.SendBroadcastMessage(IGC_TAG, myTuple);
    }
    //} 
}

List<string> droneCodeList = new List<string>();
int droneSelectRow = -1;
int droneSelectColumn = 0;

void DrawCmdPanel()
{
    if (cmdSurfaces.Count == 0) return;
    //
    var friendList = targetDataDict.Values.ToList<TargetData>().Where(t => t.Relation == TargetRelation.Friendly).ToList();
    foreach (var td in friendList)
    {
        if (!droneCodeList.Contains(td.Code)) droneCodeList.Add(td.Code);
    }
    //
    var surface = cmdSurfaces[0];

    surface.ContentType = ContentType.SCRIPT;
    surface.Script = "";

    Vector2 surfaceSize = surface.TextureSize;
    Vector2 screenCenter = surfaceSize * 0.5f;
    Vector2 viewportSize = surface.SurfaceSize;
    Vector2 scale = viewportSize / 512f;
    float minScale = Math.Min(scale.X, scale.Y);
    float sideLength = Math.Min(viewportSize.X, viewportSize.Y) - 12f;

    Color selectColor = new Color(255, 0, 0, 100);

    using (var frame = surface.DrawFrame())
    {
        // Fill background with background color 
        MySprite sprite = new MySprite(SpriteType.TEXTURE, "SquareSimple", color: backColor);
        sprite.Position = screenCenter;
        frame.Add(sprite);

        string FONT = "Debug";
        float HUD_TEXT_SIZE = 0.8f;

        float textSize = minScale * HUD_TEXT_SIZE;
        Vector2 halfScreenSize = viewportSize * 0.5f;
        sprite = MySprite.CreateText($"Drone Command System", FONT, textColor, textSize, TextAlignment.CENTER);
        sprite.Position = screenCenter + new Vector2(0, -halfScreenSize.Y);
        frame.Add(sprite);
        var ms = minScale;
        float lineHeight = 24 * ms;
        float nowHeight = lineHeight;
        var uc = textColor;
        bool sele = false;
        for (int i = 0; i < droneCodeList.Count; i++)
        {
            nowHeight += lineHeight;
            var code = droneCodeList[i];
            uc = textColor; sele = false;
            if (i == droneSelectRow) { uc = selectColor; sele = true; }
            sprite = MySprite.CreateText($"{code}", FONT, uc, textSize, TextAlignment.LEFT);
            sprite.Position = screenCenter + new Vector2(-halfScreenSize.X, -halfScreenSize.Y + nowHeight);
            frame.Add(sprite);

            if (sele && droneSelectColumn == 0) uc = selectColor;
            else uc = textColor;
            sprite = MySprite.CreateText($"TO", FONT, uc, textSize, TextAlignment.LEFT);
            sprite.Position = screenCenter + new Vector2(-halfScreenSize.X + 340, -halfScreenSize.Y + nowHeight);
            frame.Add(sprite);

            if (sele && droneSelectColumn == 1) uc = selectColor;
            else uc = textColor;
            sprite = MySprite.CreateText($"DK", FONT, uc, textSize, TextAlignment.LEFT);
            sprite.Position = screenCenter + new Vector2(-halfScreenSize.X + 370, -halfScreenSize.Y + nowHeight);
            frame.Add(sprite);

            if (sele && droneSelectColumn == 2) uc = selectColor;
            else uc = textColor;
            sprite = MySprite.CreateText($"F6", FONT, uc, textSize, TextAlignment.LEFT);
            sprite.Position = screenCenter + new Vector2(-halfScreenSize.X + 400, -halfScreenSize.Y + nowHeight);
            frame.Add(sprite);

            if (sele && droneSelectColumn == 3) uc = selectColor;
            else uc = textColor;
            sprite = MySprite.CreateText($"F5", FONT, uc, textSize, TextAlignment.LEFT);
            sprite.Position = screenCenter + new Vector2(-halfScreenSize.X + 430, -halfScreenSize.Y + nowHeight);
            frame.Add(sprite);

            if (sele && droneSelectColumn == 4) uc = selectColor;
            else uc = textColor;
            sprite = MySprite.CreateText($"B5", FONT, uc, textSize, TextAlignment.LEFT);
            sprite.Position = screenCenter + new Vector2(-halfScreenSize.X + 460, -halfScreenSize.Y + nowHeight);
            frame.Add(sprite);

            if (sele && droneSelectColumn == 5) uc = selectColor;
            else uc = textColor;
            sprite = MySprite.CreateText($"F7", FONT, uc, textSize, TextAlignment.LEFT);
            sprite.Position = screenCenter + new Vector2(-halfScreenSize.X + 490, -halfScreenSize.Y + nowHeight);
            frame.Add(sprite);

            var tmpl = targetDataDict.Values.ToList<TargetData>().Where(t => t.Code == code).ToList();
            if (tmpl.Count == 0) continue;
            TargetData td = tmpl[0];

            sprite = MySprite.CreateText($"{Math.Round((td.estPosition() - MePosition).Length(), 1)}", FONT, textColor, textSize, TextAlignment.LEFT);
            sprite.Position = screenCenter + new Vector2(-halfScreenSize.X + 250, -halfScreenSize.Y + nowHeight);
            frame.Add(sprite);

        }
        nowHeight += lineHeight;

        uc = textColor; sele = false;
        if (droneSelectRow == -1) { uc = selectColor; sele = true; }
        sprite = MySprite.CreateText($"ALL", FONT, uc, textSize, TextAlignment.LEFT);
        sprite.Position = screenCenter + new Vector2(-halfScreenSize.X, -halfScreenSize.Y + nowHeight);
        frame.Add(sprite);

        if (sele && droneSelectColumn == 0) uc = selectColor;
        else uc = textColor;
        sprite = MySprite.CreateText($"TO", FONT, uc, textSize, TextAlignment.LEFT);
        sprite.Position = screenCenter + new Vector2(-halfScreenSize.X + 340, -halfScreenSize.Y + nowHeight);
        frame.Add(sprite);

        if (sele && droneSelectColumn == 1) uc = selectColor;
        else uc = textColor;
        sprite = MySprite.CreateText($"DK", FONT, uc, textSize, TextAlignment.LEFT);
        sprite.Position = screenCenter + new Vector2(-halfScreenSize.X + 370, -halfScreenSize.Y + nowHeight);
        frame.Add(sprite);

        if (sele && droneSelectColumn == 2) uc = selectColor;
        else uc = textColor;
        sprite = MySprite.CreateText($"F6", FONT, uc, textSize, TextAlignment.LEFT);
        sprite.Position = screenCenter + new Vector2(-halfScreenSize.X + 400, -halfScreenSize.Y + nowHeight);
        frame.Add(sprite);

        if (sele && droneSelectColumn == 3) uc = selectColor;
        else uc = textColor;
        sprite = MySprite.CreateText($"F5", FONT, uc, textSize, TextAlignment.LEFT);
        sprite.Position = screenCenter + new Vector2(-halfScreenSize.X + 430, -halfScreenSize.Y + nowHeight);
        frame.Add(sprite);

        if (sele && droneSelectColumn == 4) uc = selectColor;
        else uc = textColor;
        sprite = MySprite.CreateText($"B5", FONT, uc, textSize, TextAlignment.LEFT);
        sprite.Position = screenCenter + new Vector2(-halfScreenSize.X + 460, -halfScreenSize.Y + nowHeight);
        frame.Add(sprite);

        if (sele && droneSelectColumn == 5) uc = selectColor;
        else uc = textColor;
        sprite = MySprite.CreateText($"F7", FONT, uc, textSize, TextAlignment.LEFT);
        sprite.Position = screenCenter + new Vector2(-halfScreenSize.X + 490, -halfScreenSize.Y + nowHeight);
        frame.Add(sprite);

        // CODING

        //DrawRadarText(frame, screenCenter, viewportSize, minScale); 
    }

}

void GetTurretTargets()
{
    if (!isSetup) //setup error 
        return;

    radarSurface.ClearContacts();

    foreach (var block in turrets)
    {
        if (IsClosed(block))
            continue;

        if (block.HasTarget && !block.IsUnderControl)
        {
            var target = block.GetTargetedEntity();

            if (target.Relationship == MyRelationsBetweenPlayerAndBlock.Enemies)
                targetDataDict[target.EntityId] = new TargetData((Vector3D)(target.HitPosition != null ? target.HitPosition : target.Position), TargetRelation.Enemy, 0, target.Velocity, "", target.Orientation, 0,t, TargetData.PR_TURRET);
            else
                targetDataDict[target.EntityId] = new TargetData((Vector3D)(target.HitPosition != null ? target.HitPosition : target.Position), TargetRelation.Neutral, 0, target.Velocity, "", target.Orientation, 0,t, TargetData.PR_TURRET);
        }
    }

    foreach (var block in turretControls)
    {
        if (IsClosed(block))
            continue;

        if (block.HasTarget && !block.IsUnderControl)
        {
            var target = block.GetTargetedEntity();

            if (target.Relationship == MyRelationsBetweenPlayerAndBlock.Enemies)
                targetDataDict[target.EntityId] = new TargetData((Vector3D)(target.HitPosition != null ? target.HitPosition : target.Position), TargetRelation.Enemy, 0, target.Velocity, "", target.Orientation, 0,t, TargetData.PR_TURRET);
            else
                targetDataDict[target.EntityId] = new TargetData((Vector3D)(target.HitPosition != null ? target.HitPosition : target.Position), TargetRelation.Neutral, 0, target.Velocity, "", target.Orientation, 0,t, TargetData.PR_TURRET);
        }
    }


    lastActiveShipController = reference;

    foreach (var kvp in targetDataDict)
    {
        var targetData = kvp.Value;

        Color targetIconColor = enemyIconColor;
        Color targetElevationColor = enemyElevationColor;
        RadarSurface.Relation relation = RadarSurface.Relation.Hostile;
        switch (targetData.Relation)
        {
            case TargetRelation.Enemy:
                // Already set 
                break;

            case TargetRelation.Neutral:
                targetIconColor = neutralIconColor;
                targetElevationColor = neutralElevationColor;
                relation = RadarSurface.Relation.Neutral;
                break;

            case TargetRelation.Friendly:
                targetIconColor = allyIconColor;
                targetElevationColor = allyElevationColor;
                relation = RadarSurface.Relation.Allied;
                break;
        }

        bool isSelected = false;
        if (targetData is KRadarTargetData)
        {
            var kt = (KRadarTargetData)targetData;
            if (kt.isHighThreaten)
            {
                targetIconColor = enemyHighIconColor;
                targetElevationColor = enemyHighElevationColor;
            }
            if (kt.isDisabled)
            {
                targetIconColor = enemyDisableIconColor;
                targetElevationColor = enemyDisableElevationColor;
            }

            isSelected = kt.isSelected;
        }

        if (kvp.Key == Me.CubeGrid.EntityId)
            continue;

        if (Vector3D.DistanceSquared(targetData.estPosition(), reference.GetPosition()) < (MaxRange * MaxRange))
            radarSurface.AddContact(targetData.estPosition(), reference.WorldMatrix, targetIconColor, targetElevationColor, relation, isSelected);
    }

    NetworkTargets();

    // targetDataDict.Clear(); don't clear kradar target, it will clean by parseKRadarTarget function
    targetDataDict = targetDataDict.Where(x => x.Value is KRadarTargetData || t - x.Value.t < 300).ToDictionary(x => x.Key, x => x.Value);
}

void Draw(float startProportion, float endProportion)
{
    int start = (int)(startProportion * textSurfaces.Count);
    int end = (int)(endProportion * textSurfaces.Count);

    for (int i = start; i < end; ++i)
    {
        var textSurface = textSurfaces[i];
        radarSurface.DrawRadar(textSurface);
    }
}

void PrintDetailedInfo()
{
    Echo($"WMI Radar System Online{RunningSymbol()}\n(Version {VERSION} - {DATE})");
    Echo(debugInfo);
    Echo(debugInterval);
    Echo($"{lastSetupResult}");
    Echo($"Text surfaces: {textSurfaces.Count}");
    Echo($"\nReference seat:\n\"{(reference?.CustomName)}\"");
    //Echo($"\nNext refresh in {Math.Max(grabBlockAction.RunInterval - grabBlockAction.TimeSinceLastRun, 0):N0} seconds"); 
    Echo(runtimeTracker.Write());
}

void UpdateRadarRange()
{
    turretMaxRange = GetMaxTurretRange(turrets);
    radarSurface.Range = MaxRange;
}

//#endregion 

//#region Target Data Struct 
class TargetData
{
    public Vector3D Position;
    public TargetRelation Relation;
    public byte IngestCount;
    public Vector3D Velocity;
    public string Code;
    public MatrixD Mat;
    public double Round;
    public long t;

    public int priority;
    public static int PR_TURRET = 0;
    public static int PR_KRADAR = 1;
    public static int PR_ANTENNA = 2;

    public TargetData(Vector3D position, TargetRelation relation, byte ingestCount, Vector3D velocity, string code, MatrixD mat, double round, long t, int priority)
    {
        this.Position = position;
        this.Relation = relation;
        this.IngestCount = ingestCount;
        this.Velocity = velocity;
        this.Code = code;
        this.Mat = mat;
        this.Round = round;
        this.t = t;
        this.priority = priority;
    }

    public Vector3D estPosition() { 
        return this.Position + (1D/60)*(t-this.t)*this.Velocity;
    }

    public bool live() {
        return t - this.t < 10 * 60
          && (this.Position - MePosition).Length() < 2000;
    }
}

class KRadarTargetData : TargetData
{
    public long id;  // for fc system to remember the target, need to generate
    public long lastFrame; // if signal lost for some frames, we can still predicate their position
    public Vector3D realPos;
    public double size; // for size match
    public bool isHighThreaten = false;
    public bool isSelected = false;
    public bool isDisabled = false;

    public static double SIZE_RATIO_ERROR = 0.1;
    public static double POS_ABS_ERROR = 300;
    public static long MAX_LIVE_FRAME = 300;

    public static long maxId = 1;

    public KRadarTargetData(Vector3D position, TargetRelation relation, byte ingestCount, Vector3D velocity, string code, MatrixD mat, double round, long t, int priority)
        : base(position, relation, ingestCount, velocity, code, mat, round, t, priority)
    {

    }
}
//#endregion 
// 
//#region Radar Surface 
class RadarSurface
{
    public float Range;
    public enum Relation { None = 0, Allied = 1, Neutral = 2, Hostile = 3 } // Mutually exclusive switch 
    public readonly StringBuilder Debug = new StringBuilder();

    const string FONT = "Debug";
    const float HUD_TEXT_SIZE = 1.3f;
    const float RANGE_TEXT_SIZE = 1f;
    const float TGT_ELEVATION_LINE_WIDTH = 4f;

    Color _backColor;
    Color _lineColor;
    Color _planeColor;
    Color _textColor;
    float _projectionAngleDeg;
    float _radarProjectionCos;
    float _radarProjectionSin;
    int _allyCount = 0;
    int _neutralCount = 0;
    int _hostileCount = 0;

    readonly Vector2 TGT_ICON_SIZE = new Vector2(20, 20);
    readonly Vector2 SHIP_ICON_SIZE = new Vector2(32, 16);
    readonly List<TargetInfo> _targetList = new List<TargetInfo>();
    readonly List<TargetInfo> _targetsBelowPlane = new List<TargetInfo>();
    readonly List<TargetInfo> _targetsAbovePlane = new List<TargetInfo>();
    readonly Dictionary<Relation, string> _spriteMap = new Dictionary<Relation, string>()
{
    { Relation.None, "None" },
    { Relation.Allied, "SquareSimple" },
    { Relation.Neutral, "Triangle" },
    { Relation.Hostile, "Circle" },
};

    struct TargetInfo
    {
        public Vector3 Position;
        public Color IconColor;
        public Color ElevationColor;
        public string Icon;
        public bool isSelected;
    }

    public RadarSurface(Color backColor, Color lineColor, Color planeColor, Color textColor, float projectionAngleDeg, float range)
    {
        UpdateFields(backColor, lineColor, planeColor, textColor, projectionAngleDeg, range);
    }

    public void UpdateFields(Color backColor, Color lineColor, Color planeColor, Color textColor, float projectionAngleDeg, float range)
    {
        _backColor = backColor;
        _lineColor = lineColor;
        _planeColor = planeColor;
        _textColor = textColor;
        _projectionAngleDeg = projectionAngleDeg;
        Range = range;

        var rads = MathHelper.ToRadians(_projectionAngleDeg);
        _radarProjectionCos = (float)Math.Cos(rads);
        _radarProjectionSin = (float)Math.Sin(rads);
    }

    public void AddContact(Vector3D position, MatrixD worldMatrix, Color iconColor, Color elevationLineColor, Relation relation, bool isSelected = false)
    {
        var transformedDirection = Vector3D.TransformNormal(position - worldMatrix.Translation, Matrix.Transpose(worldMatrix));
        //transformedDirection = transformedDirection;
        var newlen = getLogLen(transformedDirection.Length());
        var r = newlen / transformedDirection.Length();
        transformedDirection *= r;
        float xOffset = (float)(transformedDirection.X);
        float yOffset = (float)(transformedDirection.Z);
        float zOffset = (float)(transformedDirection.Y);

        // log len
        //xOffset = (float) getLogLen(transformedDirection.X);
        //yOffset = (float) getLogLen(transformedDirection.Z);
        //zOffset = (float) getLogLen(transformedDirection.Y);

        string spriteName = "";
        _spriteMap.TryGetValue(relation, out spriteName);

        var targetInfo = new TargetInfo()
        {
            Position = new Vector3(xOffset, yOffset, zOffset),
            ElevationColor = elevationLineColor,
            IconColor = iconColor,
            Icon = spriteName,
            isSelected = isSelected,
        };

        switch (relation)
        {
            case Relation.Allied:
                ++_allyCount;
                break;

            case Relation.Neutral:
                ++_neutralCount;
                break;

            case Relation.Hostile:
                ++_hostileCount;
                break;
        }

        _targetList.Add(targetInfo);
    }

    public void SortContacts()
    {
        _targetsBelowPlane.Clear();
        _targetsAbovePlane.Clear();

        _targetList.Sort((a, b) => (a.Position.Y).CompareTo(b.Position.Y));

        foreach (var target in _targetList)
        {
            if (target.Position.Z >= 0)
                _targetsAbovePlane.Add(target);
            else
                _targetsBelowPlane.Add(target);
        }
    }

    public void ClearContacts()
    {
        _targetList.Clear();
        _targetsAbovePlane.Clear();
        _targetsBelowPlane.Clear();
        _allyCount = 0;
        _neutralCount = 0;
        _hostileCount = 0;
    }

    public void DrawRadar(IMyTextSurface surface)
    {
        surface.ContentType = ContentType.SCRIPT;
        surface.Script = "";

        Vector2 surfaceSize = surface.TextureSize;
        Vector2 screenCenter = surfaceSize * 0.5f;
        Vector2 viewportSize = surface.SurfaceSize;
        Vector2 scale = viewportSize / 512f;
        float minScale = Math.Min(scale.X, scale.Y);
        float sideLength = Math.Min(viewportSize.X, viewportSize.Y) - 12f;

        Vector2 radarPlaneSize = new Vector2(sideLength, sideLength * _radarProjectionCos);

        using (var frame = surface.DrawFrame())
        {
            // Fill background with background color 
            MySprite sprite = new MySprite(SpriteType.TEXTURE, "SquareSimple", color: _backColor);
            sprite.Position = screenCenter;
            frame.Add(sprite);

            // Bottom Icons 
            foreach (var targetInfo in _targetsBelowPlane)
            {
                DrawTargetIcon(frame, screenCenter, radarPlaneSize, targetInfo, minScale);
            }

            // Radar plane 
            DrawRadarPlane(frame, screenCenter, radarPlaneSize, minScale);

            // Top Icons 
            foreach (var targetInfo in _targetsAbovePlane)
            {
                DrawTargetIcon(frame, screenCenter, radarPlaneSize, targetInfo, minScale);
            }

            DrawRadarText(frame, screenCenter, viewportSize, minScale);
        }
    }

    void DrawRadarText(MySpriteDrawFrame frame, Vector2 screenCenter, Vector2 viewportSize, float scale)
    {
        MySprite sprite;
        float textSize = scale * HUD_TEXT_SIZE;
        Vector2 halfScreenSize = viewportSize * 0.5f;
        sprite = MySprite.CreateText($"WMI Radar System", FONT, _textColor, textSize, TextAlignment.CENTER);
        sprite.Position = screenCenter + new Vector2(0, -halfScreenSize.Y);
        frame.Add(sprite);

        //sprite = MySprite.CreateText("Contacts", FONT, _textColor, textSize, TextAlignment.CENTER); 
        //sprite.Position = screenCenter + new Vector2(0, halfScreenSize.Y - 80); 
        //frame.Add(sprite); 

        sprite = MySprite.CreateText($"Hostile: {_hostileCount}", FONT, _textColor, textSize, TextAlignment.CENTER);
        sprite.Position = screenCenter + new Vector2(-(halfScreenSize.X * 0.5f) + 10, halfScreenSize.Y - (90 * scale));
        frame.Add(sprite);

        sprite = MySprite.CreateText($"Neutral: {_neutralCount}", FONT, _textColor, textSize, TextAlignment.CENTER);
        sprite.Position = screenCenter + new Vector2((halfScreenSize.X * 0.5f) - 10, halfScreenSize.Y - (90 * scale));
        frame.Add(sprite);

        sprite = MySprite.CreateText($"Ally: {_allyCount}", FONT, _textColor, textSize, TextAlignment.CENTER);
        sprite.Position = screenCenter + new Vector2(0, halfScreenSize.Y - (40 * scale));
        frame.Add(sprite);
    }

    void DrawRadarPlane(MySpriteDrawFrame frame, Vector2 screenCenter, Vector2 radarPlaneSize, float scale)
    {
        MySprite sprite;

        // Transparent plane circle 
        sprite = new MySprite(SpriteType.TEXTURE, "Circle", size: radarPlaneSize, color: _planeColor);
        sprite.Position = screenCenter;
        frame.Add(sprite);

        // Inner circle 
        sprite = new MySprite(SpriteType.TEXTURE, "CircleHollow", size: radarPlaneSize * 0.5f, color: _lineColor);
        sprite.Position = screenCenter;
        frame.Add(sprite);

        // Outer circle 
        sprite = new MySprite(SpriteType.TEXTURE, "CircleHollow", size: radarPlaneSize, color: _lineColor);
        sprite.Position = screenCenter;
        frame.Add(sprite);

        // Ship location 
        sprite = new MySprite(SpriteType.TEXTURE, "Triangle", size: SHIP_ICON_SIZE * scale, color: _lineColor);
        sprite.Position = screenCenter;
        frame.Add(sprite);

        // Range markers 
        float textSize = RANGE_TEXT_SIZE * scale;
        // sprite = MySprite.CreateText($"    {Range * 0.5f:0}", "Debug", _textColor, textSize, TextAlignment.LEFT);
        sprite = MySprite.CreateText($"    {800f:0}", "Debug", _textColor, textSize, TextAlignment.LEFT);
        sprite.Position = screenCenter + new Vector2(radarPlaneSize.X * -0.25f, 0);
        frame.Add(sprite);

        sprite = MySprite.CreateText($"    {Range:0}", "Debug", _textColor, textSize, TextAlignment.LEFT);
        sprite.Position = screenCenter + new Vector2(radarPlaneSize.X * -0.5f, 0);
        frame.Add(sprite);
    }

    void DrawTargetIcon(MySpriteDrawFrame frame, Vector2 screenCenter, Vector2 radarPlaneSize, TargetInfo targetInfo, float scale)
    {
        Vector3 targetPosPixels = targetInfo.Position * new Vector3(1, _radarProjectionCos, _radarProjectionSin) * radarPlaneSize.X * 0.5f;

        Vector2 targetPosPlane = new Vector2(targetPosPixels.X, targetPosPixels.Y);
        Vector2 iconPos = targetPosPlane - targetPosPixels.Z * Vector2.UnitY;

        float elevationLineWidth = Math.Max(1f, TGT_ELEVATION_LINE_WIDTH * scale);
        MySprite elevationSprite = new MySprite(SpriteType.TEXTURE, "SquareSimple", color: targetInfo.ElevationColor, size: new Vector2(elevationLineWidth, targetPosPixels.Z));
        elevationSprite.Position = screenCenter + (iconPos + targetPosPlane) * 0.5f;

        Vector2 iconSize = TGT_ICON_SIZE * scale;
        MySprite iconSprite = new MySprite(SpriteType.TEXTURE, targetInfo.Icon, color: targetInfo.isSelected && (t / 5) % 2 == 0 ? enemyLockIconColor : targetInfo.IconColor, size: iconSize);
        iconSprite.Position = screenCenter + iconPos;

        iconSize.Y *= _radarProjectionCos;
        MySprite projectedIconSprite = new MySprite(SpriteType.TEXTURE, "Circle", color: targetInfo.ElevationColor, size: iconSize);
        projectedIconSprite.Position = screenCenter + targetPosPlane;

        bool showProjectedElevation = Math.Abs(iconPos.Y - targetPosPlane.Y) > iconSize.Y;

        // Changing the order of drawing based on if above or below radar plane 
        if (targetPosPixels.Z >= 0)
        {
            if (showProjectedElevation)
                frame.Add(projectedIconSprite);
            frame.Add(elevationSprite);
            frame.Add(iconSprite);
        }
        else
        {
            iconSprite.RotationOrScale = MathHelper.Pi;

            frame.Add(elevationSprite);
            frame.Add(iconSprite);
            if (showProjectedElevation)
                frame.Add(projectedIconSprite);
        }
        if (targetInfo.isSelected)
        {
            MySprite selectLine = new MySprite(SpriteType.TEXTURE, "SquareSimple", color: targetInfo.IconColor, size: new Vector2(elevationLineWidth * 5, elevationLineWidth));
            selectLine.Position = screenCenter + iconPos + new Vector2(0, iconSize.Y * 1.1f);
            frame.Add(selectLine);
        }
    }
}
//#endregion 

//#region Ini stuff 
void AddTextSurfaces(IMyTerminalBlock block, List<IMyTextSurface> textSurfaces)
{
    var textSurface = block as IMyTextSurface;
    if (textSurface != null)
    {
        textSurfaces.Add(textSurface);
        return;
    }

    var surfaceProvider = block as IMyTextSurfaceProvider;
    if (surfaceProvider == null)
        return;

    textSurfaceIni.Clear();
    textSurfaceIni.TryParse(block.CustomData);
    int surfaceCount = surfaceProvider.SurfaceCount;
    for (int i = 0; i < surfaceCount; ++i)
    {
        string iniKey = string.Format(INI_TEXT_SURFACE_TEMPLATE, i);
        bool display = textSurfaceIni.Get(INI_SECTION_TEXT_SURF_PROVIDER, iniKey).ToBoolean(i == 0 && !(block is IMyProgrammableBlock));
        if (display)
            textSurfaces.Add(surfaceProvider.GetSurface(i));

        textSurfaceIni.Set(INI_SECTION_TEXT_SURF_PROVIDER, iniKey, display);
    }

    block.CustomData = textSurfaceIni.ToString();
}

void WriteCustomDataIni()
{
    generalIni.Clear();
    generalIni.TryParse(Me.CustomData);

    generalIni.Set(INI_SECTION_GENERAL, INI_BCAST, broadcastIFF);
    generalIni.Set(INI_SECTION_GENERAL, INI_NETWORK, networkTargets);
    generalIni.Set(INI_SECTION_GENERAL, INI_USE_RANGE_OVERRIDE, useRangeOverride);
    generalIni.Set(INI_SECTION_GENERAL, INI_RANGE_OVERRIDE, rangeOverride);
    generalIni.Set(INI_SECTION_GENERAL, INI_PROJ_ANGLE, projectionAngle);

    generalIni.Set(INI_SECTION_GENERAL, INI_MASTER_CODE, MOTHER_CODE);
    generalIni.Set(INI_SECTION_GENERAL, INI_SLAVE_CODE, SON_CODE);
    generalIni.Set(INI_SECTION_GENERAL, INI_START_WHEN_COMPILED, startWhenCompiled);

    generalIni.Set(INI_SECTION_GENERAL, INI_KRADAR_PANEL_NAME, KRADAR_PANEL_NAME);
    generalIni.Set(INI_SECTION_GENERAL, INI_AIM_MARK_NAME, AIM_MARK_NAME);
    generalIni.Set(INI_SECTION_GENERAL, INI_ALERT_RANGE, ALERT_RANGE);

    MyIniHelper.SetColorChar(INI_SECTION_COLORS, INI_TEXT, textColor, generalIni);
    MyIniHelper.SetColorChar(INI_SECTION_COLORS, INI_BACKGROUND, backColor, generalIni);
    MyIniHelper.SetColorChar(INI_SECTION_COLORS, INI_RADAR_LINES, lineColor, generalIni);
    MyIniHelper.SetColorChar(INI_SECTION_COLORS, INI_PLANE, planeColor, generalIni);
    MyIniHelper.SetColorChar(INI_SECTION_COLORS, INI_ENEMY, enemyIconColor, generalIni);
    MyIniHelper.SetColorChar(INI_SECTION_COLORS, INI_ENEMY_ELEVATION, enemyElevationColor, generalIni);
    MyIniHelper.SetColorChar(INI_SECTION_COLORS, INI_NEUTRAL, neutralIconColor, generalIni);
    MyIniHelper.SetColorChar(INI_SECTION_COLORS, INI_NEUTRAL_ELEVATION, neutralElevationColor, generalIni);
    MyIniHelper.SetColorChar(INI_SECTION_COLORS, INI_FRIENDLY, allyIconColor, generalIni);
    MyIniHelper.SetColorChar(INI_SECTION_COLORS, INI_FRIENDLY_ELEVATION, allyElevationColor, generalIni);
    generalIni.SetSectionComment(INI_SECTION_COLORS, "Colors are defined with RGBAlpha color codes where\nvalues can range from 0,0,0,0 [transparent] to 255,255,255,255 [white].");

    Me.CustomData = generalIni.ToString();
}

void ParseCustomDataIni()
{
    generalIni.Clear();
    generalIni.TryParse(Me.CustomData);

    broadcastIFF = generalIni.Get(INI_SECTION_GENERAL, INI_BCAST).ToBoolean(broadcastIFF);
    MOTHER_CODE = generalIni.Get(INI_SECTION_GENERAL, INI_MASTER_CODE).ToString(MOTHER_CODE);
    SON_CODE = generalIni.Get(INI_SECTION_GENERAL, INI_SLAVE_CODE).ToString(SON_CODE);
    networkTargets = generalIni.Get(INI_SECTION_GENERAL, INI_NETWORK).ToBoolean(networkTargets);
    useRangeOverride = generalIni.Get(INI_SECTION_GENERAL, INI_USE_RANGE_OVERRIDE).ToBoolean(useRangeOverride);
    rangeOverride = generalIni.Get(INI_SECTION_GENERAL, INI_RANGE_OVERRIDE).ToSingle(rangeOverride);
    projectionAngle = generalIni.Get(INI_SECTION_GENERAL, INI_PROJ_ANGLE).ToSingle(projectionAngle);
    startWhenCompiled = generalIni.Get(INI_SECTION_GENERAL, INI_START_WHEN_COMPILED).ToBoolean(startWhenCompiled);
    Echo("startWhenCompiled " + startWhenCompiled);
    KRADAR_PANEL_NAME = generalIni.Get(INI_SECTION_GENERAL, INI_KRADAR_PANEL_NAME).ToString(KRADAR_PANEL_NAME);
    AIM_MARK_NAME = generalIni.Get(INI_SECTION_GENERAL, INI_AIM_MARK_NAME).ToString(AIM_MARK_NAME);
    double.TryParse(generalIni.Get(INI_SECTION_GENERAL, INI_ALERT_RANGE).ToString(ALERT_RANGE+""), out ALERT_RANGE);

    textColor = MyIniHelper.GetColorChar(INI_SECTION_COLORS, INI_TEXT, generalIni, textColor);
    backColor = MyIniHelper.GetColorChar(INI_SECTION_COLORS, INI_BACKGROUND, generalIni, backColor);
    lineColor = MyIniHelper.GetColorChar(INI_SECTION_COLORS, INI_RADAR_LINES, generalIni, lineColor);
    planeColor = MyIniHelper.GetColorChar(INI_SECTION_COLORS, INI_PLANE, generalIni, planeColor);
    enemyIconColor = MyIniHelper.GetColorChar(INI_SECTION_COLORS, INI_ENEMY, generalIni, enemyIconColor);
    enemyElevationColor = MyIniHelper.GetColorChar(INI_SECTION_COLORS, INI_ENEMY_ELEVATION, generalIni, enemyElevationColor);
    neutralIconColor = MyIniHelper.GetColorChar(INI_SECTION_COLORS, INI_NEUTRAL, generalIni, neutralIconColor);
    neutralElevationColor = MyIniHelper.GetColorChar(INI_SECTION_COLORS, INI_NEUTRAL_ELEVATION, generalIni, neutralElevationColor);
    allyIconColor = MyIniHelper.GetColorChar(INI_SECTION_COLORS, INI_FRIENDLY, generalIni, allyIconColor);
    allyElevationColor = MyIniHelper.GetColorChar(INI_SECTION_COLORS, INI_FRIENDLY_ELEVATION, generalIni, allyElevationColor);

    WriteCustomDataIni();

    if (radarSurface != null)
    {
        radarSurface.UpdateFields(backColor, lineColor, planeColor, textColor, projectionAngle, MaxRange);
    }
}

public static class MyIniHelper
{
    /// <summary> 
    /// Adds a color character to a MyIni object 
    /// </summary> 
    public static void SetColorChar(string sectionName, string itemName, Color color, MyIni ini)
    {

        string colorString = string.Format("{0}, {1}, {2}, {3}", color.R, color.G, color.B, color.A);

        ini.Set(sectionName, itemName, colorString);
    }

    /// <summary> 
    /// Parses a MyIni for a color character 
    /// </summary> 
    public static Color GetColorChar(string sectionName, string itemName, MyIni ini, Color? defaultChar = null)
    {
        string rgbString = ini.Get(sectionName, itemName).ToString("null");
        string[] rgbSplit = rgbString.Split(',');

        int r = 0, g = 0, b = 0, a = 0;
        if (rgbSplit.Length != 4)
        {
            if (defaultChar.HasValue)
                return defaultChar.Value;
            else
                return Color.Transparent;
        }

        int.TryParse(rgbSplit[0].Trim(), out r);
        int.TryParse(rgbSplit[1].Trim(), out g);
        int.TryParse(rgbSplit[2].Trim(), out b);
        bool hasAlpha = int.TryParse(rgbSplit[3].Trim(), out a);
        if (!hasAlpha)
            a = 255;

        r = MathHelper.Clamp(r, 0, 255);
        g = MathHelper.Clamp(g, 0, 255);
        b = MathHelper.Clamp(b, 0, 255);
        a = MathHelper.Clamp(a, 0, 255);

        return new Color(r, g, b, a);
    }
}
//#endregion 
// 
//#region General Functions 
//Whip's Running Symbol Method v8 
//• 
int runningSymbolVariant = 0;
int runningSymbolCount = 0;
const int increment = 1;
string[] runningSymbols = new string[] { ".", "..", "...", "....", "...", "..", ".", "" };

string RunningSymbol()
{
    if (runningSymbolCount >= increment)
    {
        runningSymbolCount = 0;
        runningSymbolVariant++;
        if (runningSymbolVariant >= runningSymbols.Length)
            runningSymbolVariant = 0;
    }
    runningSymbolCount++;
    return runningSymbols[runningSymbolVariant];
}

IMyShipController GetControlledShipController(List<IMyShipController> SCs)
{
    foreach (IMyShipController thisController in SCs)
    {
        if (IsClosed(thisController))
            continue;

        if (thisController.IsUnderControl && thisController.CanControlShip)
            return thisController;
    }

    return null;
}

float GetMaxTurretRange(List<IMyLargeTurretBase> turrets)
{
    float maxRange = 0;
    foreach (var block in turrets)
    {
        if (!block.IsWorking)
            continue;

        float thisRange = block.Range;
        if (thisRange > maxRange)
        {
            maxRange = thisRange;
        }
    }
    return maxRange;
}

public static bool IsClosed(IMyTerminalBlock block)
{
    return block.WorldMatrix == MatrixD.Identity;
}

public static bool StringContains(string source, string toCheck, StringComparison comp = StringComparison.OrdinalIgnoreCase)
{
    return source?.IndexOf(toCheck, comp) >= 0;
}
//#endregion 
// 
//#region Block Fetching 
bool PopulateLists(IMyTerminalBlock block)
{
    if (!block.IsSameConstructAs(Me))
        return false;

    if (StringContains(block.CustomName, textPanelName))
    {
        AddTextSurfaces(block, textSurfaces);
    }

    if (StringContains(block.CustomName, cmdPanelName))
    {
        AddTextSurfaces(block, cmdSurfaces);
    }

    var turret = block as IMyLargeTurretBase;
    if (turret != null)
    {
        turrets.Add(turret); 
        return false;
    }

    var controller = block as IMyShipController;
    if (controller != null)
    {
        controllers.Add(controller);
        return false;
    }

    if (block is IMyTurretControlBlock) {
        turretControls.Add((IMyTurretControlBlock)block);
        return false;
    }

    return false;
}

void GrabBlocks()
{
    turrets.Clear();
    turretControls.Clear();
    controllers.Clear();
    textSurfaces.Clear();

    GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(null, PopulateLists);

    if (turrets.Count == 0 && turretControls.Count == 0)
        Log.Warning($"No turrets found. You will only be able to see targets that are broadcast by allies.");

    if (textSurfaces.Count == 0)
        Log.Error($"No text panels or text surface providers with name tag '{textPanelName}' were found.");

    if (controllers.Count == 0)
        Log.Error($"No ship controllers were found.");

    lastSetupResult = Log.Write();

    if (textSurfaces.Count == 0)
        isSetup = false;
    else
    {
        isSetup = true;
        ParseCustomDataIni();
    }

    radarSurface = new RadarSurface(backColor, lineColor, planeColor, textColor, projectionAngle, MaxRange);

    runtimeTracker = new RuntimeTracker(this);

    // Scheduler creation 
    scheduler = new Scheduler(this);
    //grabBlockAction = new ScheduledAction(GrabBlocks, 0.1); 
    //scheduler.AddScheduledAction(grabBlockAction); 
    scheduler.AddScheduledAction(UpdateRadarRange, 1);
    //scheduler.AddScheduledAction(PrintDetailedInfo, 0.01); 

    scheduler.AddQueuedAction(GetTurretTargets, cycleTime);                                             // cycle 1 
    scheduler.AddQueuedAction(radarSurface.SortContacts, cycleTime);                                    // cycle 2 

    float step = 1f / 7f;
    scheduler.AddQueuedAction(() => Draw(0 * step, 1 * step), cycleTime);                               // cycle 3 
    scheduler.AddQueuedAction(() => Draw(1 * step, 2 * step), cycleTime);                               // cycle 4 
    scheduler.AddQueuedAction(() => Draw(2 * step, 3 * step), cycleTime);                               // cycle 5 
    scheduler.AddQueuedAction(() => Draw(3 * step, 4 * step), cycleTime);                               // cycle 6 
    scheduler.AddQueuedAction(() => Draw(4 * step, 5 * step), cycleTime);                               // cycle 7 
    scheduler.AddQueuedAction(() => Draw(5 * step, 6 * step), cycleTime);                               // cycle 8 
    scheduler.AddQueuedAction(() => Draw(6 * step, 7 * step), cycleTime);                               // cycle 9 
    scheduler.AddQueuedAction(radarSurface.ClearContacts, cycleTime);                                   // cycle 10 

    scheduler.AddQueuedAction(DrawCmdPanel, cycleTime);
}
//#endregion 
// 
//#region Scheduler 
/// <summary> 
/// Class for scheduling actions to occur at specific frequencies. Actions can be updated in parallel or in sequence (queued). 
/// </summary> 
public class Scheduler
{
    readonly List<ScheduledAction> _scheduledActions = new List<ScheduledAction>();
    readonly List<ScheduledAction> _actionsToDispose = new List<ScheduledAction>();
    Queue<ScheduledAction> _queuedActions = new Queue<ScheduledAction>();
    const double runtimeToRealtime = 1.0 / 0.96;
    private readonly Program _program;
    private ScheduledAction _currentlyQueuedAction = null;

    /// <summary> 
    /// Constructs a scheduler object with timing based on the runtime of the input program. 
    /// </summary> 
    /// <param name="program"></param> 
    public Scheduler(Program program)
    {
        _program = program;
    }

    /// <summary> 
    /// Updates all ScheduledAcions in the schedule and the queue. 
    /// </summary> 
    public void Update()
    {
        double deltaTime = Math.Max(0, _program.Runtime.LastRunTimeMs * 0.1 * runtimeToRealtime);
        //_program.Echo("a "+deltaTime);

        _actionsToDispose.Clear();
        foreach (ScheduledAction action in _scheduledActions)
        {
            action.Update(deltaTime);
            if (action.JustRan && action.DisposeAfterRun)
            {
                _actionsToDispose.Add(action);
            }
        }

        // Remove all actions that we should dispose 
        _scheduledActions.RemoveAll((x) => _actionsToDispose.Contains(x));

        if (_currentlyQueuedAction == null)
        {
            // If queue is not empty, populate current queued action 
            if (_queuedActions.Count != 0)
                _currentlyQueuedAction = _queuedActions.Dequeue();
        }

        // If queued action is populated 
        if (_currentlyQueuedAction != null)
        {
            _currentlyQueuedAction.Update(deltaTime);
            if (_currentlyQueuedAction.JustRan)
            {
                // If we should recycle, add it to the end of the queue 
                if (!_currentlyQueuedAction.DisposeAfterRun)
                    _queuedActions.Enqueue(_currentlyQueuedAction);

                // Set the queued action to null for the next cycle 
                _currentlyQueuedAction = null;
            }
        }
    }

    /// <summary> 
    /// Adds an Action to the schedule. All actions are updated each update call. 
    /// </summary> 
    /// <param name="action"></param> 
    /// <param name="updateFrequency"></param> 
    /// <param name="disposeAfterRun"></param> 
    public void AddScheduledAction(Action action, double updateFrequency, bool disposeAfterRun = false)
    {
        ScheduledAction scheduledAction = new ScheduledAction(action, updateFrequency, disposeAfterRun);
        _scheduledActions.Add(scheduledAction);
    }

    /// <summary> 
    /// Adds a ScheduledAction to the schedule. All actions are updated each update call. 
    /// </summary> 
    /// <param name="scheduledAction"></param> 
    public void AddScheduledAction(ScheduledAction scheduledAction)
    {
        _scheduledActions.Add(scheduledAction);
    }

    /// <summary> 
    /// Adds an Action to the queue. Queue is FIFO. 
    /// </summary> 
    /// <param name="action"></param> 
    /// <param name="updateInterval"></param> 
    /// <param name="disposeAfterRun"></param> 
    public void AddQueuedAction(Action action, double updateInterval, bool disposeAfterRun = false)
    {
        if (updateInterval <= 0)
        {
            updateInterval = 0.001; // avoids divide by zero 
        }
        ScheduledAction scheduledAction = new ScheduledAction(action, 1.0 / updateInterval, disposeAfterRun);
        _queuedActions.Enqueue(scheduledAction);
    }

    /// <summary> 
    /// Adds a ScheduledAction to the queue. Queue is FIFO. 
    /// </summary> 
    /// <param name="scheduledAction"></param> 
    public void AddQueuedAction(ScheduledAction scheduledAction)
    {
        _queuedActions.Enqueue(scheduledAction);
    }
}

public class ScheduledAction
{
    public bool JustRan { get; private set; } = false;
    public bool DisposeAfterRun { get; private set; } = false;
    public double TimeSinceLastRun { get; private set; } = 0;
    public readonly double RunInterval;

    private readonly double _runFrequency;
    private readonly Action _action;
    protected bool _justRun = false;

    /// <summary> 
    /// Class for scheduling an action to occur at a specified frequency (in Hz). 
    /// </summary> 
    /// <param name="action">Action to run</param> 
    /// <param name="runFrequency">How often to run in Hz</param> 
    public ScheduledAction(Action action, double runFrequency, bool removeAfterRun = false)
    {
        _action = action;
        _runFrequency = runFrequency;
        RunInterval = 1.0 / _runFrequency;
        DisposeAfterRun = removeAfterRun;
    }

    public virtual void Update(double deltaTime)
    {
        TimeSinceLastRun += deltaTime;

        if (TimeSinceLastRun >= RunInterval)
        {
            _action.Invoke();
            TimeSinceLastRun = 0;

            JustRan = true;
        }
        else
        {
            JustRan = false;
        }
    }
}
//#endregion 
// 
//#region Script Logging 
public static class Log
{
    static StringBuilder _builder = new StringBuilder();
    static List<string> _errorList = new List<string>();
    static List<string> _warningList = new List<string>();
    static List<string> _infoList = new List<string>();
    const int _logWidth = 530; //chars, conservative estimate 

    public static void Clear()
    {
        _builder.Clear();
        _errorList.Clear();
        _warningList.Clear();
        _infoList.Clear();
    }

    public static void Error(string text)
    {
        _errorList.Add(text);
    }

    public static void Warning(string text)
    {
        _warningList.Add(text);
    }

    public static void Info(string text)
    {
        _infoList.Add(text);
    }

    public static string Write(bool preserveLog = false)
    {
        //WriteLine($"Error count: {_errorList.Count}"); 
        //WriteLine($"Warning count: {_warningList.Count}"); 
        //WriteLine($"Info count: {_infoList.Count}"); 

        if (_errorList.Count != 0 && _warningList.Count != 0 && _infoList.Count != 0)
            WriteLine("");

        if (_errorList.Count != 0)
        {
            for (int i = 0; i < _errorList.Count; i++)
            {
                WriteLine("");
                WriteElememt(i + 1, "ERROR", _errorList[i]);
                //if (i < _errorList.Count - 1) 
            }
        }

        if (_warningList.Count != 0)
        {
            for (int i = 0; i < _warningList.Count; i++)
            {
                WriteLine("");
                WriteElememt(i + 1, "WARNING", _warningList[i]);
                //if (i < _warningList.Count - 1) 
            }
        }

        if (_infoList.Count != 0)
        {
            for (int i = 0; i < _infoList.Count; i++)
            {
                WriteLine("");
                WriteElememt(i + 1, "Info", _infoList[i]);
                //if (i < _infoList.Count - 1) 
            }
        }

        string output = _builder.ToString();

        if (!preserveLog)
            Clear();

        return output;
    }

    private static void WriteElememt(int index, string header, string content)
    {
        WriteLine($"{header} {index}:");

        string wrappedContent = TextHelper.WrapText(content, 1, _logWidth);
        string[] wrappedSplit = wrappedContent.Split('\n');

        foreach (var line in wrappedSplit)
        {
            _builder.Append("  ").Append(line).Append('\n');
        }
    }

    private static void WriteLine(string text)
    {
        _builder.Append(text).Append('\n');
    }
}

// Whip's TextHelper Class v2 
public class TextHelper
{
    static StringBuilder textSB = new StringBuilder();
    const float adjustedPixelWidth = (512f / 0.778378367f);
    const int monospaceCharWidth = 24 + 1; //accounting for spacer 
    const int spaceWidth = 8;

    #region bigass dictionary 
    static Dictionary<char, int> _charWidths = new Dictionary<char, int>()
{
{'.', 9},
{'!', 8},
{'?', 18},
{',', 9},
{':', 9},
{';', 9},
{'"', 10},
{'\'', 6},
{'+', 18},
{'-', 10},

{'(', 9},
{')', 9},
{'[', 9},
{']', 9},
{'{', 9},
{'}', 9},

{'\\', 12},
{'/', 14},
{'_', 15},
{'|', 6},

{'~', 18},
{'<', 18},
{'>', 18},
{'=', 18},

{'0', 19},
{'1', 9},
{'2', 19},
{'3', 17},
{'4', 19},
{'5', 19},
{'6', 19},
{'7', 16},
{'8', 19},
{'9', 19},

{'A', 21},
{'B', 21},
{'C', 19},
{'D', 21},
{'E', 18},
{'F', 17},
{'G', 20},
{'H', 20},
{'I', 8},
{'J', 16},
{'K', 17},
{'L', 15},
{'M', 26},
{'N', 21},
{'O', 21},
{'P', 20},
{'Q', 21},
{'R', 21},
{'S', 21},
{'T', 17},
{'U', 20},
{'V', 20},
{'W', 31},
{'X', 19},
{'Y', 20},
{'Z', 19},

{'a', 17},
{'b', 17},
{'c', 16},
{'d', 17},
{'e', 17},
{'f', 9},
{'g', 17},
{'h', 17},
{'i', 8},
{'j', 8},
{'k', 17},
{'l', 8},
{'m', 27},
{'n', 17},
{'o', 17},
{'p', 17},
{'q', 17},
{'r', 10},
{'s', 17},
{'t', 9},
{'u', 17},
{'v', 15},
{'w', 27},
{'x', 15},
{'y', 17},
{'z', 16}
};
    #endregion

    public static int GetWordWidth(string word)
    {
        int wordWidth = 0;
        foreach (char c in word)
        {
            int thisWidth = 0;
            bool contains = _charWidths.TryGetValue(c, out thisWidth);
            if (!contains)
                thisWidth = monospaceCharWidth; //conservative estimate 

            wordWidth += (thisWidth + 1);
        }
        return wordWidth;
    }

    public static string WrapText(string text, float fontSize, float pixelWidth = adjustedPixelWidth)
    {
        textSB.Clear();
        var words = text.Split(' ');
        var screenWidth = (pixelWidth / fontSize);
        int currentLineWidth = 0;
        foreach (var word in words)
        {
            if (currentLineWidth == 0)
            {
                textSB.Append($"{word}");
                currentLineWidth += GetWordWidth(word);
                continue;
            }

            currentLineWidth += spaceWidth + GetWordWidth(word);
            if (currentLineWidth > screenWidth) //new line 
            {
                currentLineWidth = GetWordWidth(word);
                textSB.Append($"\n{word}");
            }
            else
            {
                textSB.Append($" {word}");
            }
        }

        return textSB.ToString();
    }
}
//#endregion 
// 
//#region Runtime Tracking 
/// <summary> 
/// Class that tracks runtime history. 
/// </summary> 
public class RuntimeTracker
{
    public int Capacity { get; set; }
    public double Sensitivity { get; set; }
    public double MaxRuntime { get; private set; }
    public double MaxInstructions { get; private set; }
    public double AverageRuntime { get; private set; }
    public double AverageInstructions { get; private set; }

    private readonly Queue<double> _runtimes = new Queue<double>();
    private readonly Queue<double> _instructions = new Queue<double>();
    private readonly StringBuilder _sb = new StringBuilder();
    private readonly int _instructionLimit;
    private readonly Program _program;

    public RuntimeTracker(Program program, int capacity = 100, double sensitivity = 0.01)
    {
        _program = program;
        Capacity = capacity;
        Sensitivity = sensitivity;
        _instructionLimit = _program.Runtime.MaxInstructionCount;
    }

    public void AddRuntime()
    {
        double runtime = _program.Runtime.LastRunTimeMs;
        AverageRuntime = Sensitivity * (runtime - AverageRuntime) + AverageRuntime;

        _runtimes.Enqueue(runtime);
        if (_runtimes.Count == Capacity)
        {
            _runtimes.Dequeue();
        }

        MaxRuntime = _runtimes.Max();
    }

    public void AddInstructions()
    {
        double instructions = _program.Runtime.CurrentInstructionCount;
        AverageInstructions = Sensitivity * (instructions - AverageInstructions) + AverageInstructions;

        _instructions.Enqueue(instructions);
        if (_instructions.Count == Capacity)
        {
            _instructions.Dequeue();
        }

        MaxInstructions = _instructions.Max();
    }

    public string Write()
    {
        _sb.Clear();
        _sb.AppendLine("\n_____________________________\nGeneral Runtime Info\n");
        _sb.AppendLine($"Avg instructions: {AverageInstructions:n2}");
        _sb.AppendLine($"Max instructions: {MaxInstructions:n0}");
        _sb.AppendLine($"Avg complexity: {MaxInstructions / _instructionLimit:0.000}%");
        _sb.AppendLine($"Avg runtime: {AverageRuntime:n4} ms");
        _sb.AppendLine($"Max runtime: {MaxRuntime:n4} ms");
        return _sb.ToString();
    }
}
//#endregion 
//
// util
T getBlockByName<T>(string name, bool sameGrid = true, bool sameName = false)
where T : class, IMyTerminalBlock
{
    List<T> blocks = new List<T>();

    GridTerminalSystem.GetBlocksOfType<T>(blocks, b => (
    !sameGrid || (sameGrid && b.CubeGrid == Me.CubeGrid)
    ) && (
    (sameName && b.CustomName == name) ||
    (!sameName && b.CustomName.Contains(name))
    )
    );
    if (blocks.Count > 0) return blocks[0];
    return null;
}

List<T> getBlockListByName<T>(string name, bool sameGrid = true, bool sameName = false)
where T : class, IMyTerminalBlock
{
    List<T> blocks = new List<T>();

    GridTerminalSystem.GetBlocksOfType<T>(blocks, b => (
    !sameGrid || (sameGrid && b.CubeGrid == Me.CubeGrid)
    ) && (
    (sameName && b.CustomName == name) ||
    (!sameName && b.CustomName.Contains(name))
    )
    );

    return blocks;
}

void init()
{
    GrabBlocks();

    // Define reference ship controller
    reference = getBlockByName<IMyShipController>(COCKPIT_NAME);
    //reference = GetControlledShipController(controllers); // Primary, get active controller 
    if (reference == null)
    {
        if (lastActiveShipController != null)
        {
            // Backup, use last active controller 
            reference = lastActiveShipController;
        }
        else if (reference == null && controllers.Count != 0)
        {
            // Last case, resort to the first controller in the list 
            reference = controllers[0];
        }
        else
        {
            return;
        }
    }
    dcsComputer = getBlockByName<IMyProgrammableBlock>(DCS_NAME);
    fcsComputer = getBlockByName<IMyProgrammableBlock>(FCS_NAME);
    fcsReference = getBlockByName<IMyTerminalBlock>(FCS_REFERENCE_NAME, false);
    var tmpPanel = getBlockByName<IMyTerminalBlock>(KRADAR_PANEL_NAME,false);
    if (tmpPanel is IMyTextPanel)
    {
        kradarPanel = (IMyTextSurface)tmpPanel;
    }
    aimMark = getBlockByName<IMyTerminalBlock>(AIM_MARK_NAME);
    
    inited = true;
}

void decodeMessage(string msg, out string motherCode, out MatrixD matrix, out Vector3D velocity, out double r)
{
    String[] kv = msg.Split(':');
    motherCode = kv[0];
    String[] args;
    args = kv[1].Split(',');
    matrix = new MatrixD(Convert.ToDouble(args[0]), Convert.ToDouble(args[1]), Convert.ToDouble(args[2]), Convert.ToDouble(args[3]),
    Convert.ToDouble(args[4]), Convert.ToDouble(args[5]), Convert.ToDouble(args[6]), Convert.ToDouble(args[7]),
    Convert.ToDouble(args[8]), Convert.ToDouble(args[9]), Convert.ToDouble(args[10]), Convert.ToDouble(args[11]),
    Convert.ToDouble(args[12]), Convert.ToDouble(args[13]), Convert.ToDouble(args[14]), Convert.ToDouble(args[15]));

    Vector3D shipPosition = new Vector3D(matrix.M41, matrix.M42, matrix.M43);

    MatrixD shipLookAtMatrix = MatrixD.CreateLookAt(new Vector3D(0, 0, 0), matrix.Forward, matrix.Up);

    velocity = new Vector3D(Convert.ToDouble(args[16]), Convert.ToDouble(args[17]), Convert.ToDouble(args[18]));

    r = Convert.ToDouble(args[19]);
}

string encodeMessage(string motherCode, MatrixD refWorldMatrix, Vector3D speed, double r)
{
    string message = motherCode + ":" + refWorldMatrix.M11 + "," + refWorldMatrix.M12 + "," + refWorldMatrix.M13 + "," + refWorldMatrix.M14 + "," +
    refWorldMatrix.M21 + "," + refWorldMatrix.M22 + "," + refWorldMatrix.M23 + "," + refWorldMatrix.M24 + "," +
    refWorldMatrix.M31 + "," + refWorldMatrix.M32 + "," + refWorldMatrix.M33 + "," + refWorldMatrix.M34 + "," +
    MePosition.X + "," + MePosition.Y + "," + MePosition.Z + "," + refWorldMatrix.M44 + "," +
    speed.X + "," + speed.Y + "," + speed.Z + "," + r;
    return message;
}


void PlayAction(IMyTerminalBlock block, String action, String cmd = null)
{
    if (block == null) return;
    if (cmd != null)
    {
        TerminalActionParameter tap = TerminalActionParameter.Deserialize(cmd, cmd.GetTypeCode());
        List<TerminalActionParameter> argumentList = new List<TerminalActionParameter>();
        argumentList.Add(tap);

        if (block != null)
        {
            block.GetActionWithName(action).Apply(block, argumentList);
        }
    }
    else
    {
        block.GetActionWithName(action).Apply(block);
    }
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

Vector3D LockTargetVelocity = Vector3D.Zero;
Vector3D LockTargetPosition = Vector3D.Zero;
Vector3D radarHighThreatPosition = Vector3D.Zero;
void parseFcsTarget()
{
    if (fcsComputer == null) return;
    // parse locktarget from fcs

    CustomConfiguration cfgTarget = new CustomConfiguration(fcsComputer);
    cfgTarget.Load();

    string tmpS = "";
    cfgTarget.Get("Position", ref tmpS);
    Vector3D.TryParse(tmpS, out LockTargetPosition);

    cfgTarget.Get("Velocity", ref tmpS);
    Vector3D.TryParse(tmpS, out LockTargetVelocity);

    cfgTarget.Get("radarHighThreatPosition", ref tmpS);
    Vector3D.TryParse(tmpS, out radarHighThreatPosition);

    int tmpI = 0;
    cfgTarget.Get("TargetCount", ref tmpI);

    int targetCount = tmpI;
    for (int i = 0; i < targetCount; i++)
    {
        Vector3D tmpP, tmpV;
        cfgTarget.Get("Position" + i, ref tmpS);
        Vector3D.TryParse(tmpS, out tmpP);
        cfgTarget.Get("Velocity" + i, ref tmpS);
        Vector3D.TryParse(tmpS, out tmpV);
        long tmpL;
        cfgTarget.Get("EntityId" + i, ref tmpS);
        long.TryParse(tmpS, out tmpL);
        putTargetDict(tmpL, new TargetData(tmpP, TargetRelation.Enemy, MAX_REBROADCAST_INGEST_COUNT, tmpV, MOTHER_CODE, new MatrixD(), 0,t, TargetData.PR_KRADAR));
    }
}

void putTargetDict(long eid, TargetData tar) {
    if (targetDataDict.ContainsKey(eid) && targetDataDict[eid].priority < tar.priority) {
        targetDataDict[eid].t = t;
        return;
    }
    targetDataDict[eid] = tar;
}

class KRadarElement
{
    public Vector3D pos;
    public double dis;
    public double size;
    public bool occupied = false;
    public Vector3D ve;
    public bool haveVe = false;

    public double error; // update for each target
}
List<KRadarElement> kradarPosList = new List<KRadarElement>();

void parseKRadarTarget()
{
    if (kradarPanel == null) return;
    var data = kradarPanel.GetText();
    if(data == null) data = "";
    string[] lines = data.Split('\n');
    
    bool firstLine = true;
    foreach (var l in lines)
    {
        if(firstLine) { 
            long updateTimestamp;
            bool get = long.TryParse(l, out updateTimestamp);
            if (!get) break;
            if (updateTimestamp == kradarLastUpdate) break;
            kradarLastUpdate = updateTimestamp;
            // 0116 求本地frame与kradarframe的平均差
            debugInterval = "";
            
            firstLine = false;
            kradarPosList.Clear();
            continue;
        }
        if (l == null || l.Length == 0) continue;
        string[] fields = l.Split(':');
        if (fields.Count() < 4) continue;

        KRadarElement ke = new KRadarElement();
        double x, y, z;
        double size;
        bool allRead = true;
        allRead &= double.TryParse(fields[0], out size);
        allRead &= double.TryParse(fields[1], out x);
        allRead &= double.TryParse(fields[2], out y);
        allRead &= double.TryParse(fields[3], out z);
        if (!allRead)
        {
            continue;
        }
        ke.pos = new Vector3D(x, y, z);
        ke.size = size;
        ke.dis = (ke.pos - MePosition).Length();
        ke.occupied = false;

        if (fields.Count() >= 7)
        {
            double vx, vy, vz;
            bool allVRead = true;
            allVRead &= double.TryParse(fields[4], out vx);
            allVRead &= double.TryParse(fields[5], out vy);
            allVRead &= double.TryParse(fields[6], out vz);
            if (allVRead)
            {
                ke.ve = new Vector3D(vx, vy, vz);
                ke.haveVe = true;
            }
            // 0116 对ke的pos直接进行基于kradarFrameOffset 的估算
            debugInt($"kv: {ke.ve.Length(), 7:F2}");
            var offsetSec = (1D/10000000D) * (DateTime.UtcNow.Ticks - kradarLastUpdate);
            debugInt($"nd: {ke.ve.Length() * offsetSec, 7:F2}");
            ke.pos += ke.ve * offsetSec;
            if (ke.ve.Length() > 10) {
                var nowv = ke.pos - testPosLast;
                testPosLast = ke.pos;
                var tdiff = t - testTLast;
                nowv = nowv / tdiff * 60D;
                testTLast = t;
                debugInt($"nv: {nowv.Length(), 7:F2}");
                debugInt($"tdiff: {tdiff} offsetSec: {offsetSec, 7:F2}");
                debugInt($"vd: {(nowv - ke.ve).Length(), 7:F2}"); // 根据位置算的速度 与实际速度的差值
                testQueue.Enqueue(nowv);
                if (testQueue.Count > 10) testQueue.Dequeue();
                var avgv = testQueue.Aggregate((a, b) => a + b)/testQueue.Count;
                var tqvrnc = testQueue.Select((a) => (a - avgv).Length()).Average();
                debugInt($"tqvrnc: {tqvrnc, 7:F2}");
            }
        }

        kradarPosList.Add(ke);
    }


    // get all kradar target
    foreach(var kvp in targetDataDict.Where(x => x.Value is KRadarTargetData && t - ((KRadarTargetData)x.Value).lastFrame > KRadarTargetData.MAX_LIVE_FRAME).ToList()) {
        targetDataDict.Remove(kvp.Key);
    }
    List<KRadarTargetData> kradarTargetList = targetDataDict.Where(x => x.Value is KRadarTargetData).Select(x => (KRadarTargetData)x.Value).ToList();

    //debug("kp count: " + kradarPosList.Count() + " " + kradarTargetList.Count());
    kradarPosList.ForEach(kp => {kp.occupied = false;});
    // predict pos and best match
    foreach (var kt in kradarTargetList)
    {
        var dt = t - kt.lastFrame;
        var kv = kt.Velocity;
        var kvf = ((1D / 60) * kt.Velocity);
        var delta = dt * kvf * 0.5;
        kt.Position = kt.realPos + delta;
        kt.t = t;

        // try match target
        foreach (var kp in kradarPosList)
        {
            kp.error = (kt.Position - kp.pos).Length();
        }

        var matchList = kradarPosList.Where(x => x.occupied == false).OrderBy(x => x.error);
        KRadarElement found = null;
        //debug("mlcount: " + matchList.Count);
        string debugString = "";
        foreach (var kp in matchList)
        {
            debugString += (kp.error + " " + kt.size + " " + kp.size + " ");
            if (kp.error < KRadarTargetData.POS_ABS_ERROR &&
                (kt.size != 0 && (Math.Abs(kt.size - kp.size) / kt.size) < KRadarTargetData.SIZE_RATIO_ERROR))
            {
                found = kp;
                break;
            }
        }
        //debug(debugString);

        if (found != null)
        {
            var lastRealPos = kt.realPos;
            var lastFrame = kt.lastFrame;
            var pe = (found.pos - kt.Position).Length();
            kt.realPos = found.pos;
            kt.size = found.size;
            kt.Position = found.pos;
            kt.lastFrame = t;
            kt.t = t;

            if (found.haveVe)
            {
                kt.Velocity = found.ve;
                var los = MePosition - kt.Position;
                var dir = Vector3D.Normalize(los);
                kt.isHighThreaten = (los.Length() < 10000) && (Vector3D.Dot(kt.Velocity, dir) > 20);
            }

            found.occupied = true;
        }
    }

    // find left kpos, create new kradar target
    int foundCount = 0;
    foreach (var kp in kradarPosList)
    {
        if (kp.occupied) {
            foundCount ++;
            continue;
        }
        var newID = KRadarTargetData.maxId++;
        KRadarTargetData newTarget = new KRadarTargetData(kp.pos, TargetRelation.Enemy, MAX_REBROADCAST_INGEST_COUNT, Vector3D.Zero, MOTHER_CODE, new MatrixD(), 0,t, TargetData.PR_KRADAR);
        newTarget.realPos = kp.pos;
        newTarget.lastFrame = t;
        newTarget.size = kp.size;
        newTarget.id = newID;
        if (kp.haveVe) newTarget.Velocity = kp.ve;

        targetDataDict[newID] = newTarget;
        kradarTargetList.Add(newTarget);
    };
    if (Me is IMyTextSurfaceProvider)
    {
        IMyTextSurface ts = ((IMyTextSurfaceProvider)Me).GetSurface(0);
        StringBuilder sb = new StringBuilder();
        sb.Append(t + "\n");
        foreach (var kt in kradarTargetList)
        {
            if (kt.isDisabled) continue;
            sb.Append(kt.id).Append(":").Append(kt.isSelected ? "Y" : "N").Append(":")
                .Append(kt.isHighThreaten ? "Y" : "N").Append(":")
                .Append(kt.Position.X).Append(":")
                .Append(kt.Position.Y).Append(":")
                .Append(kt.Position.Z).Append(":")
                .Append(kt.Velocity.X).Append(":")
                .Append(kt.Velocity.Y).Append(":")
                .Append(kt.Velocity.Z).Append("\n");
        }
        ts.WriteText(sb.ToString());
    }
    //debug("foundCount: " + foundCount);
}

void debug(string v)
{
    debugInfo += "\n" + v;
}
void debugInt(string v) {
    debugInterval += "\n" + v;
}

static double getLogLen(double len)
{
    // rangeOverride
    var r = (float)((sigmoid(len / 728) - 0.5) * 2); // 728 make 800 = 0.5
    if (r > 1) r = 1;
    if (r < -1) r = -1;
    return r;
}

// (/ 1.0 (+ 1.0 (exp (/ 800 -728.0))))
static double sigmoid(double x)
{
    return 1.0 / (1.0 + Math.Exp(-x));
}


#endregion
}
}