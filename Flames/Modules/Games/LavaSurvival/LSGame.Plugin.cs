﻿/*
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
using System;
using Flames.Events.EconomyEvents;
using Flames.Events.PlayerEvents;
using Flames.Events.LevelEvents;
using Flames.Games;
using Flames.Maths;

namespace Flames.Modules.Games.LS
{
    public partial class LSGame : RoundsGame
    {
        public override void HookEventHandlers()
        {
            OnJoinedLevelEvent.Register(HandleJoinedLevel, Priority.High);
            OnPlayerDyingEvent.Register(HandlePlayerDying, Priority.High);
            OnPlayerDiedEvent.Register(HandlePlayerDied, Priority.High);
            OnBlockHandlersUpdatedEvent.Register(HandleBlockHandlersUpdated, Priority.High);
            OnBlockChangingEvent.Register(HandleBlockChanging, Priority.High);
            OnMoneyChangedEvent.Register(HandleMoneyChanged, Priority.High);

            base.HookEventHandlers();
        }

        public override void UnhookEventHandlers()
        {
            OnJoinedLevelEvent.Unregister(HandleJoinedLevel);
            OnPlayerDyingEvent.Unregister(HandlePlayerDying);
            OnPlayerDiedEvent.Unregister(HandlePlayerDied);
            OnBlockHandlersUpdatedEvent.Unregister(HandleBlockHandlersUpdated);
            OnBlockChangingEvent.Unregister(HandleBlockChanging);
            OnMoneyChangedEvent.Unregister(HandleMoneyChanged);

            base.UnhookEventHandlers();
        }

        public void HandleMoneyChanged(Player p)
        {
            if (p.level != Map) return;
            UpdateStatus1(p);
        }

        public void HandleJoinedLevel(Player p, Level prevLevel, Level level, ref bool announce)
        {
            HandleJoinedCommon(p, prevLevel, level, ref announce);

            if (Map != level) return;
            ResetRoundState(p, Get(p)); // TODO: Check for /reload case?
            OutputMapSummary(p, Map.Config);
            if (RoundInProgress) OutputStatus(p);
        }

        public void HandlePlayerDying(Player p, ushort block, ref bool cancel)
        {
            if (p.level == Map && IsPlayerDead(p)) cancel = true;
        }

        public void HandlePlayerDied(Player p, ushort block, ref TimeSpan cooldown)
        {
            if (p.level != Map || IsPlayerDead(p)) return;

            cooldown = TimeSpan.FromSeconds(30);
            AddLives(p, -1, false);
        }

        public void HandleBlockChanging(Player p, ushort x, ushort y, ushort z, ushort block, bool placing, ref bool cancel)
        {
            if (p.level != Map || !(placing || p.painting)) return;

            if (Config.SpawnProtection && NearLavaSpawn(x, y, z))
            {
                p.Message("You can't place blocks so close to the {0} spawn", FloodBlockName());
                p.RevertBlock(x, y, z);
                cancel = true; return;
            }
        }

        public bool NearLavaSpawn(ushort x, ushort y, ushort z)
        {
            Vec3U16 pos = layerMode ? CurrentLayerPos() : cfg.FloodPos;
            int dist = Config.SpawnProtectionRadius;

            int dx = Math.Abs(x - pos.X);
            int dy = Math.Abs(y - pos.Y);
            int dz = Math.Abs(z - pos.Z);
            return dx <= dist && dy <= dist && dz <= dist;
        }

        public bool TryPlaceBlock(Player p, ref int blocksLeft, string type,
                           ushort block, ushort x, ushort y, ushort z)
        {
            if (!p.Game.Referee && blocksLeft <= 0)
            {
                p.Message("You have no {0} left", type);
                p.RevertBlock(x, y, z);
                return false;
            }

            if (p.ChangeBlock(x, y, z, block) == ChangeResult.Unchanged)
                return false;
            if (p.Game.Referee) return true;

            blocksLeft--;
            if ((blocksLeft % 10) == 0 || blocksLeft <= 10)
            {
                p.Message("{0} left: &4{1}", type, blocksLeft);
            }
            return true;
        }
    }
}