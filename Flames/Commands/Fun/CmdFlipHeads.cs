/*
    Copyright 2010 MCSharp team (Modified for use with MCZall/MCLawl/MCForge)
    
    Dual-licensed under the Educational Community License, Version 2.0 and
    the GNU General Public License, Version 3 (the "Licenses"); you may
    not use this file except in compliance with the Licenses. You may
    obtain a copy of the Licenses at
    
    https://opensource.org/license/ecl-2-0/
    https://www.gnu.org/licenses/gpl-3.0.html
    
    Unless required by applicable law or agreed to in writing,
    software distributed under the Licenses are distributed on an "AS IS"
    BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express
    or implied. See the Licenses for the specific language governing
    permissions and limitations under the Licenses.
*/
namespace Flames.Commands.Fun
{
    public class CmdFlipHeads : Command
    {
        public override string name { get { return "FlipHeads"; } }
        public override string type { get { return CommandTypes.Other; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Operator; } }

        public override void Use(Player p, string message)
        {
            Server.flipHead = !Server.flipHead;
            if (Server.flipHead) Chat.MessageGlobal("All necks were broken");
            else Chat.MessageGlobal("All necks were mended");
        }

        public override void Help(Player p)
        {
            p.Message("&T/FlipHeads");
            p.Message("&HMakes all players have upside down heads");
        }
    }
}