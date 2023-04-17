using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Sandbox.ModAPI;
using VRage.ModAPI;
using Sandbox.ModAPI.Interfaces;
using VRage.Game.ModAPI.Interfaces;
using VRage.Game.ModAPI;
using SpaceEngineers.Game.ModAPI;

namespace RadarBlock
{
	public class SoundBlockManager
	{
        public static void PlaySound(IMyEntity parent, string soundBlockName)
        {
            IMySoundBlock sound = FindSoundBlock(parent, soundBlockName);

            if(sound != null)
            {
                List<ITerminalAction> actions = new List<ITerminalAction>();
                sound.GetActions(actions);
                ITerminalAction first = actions.FirstOrDefault(x => x.Name.ToString() == "Play");
                if(first != null)
                {
                    first.Apply(sound);
                }
            }
        }

		public static IMySoundBlock FindSoundBlock(IMyEntity parent, string soundBlockName)
		{
			IMySoundBlock result = null;

			if (!(parent is IMyCubeGrid))
				return result;

			IMyCubeGrid grid = (IMyCubeGrid)parent;
			List<IMySlimBlock> blocks = new List<IMySlimBlock>();
			grid.GetBlocks(blocks);
			foreach (IMySlimBlock block in blocks)
			{
				if (block.FatBlock == null)
					continue;

				if (!(block.FatBlock is IMySoundBlock))
					continue;

				if (((IMySoundBlock)block.FatBlock).CustomName.ToLower() != soundBlockName.ToLower())
					continue;

				result = (IMySoundBlock)block.FatBlock;
				break;
			}

			return result;
		}
	}
}
