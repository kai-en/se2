using Sandbox.Game.Gui;
using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
public class TestBlock:MyGridProgram { 
#region In-game Script 

List<IMyTerminalBlock> targetList;
bool inited = false;
string debugString = "";

void debug(string s) {
  debugString += s + "\n";
}

void init() {
targetList =  new List<IMyTerminalBlock>();
GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock> (targetList, t => t.CustomName.Contains("Warhead"));
debug("whc: " + targetList.Count);
IMyTerminalBlock target;
if (targetList.Count > 0) {
  target = targetList[0];
} else {
  return;
}
debug("tc: " + target.GetType());
List<ITerminalAction> actions = new List<ITerminalAction>();
target.GetActions(actions);
foreach (var a in actions) {
  debug(a.Id);
}

inited = true;
}


Program()
{
Runtime.UpdateFrequency = UpdateFrequency.Update1;
}

void Main(string arguments, UpdateType updateType){

if (!inited) {
init();
return;
}

Echo(debugString);

}


#endregion
}
}