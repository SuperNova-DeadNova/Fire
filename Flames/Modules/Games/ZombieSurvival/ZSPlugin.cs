﻿/*
    Copyright 2015 MCGalaxy
        
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
using Flames.Events.ServerEvents;

namespace Flames.Modules.Games.ZS
{
    public class ZSPlugin : Plugin
    {
        public override string name { get { return "ZS"; } }
        public static Command cmdZS = new CmdZombieSurvival();

        public override void Load(bool startup)
        {
            OnConfigUpdatedEvent.Register(OnConfigUpdated, Priority.Low);
            Command.Register(cmdZS);

            ZSGame.Instance.Config.Path = "properties/games/zombiesurvival.properties";
            OnConfigUpdated();
            ZSGame.Instance.AutoStart();
        }

        public override void Unload(bool shutdown)
        {
            OnConfigUpdatedEvent.Unregister(OnConfigUpdated);
            Command.Unregister(cmdZS);
        }

        public void OnConfigUpdated()
        {
            ZSGame.Instance.Config.Load();
            ZSGame.Instance.infectMessages = ZSConfig.LoadInfectMessages();
        }
    }
}