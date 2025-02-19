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
using Flames.Blocks.Physics;
using Flames.Games;

namespace Flames.Modules.Games.LS
{
    public partial class LSGame : RoundsGame
    {
        public void UpdateBlockHandlers()
        {
            Map.UpdateBlockHandlers(Block.Sponge);
            Map.UpdateBlockHandlers(Block.StillWater);
            Map.UpdateBlockHandlers(Block.Water);
            Map.UpdateBlockHandlers(Block.Deadly_ActiveWater);
            Map.UpdateBlockHandlers(Block.Lava);
            Map.UpdateBlockHandlers(Block.Deadly_ActiveLava);
            Map.UpdateBlockHandlers(Block.Door_Log);
        }

        public void HandleBlockHandlersUpdated(Level lvl, ushort block)
        {
            if (!Running || lvl != Map) return;

            switch (block)
            {
                case Block.Sponge:
                    lvl.PlaceHandlers[block] = PlaceSponge;
                    lvl.PhysicsHandlers[block] = DoSponge; 
                    break;
                case Block.StillWater:
                    lvl.PlaceHandlers[block] = PlaceWater; 
                    break;
                case Block.Water:
                case Block.Deadly_ActiveWater:
                    lvl.PhysicsHandlers[block] = DoWater; 
                    break;
                case Block.Lava:
                case Block.Deadly_ActiveLava:
                    lvl.PhysicsHandlers[block] = DoLava; 
                    break;
                case Block.Door_Log:
                    lvl.PlaceHandlers[block] = PlaceDoor; 
                    break;
            }
        }

        public ChangeResult PlaceSponge(Player p, ushort newBlock, ushort x, ushort y, ushort z)
        {
            LSData data = Get(p);
            bool placed = TryPlaceBlock(p, ref data.SpongesLeft, "Sponges", Block.Sponge, x, y, z);
            if (!placed) return ChangeResult.Unchanged;

            PhysInfo C = default;
            C.X = x; 
            C.Y = y; 
            C.Z = z;
            OtherPhysics.DoSponge(Map, ref C, !waterMode);
            return ChangeResult.Modified;
        }

        public ChangeResult PlaceWater(Player p, ushort newBlock, ushort x, ushort y, ushort z)
        {
            LSData data = Get(p);
            bool placed = TryPlaceBlock(p, ref data.WaterLeft, "Water blocks", Block.StillWater, x, y, z);
            if (!placed) return ChangeResult.Unchanged;

            return ChangeResult.Modified;
        }

        public ChangeResult PlaceDoor(Player p, ushort newBlock, ushort x, ushort y, ushort z)
        {
            LSData data = Get(p);
            bool placed = TryPlaceBlock(p, ref data.DoorsLeft, "Door blocks", Block.Door_Log, x, y, z);
            if (!placed) return ChangeResult.Unchanged;

            return ChangeResult.Modified;
        }


        public void DoSponge(Level lvl, ref PhysInfo C)
        {
            if (C.Data.Value2++ < Config.SpongeLife) return;

            lvl.AddUpdate(C.Index, Block.Air, default(PhysicsArgs));
            OtherPhysics.DoSpongeRemoved(lvl, C.Index, !waterMode);
            C.Data.Data = PhysicsArgs.RemoveFromChecks;
        }

        public void DoWater(Level lvl, ref PhysInfo C)
        {
            ushort x = C.X, y = C.Y, z = C.Z;

            if (!lvl.CheckSpongeWater(x, y, z))
            {
                ushort block = C.Block;

                SpreadWater(lvl, (ushort)(x + 1), y, z, block);
                SpreadWater(lvl, (ushort)(x - 1), y, z, block);
                SpreadWater(lvl, x, y, (ushort)(z + 1), block);
                SpreadWater(lvl, x, y, (ushort)(z - 1), block);
                SpreadWater(lvl, x, (ushort)(y - 1), z, block);

                if (floodUp) SpreadWater(lvl, x, (ushort)(y + 1), z, block);
            }
            else
            { //was placed near sponge
                lvl.AddUpdate(C.Index, Block.Air, default(PhysicsArgs));
            }
            C.Data.Data = PhysicsArgs.RemoveFromChecks;
        }

        public void DoLava(Level lvl, ref PhysInfo C)
        {
            ushort x = C.X, y = C.Y, z = C.Z;

            if (C.Data.Data < spreadDelay)
            {
                C.Data.Data++; 
                return;
            }

            if (!lvl.CheckSpongeWater(x, y, z))
            {
                ushort block = C.Block;
                SpreadLava(lvl, (ushort)(x + 1), y, z, block);
                SpreadLava(lvl, (ushort)(x - 1), y, z, block);
                SpreadLava(lvl, x, y, (ushort)(z + 1), block);
                SpreadLava(lvl, x, y, (ushort)(z - 1), block);
                SpreadLava(lvl, x, (ushort)(y - 1), z, block);

                if (floodUp) SpreadLava(lvl, x, (ushort)(y + 1), z, block);
            }
            else
            { //was placed near sponge
                lvl.AddUpdate(C.Index, Block.Air, default(PhysicsArgs));
            }
            C.Data.Data = PhysicsArgs.RemoveFromChecks;
        }


        public void SpreadWater(Level lvl, ushort x, ushort y, ushort z, ushort type)
        {
            int index;
            ushort block = lvl.GetBlock(x, y, z, out index);
            if (InSafeZone(x, y, z)) return;

            switch (block)
            {
                case Block.Air:
                    if (!lvl.CheckSpongeWater(x, y, z))
                    {
                        lvl.AddUpdate(index, type);
                    }
                    break;

                case Block.Lava:
                case Block.FastLava:
                case Block.Deadly_ActiveLava:
                    if (!lvl.CheckSpongeWater(x, y, z))
                    {
                        lvl.AddUpdate(index, Block.Stone, default(PhysicsArgs));
                    }
                    break;

                case Block.Sand:
                case Block.Gravel:
                case Block.FloatWood:
                    lvl.AddCheck(index); 
                    break;

                case Block.CoalOre: // TODO 
                case Block.Water:
                case Block.Deadly_ActiveWater:
                    break;

                default:
                    SpreadLiquid(lvl, x, y, z, index, block, true);
                    break;
            }
        }

        public void SpreadLava(Level lvl, ushort x, ushort y, ushort z, ushort type)
        {
            int index;
            ushort block = lvl.GetBlock(x, y, z, out index);
            if (InSafeZone(x, y, z)) return;

            // in LS, sponge should stop lava too
            switch (block)
            {
                case Block.Air:
                    if (!lvl.CheckSpongeWater(x, y, z))
                    {
                        lvl.AddUpdate(index, type);
                    }
                    break;

                case Block.Water:
                case Block.Deadly_ActiveWater:
                    if (!lvl.CheckSpongeWater(x, y, z))
                    {
                        lvl.AddUpdate(index, Block.Stone, default(PhysicsArgs));
                    }
                    break;

                case Block.Sand:
                    if (lvl.physics > 1)
                    { //Adv physics changes sand to glass next to lava
                        lvl.AddUpdate(index, Block.Glass, default(PhysicsArgs));
                    }
                    else
                    {
                        lvl.AddCheck(index);
                    }
                    break;

                case Block.Gravel:
                    lvl.AddCheck(index); 
                    break;

                case Block.CoalOre: // TODO 
                case Block.Lava:
                case Block.FastLava:
                case Block.Deadly_ActiveLava:
                    break;

                default:
                    SpreadLiquid(lvl, x, y, z, index, block, false);
                    break;
            }
        }

        public void SpreadLiquid(Level lvl, ushort x, ushort y, ushort z, int index,
                          ushort block, bool isWater)
        {
            if (floodMode == LSFloodMode.Calm) return;
            Random rand = lvl.physRandom;

            bool instaKills = isWater ?
                lvl.Props[block].WaterKills : lvl.Props[block].LavaKills;

            // TODO need to kill less often
            if (instaKills && floodMode > LSFloodMode.Disturbed)
            {
                if (!lvl.CheckSpongeWater(x, y, z))
                {
                    lvl.AddUpdate(index, Block.Air, default(PhysicsArgs));
                }
            }
            else if (!lvl.Props[block].OPBlock && rand.Next(1, 101) <= burnChance)
            {
                PhysicsArgs C = default;
                C.Type1 = PhysicsArgs.Wait; 
                C.Value1 = destroyDelay;
                C.Type2 = PhysicsArgs.Dissipate; 
                C.Value2 = dissipateChance;
                lvl.AddUpdate(index, Block.CoalOre, C);
            }
        }


        public byte GetDestroyDelay()
        {
            LSFloodMode mode = floodMode;

            if (mode == LSFloodMode.Disturbed) return 200;
            if (mode == LSFloodMode.Furious) return 100;
            if (mode == LSFloodMode.Wild) return 50;
            return 10;
        }

        public byte GetDissipateChance()
        {
            LSFloodMode mode = floodMode;

            if (mode == LSFloodMode.Disturbed) return 50;
            if (mode == LSFloodMode.Furious) return 65;
            if (mode == LSFloodMode.Wild) return 80;
            return 100;
        }

        public byte GetBurnChance()
        {
            LSFloodMode mode = floodMode;

            if (mode == LSFloodMode.Disturbed) return 50;
            if (mode == LSFloodMode.Furious) return 70;
            if (mode == LSFloodMode.Wild) return 85;
            return 100;
        }
    }
}