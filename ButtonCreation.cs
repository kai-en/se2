using System.Collections.Generic;
using System.Text;
using Sandbox.ModAPI;
using Sandbox.Game;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Utils;

namespace RadarBlock{	
	
	public static class ButtonCreations{
		
		public static bool controlsCreated = false;
		public static bool actionCreated = false;
		
		public static void CreateControls(IMyTerminalBlock block, List<IMyTerminalControl> controls){
			
			if(block as IMyBeacon == null || controlsCreated == true)
            {
				return;
			}

            controlsCreated = true;

            // Hide Radar Hud Text Box
            var controlList = new List<IMyTerminalControl>();
            MyAPIGateway.TerminalControls.GetControls<IMyBeacon>(out controlList);
            controlList[9].Visible = Block => RadarButtons.HideControls(Block);
            //controlList[8].Visible = Block => RadarButtons.HideControls(Block);

            //Separator
            var sepA = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, IMyBeacon>("SeparatorA");
			sepA.Enabled = Block => true;
			sepA.SupportsMultipleBlocks = false;
			sepA.Visible = Block => RadarButtons.ControlVisibility(Block);
			MyAPIGateway.TerminalControls.AddControl<IMyBeacon>(sepA);
			controls.Add(sepA);
			
			// Asteroid Filter
			var roidFilter = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, IMyBeacon>("RoidFilter");
			roidFilter.Enabled = Block => true;
			roidFilter.SupportsMultipleBlocks = false;
			roidFilter.Visible = Block => RadarButtons.ControlVisibility(Block);
			roidFilter.Title = MyStringId.GetOrCompute("Omit Roids");
			roidFilter.Getter = RadarCore.GetRoidFilter;
			roidFilter.Setter = RadarCore.SetRoidFilter;
			MyAPIGateway.TerminalControls.AddControl<IMyBeacon>(roidFilter);
			controls.Add(roidFilter);
			
			// Friendly Filter
			var friendlyFilter = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, IMyBeacon>("FriendlyFilter");
			friendlyFilter.Enabled = Block => true;
			friendlyFilter.SupportsMultipleBlocks = false;
			friendlyFilter.Visible = Block => RadarButtons.ControlVisibility(Block);
			friendlyFilter.Title = MyStringId.GetOrCompute("Omit Friendly");
			friendlyFilter.Getter = RadarCore.GetFriendlyFilter;
			friendlyFilter.Setter = RadarCore.SetFriendlyFilter;
			MyAPIGateway.TerminalControls.AddControl<IMyBeacon>(friendlyFilter);
			controls.Add(friendlyFilter);
			
			// NPC Filter
			var npcFilter = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, IMyBeacon>("NPCFilter");
			npcFilter.Enabled = Block => true;
			npcFilter.SupportsMultipleBlocks = false;
			npcFilter.Visible = Block => RadarButtons.ControlVisibility(Block);
			npcFilter.Title = MyStringId.GetOrCompute("Omit NPCs");
			npcFilter.Getter = RadarCore.GetNPCFilter;
			npcFilter.Setter = RadarCore.SetNPCFilter;
			MyAPIGateway.TerminalControls.AddControl<IMyBeacon>(npcFilter);
			controls.Add(npcFilter);
			
			//Separator
			var sepB = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, IMyBeacon>("SeparatorB");
			sepB.Enabled = Block => true;
			sepB.SupportsMultipleBlocks = false;
			sepB.Visible = Block => RadarButtons.ControlVisibility(Block);
			MyAPIGateway.TerminalControls.AddControl<IMyBeacon>(sepB);
			controls.Add(sepB);
			
			//Scan Button
			var scanButton = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyBeacon>("Scan");
			scanButton.Enabled = Block => RadarButtons.CheckEnabled(Block);
			scanButton.SupportsMultipleBlocks = false;
			scanButton.Visible = Block => RadarButtons.ControlVisibility(Block);
			scanButton.Title = MyStringId.GetOrCompute("Scan");
			scanButton.Action = Block => RadarCore.ActiveScan(Block);
			MyAPIGateway.TerminalControls.AddControl<IMyBeacon>(scanButton);
			controls.Add(scanButton);
			
			// Clear GPS Button
			var clearButton = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyBeacon>("ClearGPS");
			clearButton.Enabled = Block => true;
			clearButton.SupportsMultipleBlocks = false;
			clearButton.Visible = Block => RadarButtons.ControlVisibility(Block);
			clearButton.Title = MyStringId.GetOrCompute("Clear GPS");
			clearButton.Action = Block => RadarCore.ClearGPS(Block);
			MyAPIGateway.TerminalControls.AddControl<IMyBeacon>(clearButton);
			controls.Add(clearButton);

            //Separator
            var sepC = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, IMyBeacon>("SeparatorC");
            sepC.Enabled = Block => true;
            sepC.SupportsMultipleBlocks = false;
            sepC.Visible = Block => RadarButtons.ControlVisibility(Block);
            MyAPIGateway.TerminalControls.AddControl<IMyBeacon>(sepC);
            controls.Add(sepC);

            //Label
            var labelA = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlLabel, IMyBeacon>("LabelA");
            labelA.Enabled = Block => true;
            labelA.SupportsMultipleBlocks = false;
            labelA.Visible = Block => RadarButtons.ControlVisibility(Block);
            labelA.Label = MyStringId.GetOrCompute("Select LCD To Display Radar Info");
            MyAPIGateway.TerminalControls.AddControl<IMyBeacon>(labelA);
            controls.Add(labelA);

            // LCD List
            var lcdList = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlListbox, IMyBeacon>("LCDList");
            lcdList.Enabled = Block => true;
            lcdList.SupportsMultipleBlocks = false;
            lcdList.Visible = Block => RadarButtons.ControlVisibility(Block);
            lcdList.ListContent = RadarCore.GetLCDs;
            lcdList.VisibleRowsCount = 6;
            lcdList.Multiselect = false;
            lcdList.ItemSelected = RadarCore.SetSelectedLCD;
            MyAPIGateway.TerminalControls.AddControl<IMyBeacon>(lcdList);
            controls.Add(lcdList);
        }
		
		public static void CreateActions(IMyTerminalBlock block, List<IMyTerminalAction> controls){

			if(block as IMyBeacon == null || actionCreated == true){
				
				return;
				
			}
			
			actionCreated = true;
			
			//Action - Scan
			var scanAction = MyAPIGateway.TerminalControls.CreateAction<IMyBeacon>("ScanAction");
			scanAction.Enabled = Block => RadarButtons.ControlVisibility(Block);
            scanAction.ValidForGroups = false;
            scanAction.Action = Block => RadarCore.ActiveScan(Block);
            scanAction.Name = new StringBuilder("Scan");
			MyAPIGateway.TerminalControls.AddAction<IMyBeacon>(scanAction);
			controls.Add(scanAction);
			
		}
	}
}