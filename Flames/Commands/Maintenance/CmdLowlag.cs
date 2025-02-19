/*
    Copyright 2011 MCForge
        
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
namespace Flames.Commands.Maintenance
{
    public class CmdLowlag : Command
    {
        public override string name { get { return "LowLag"; } }
        public override string type { get { return CommandTypes.Moderation; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Operator; } }
        public string IntervalMessage(int interval)
        {
            if (Server.Config.PositionUpdateInterval <= 0)
            {
                return "  Player positions are updated &aconstantly&S.";
            }
            else
            {
                return "Positions now update every &b" + interval + " &Smilliseconds.";
            }
        }
        public override void Use(Player p, string message)
        {
            if (message.Length == 0 && Server.Config.PositionUpdateInterval > 1000)
            {
                Server.Config.PositionUpdateInterval = 100;
                Chat.MessageAll("&dLow lag &Sturned &cOFF &S- positions update every &b100 &Sms.");
            }
            else if (message.Length == 0)
            {
                Server.Config.PositionUpdateInterval = 2000;
                Chat.MessageAll("&dLow lag &Sturned &aON &S- positions update every &b2000 &Sms.");
            }
            else
            {
                int interval = 0;
                if (!CommandParser.GetInt(p, message, "Interval", ref interval, 0, 2000)) return;
                Server.Config.PositionUpdateInterval = interval;
                Chat.MessageAll(IntervalMessage(interval));
            }
            SrvProperties.Save();
        }

        public override void Help(Player p)
        {
            p.Message("&T/LowLag [interval in milliseconds]");
            p.Message("&HSets the interval between sending of position packets.");
            p.Message("&HIf no interval is given, then 2000 ms is used if the current interval" +
                               " is less than 1000 ms, otherwise 200 ms is used for the interval.");
        }
    }
}