#define DEBUG_WIREFRAME
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using th3dungeon.Data;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;

namespace th3dungeon
{
    public class Th3Dungeon : ModSystem
    {
        private ICoreServerAPI _api;

        private IWorldGenBlockAccessor _chunkGenBlockAccessor;

        private LCGRandom _chunkRand;

        private int _chunkSize;

        private DungeonsConfig _dungeonsConfig;

        private int _chunkRange = 5;

#if DEBUG_WIREFRAME
        private DrawWireframeCube _drawWireframeCube;

        private ClientMain _game;

        private IServerNetworkChannel _serverNetworkChannel;

        private List<Cuboidi> _generatedRoomsC;

        private IClientNetworkChannel _clientNetworkChannel;

        private readonly Vec4f _debugColor = new Vec4f(1f, 1f, 0f, 1f);

        private bool _debugDungeonEnabled;
#endif
        private RockStrataConfig RockStrata { get; set; }

        public override double ExecuteOrder()
        {
            return 0.5;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            _api = api;

            _chunkSize = api.WorldManager.ChunkSize;

            _api.Event.GetWorldgenBlockAccessor(OnWorldGenBlockAccessor);
            _api.Event.InitWorldGenerator(InitWorldGen, "standard");

            _api.Event.ChunkColumnGeneration(GenChunkColumn, EnumWorldGenPass.TerrainFeatures, "standard");
            
            _api.RegisterCommand("mapth3dungeons", "adds a waypoint for every th3dungeon within the specified chunk radius", "[radius in chunks]", (player, groupId, args) =>
            {
                var distance = (int)args.PopInt(10);
                var chunkRand = new LCGRandom(api.World.Seed);
                var worldMapManager = _api.ModLoader.GetModSystem<WorldMapManager>();
                if (worldMapManager.MapLayers.FirstOrDefault(l => l is WaypointMapLayer) is
                    WaypointMapLayer waypointMapLayer)
                {
                    var chunkX = player.Entity.Pos.AsBlockPos.X / 32;
                    var chunkZ = player.Entity.Pos.AsBlockPos.Z / 32;
                    var addedWaypoints = 0;

                    for (var dx = -distance; dx <= distance; dx++)
                    {
                        for (var dz = -distance; dz <= distance; dz++)
                        {
                            chunkRand.InitPositionSeed(chunkX + dx, chunkZ + dz);
                            var unused = chunkRand.NextFloat();
                            var spawnChance = chunkRand.NextFloat();
                            if (spawnChance <= _dungeonsConfig.Chance)
                            {
                                var x = 32 * (chunkX + dx);
                                var z = 32 * (chunkZ + dz);
                                var wp = new Waypoint
                                {
                                    Color = -23296, // Orange
                                    OwningPlayerUid = player.PlayerUID,
                                    Position = new Vec3d(x, 100, z),
                                    Title = "Th3Dungeon",
                                    Icon = "ruins",
                                    Pinned = false,
                                };
                                waypointMapLayer.Waypoints.Add(wp);
                                addedWaypoints++;
                            }
                        }
                    }
                    player.SendMessage(GlobalConstants.GeneralChatGroup, $"Added {addedWaypoints} waypoints", EnumChatType.CommandSuccess);
                }
            }, Privilege.root);

#if DEBUG_WIREFRAME
            _serverNetworkChannel = api.Network.RegisterChannel("th3dungeon-debug");
            _serverNetworkChannel.RegisterMessageType(typeof(List<Cuboidi>));
            _api.Event.PlayerNowPlaying += OnPlayerNowPlaying;
#endif
        }

#if DEBUG_WIREFRAME
        private void OnPlayerNowPlaying(IServerPlayer byPlayer)
        {
            if (_dungeonsConfig != null && _dungeonsConfig.Debug > 1 &&  _generatedRoomsC != null)
            {
                _serverNetworkChannel.SendPacket(_generatedRoomsC, byPlayer);
            }
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            _game = (ClientMain)api.World;
            _drawWireframeCube = new DrawWireframeCube(_game, -1);
            var dummyRenderer = new DummyRenderer
            {
                action = OnRender,
                RenderOrder = 0.5
            };

            api.RegisterCommand("debugdungeon", string.Empty, string.Empty, (groupId, args) =>
            {
                _debugDungeonEnabled = !_debugDungeonEnabled;
                if (_debugDungeonEnabled)
                {
                    api.Event.RegisterRenderer(dummyRenderer, EnumRenderStage.Opaque, "dungeon-render");
                }
                else
                {
                    api.Event.UnregisterRenderer(dummyRenderer, EnumRenderStage.Opaque);
                }
            });

            _clientNetworkChannel = api.Network.RegisterChannel("th3dungeon-debug");
            _clientNetworkChannel.RegisterMessageType(typeof(List<Cuboidi>));
            _clientNetworkChannel.SetMessageHandler<List<Cuboidi>>(OnGeneratedRoomsReceiving);
        }

        private void OnGeneratedRoomsReceiving(List<Cuboidi> rooms)
        {
            _generatedRoomsC = rooms;
        }

        private void OnRender(float deltaTime)
        {
            if (!_debugDungeonEnabled || _generatedRoomsC == null) return;
            
            foreach (var room in _generatedRoomsC)
            {
                var halfSizeX = room.SizeX / 2f;
                var halfSizeY = room.SizeY / 2f;
                var halfSizeZ = room.SizeZ / 2f;
                _drawWireframeCube.Render(_game, room.X1 + halfSizeX, room.Y1 + halfSizeY, room.Z1 + halfSizeZ, halfSizeX, halfSizeY, halfSizeZ, 4f, _debugColor);
            }
        }
#endif
        private void OnWorldGenBlockAccessor(IChunkProviderThread chunkProvider)
        {
            _chunkGenBlockAccessor = chunkProvider.GetBlockAccessor(true);
        }

        private void InitWorldGen()
        {
            _chunkRand = new LCGRandom(_api.WorldManager.Seed);
            DungeonsConfig modConfig = null;
            try
            {
                modConfig = _api.LoadModConfig<DungeonsConfig>("th3dungeonconfig.json");
            }
            catch (Exception e)
            {
                Mod.Logger.Fatal($"failed loading ModConfig/th3dungeonconfig.json {e.Message}");
            }
            
            RockStrata = _api.Assets.Get<RockStrataConfig>(new AssetLocation("game:worldgen/rockstrata.json"));

            if (modConfig?.Dungeons != null && modConfig.Dungeons.Count > 0)
            {
                _dungeonsConfig = modConfig;
            }
            else
            {
                _dungeonsConfig = new DungeonsConfig
                {
                    Dungeons = new List<DungeonConfig>()
                };
                
                if (modConfig != null)
                {
                    _dungeonsConfig.ChunkRange = modConfig.ChunkRange != 0 ? modConfig.ChunkRange : 6;
                    _dungeonsConfig.Chance = modConfig.Chance != 0 ? modConfig.Chance : 0.0008f;
                    _dungeonsConfig.Debug =  modConfig.Debug !=0 ? modConfig.Debug : 0;
                }
                var dungeonsConfigs = _api.Assets.GetMany<DungeonsConfig>(Mod.Logger,"worldgen/th3dungeon/th3dungeonconfig.json");
                
                // merge all configs
                var dungeonCount = dungeonsConfigs.Count;
                if (modConfig != null && modConfig.ExcludeTh3Dungeons && dungeonsConfigs.Any(config => config.Key.Domain.Equals(Mod.Info.ModID)))
                {
                    dungeonCount--;
                }
                foreach (var dungeonConfig in dungeonsConfigs)
                {
                    if (modConfig != null && modConfig.ExcludeTh3Dungeons && dungeonConfig.Key.Domain.Equals(Mod.Info.ModID))
                    {
                        continue;
                    }
                    if (dungeonConfig.Value.ChunkRange > _dungeonsConfig.ChunkRange)
                    {
                        _dungeonsConfig.ChunkRange = dungeonConfig.Value.ChunkRange;
                    }

                    float sumModDungeon = 0;
                    dungeonConfig.Value.Dungeons.ForEach(dungeon => sumModDungeon += dungeon.Chance);
                    if (Math.Abs(sumModDungeon - 1f) > 0.001)
                    {
                        Mod.Logger.Fatal($"DungeonsConfig {dungeonConfig.Key.Domain}:{dungeonConfig.Key.Path} does not add up to 1. [{sumModDungeon}]");
                    }
                    foreach (var dungeon in dungeonConfig.Value.Dungeons)
                    {
                        dungeon.Chance /= dungeonCount;
                        _dungeonsConfig.Dungeons.Add(dungeon);
                    }
                }
            }
            
            if (_dungeonsConfig == null)
            {
                Mod.Logger.Fatal($"DungeonsConfigs not found check your ModConfig folder and create a th3dungeonconfig.json");
                return;
            }
            float sum = 0;
            _dungeonsConfig.Dungeons.ForEach(dungeon => sum += dungeon.Chance);
            
            if (Math.Abs(sum - 1f) > 0.001)
            {
                Mod.Logger.Fatal($"DungeonsConfigs do not add up to 1. [{sum}]");
            }
            _chunkRange = _dungeonsConfig.ChunkRange;

            _dungeonsConfig.Dungeons.ForEach(dungeon =>
            {
                dungeon.Rooms = new Dictionary<string, List<DungeonRoom>>();
                sum = 0;
                dungeon.Categories.ForEach(cat => sum += cat.Chance);
                if (Math.Abs(sum - 1f) > 0.001)
                {
                    Mod.Logger.Fatal($"DungeonConfig {dungeon.BasePath} categories do not add up to 1. [{sum}]");
                }
                var startRoomsPath = new AssetLocation(dungeon.StartRoomPath);
                var startRooms = _api.Assets.GetMany<BlockSchematic>(_api.Logger, startRoomsPath.Path, startRoomsPath.Domain);
                dungeon.StartRooms = new List<DungeonRoom>();
                foreach (var asset in startRooms)
                {
                    dungeon.StartRooms.Add(new DungeonRoom(_api, asset.Value, _chunkGenBlockAccessor, asset.Key.Path));
                }

                if (dungeon.GenerateEntrance)
                {
                    var startRoomsTopPath = new AssetLocation(dungeon.StartRoomTopPath);
                    var startRoomsTop = _api.Assets.GetMany<BlockSchematic>(_api.Logger, startRoomsTopPath.Path, startRoomsTopPath.Domain);
                    dungeon.StartRoomsTop = new List<DungeonRoom>();
                    foreach (var asset in startRoomsTop)
                    {
                        dungeon.StartRoomsTop.Add(new DungeonRoom(_api, asset.Value, _chunkGenBlockAccessor, asset.Key.Path));
                    }

                    var stairs = _api.Assets.Get<BlockSchematic>(new AssetLocation(dungeon.StairsPath));
                    dungeon.Stairs = new DungeonRoom(_api, stairs, _chunkGenBlockAccessor, dungeon.StairsPath);
                }

                var endRoomsPath = new AssetLocation(dungeon.EndRoomPath);
                var endRooms = _api.Assets.GetMany<BlockSchematic>(_api.Logger, endRoomsPath.Path, endRoomsPath.Domain);
                dungeon.EndRooms = new List<DungeonRoom>();
                foreach (var asset in endRooms)
                {
                    dungeon.EndRooms.Add(new DungeonRoom(_api, asset.Value, _chunkGenBlockAccessor, asset.Key.Path));
                }

                var basePath = new AssetLocation(dungeon.BasePath);
                foreach (var cat in dungeon.Categories)
                {
                    var assets = _api.Assets.GetMany<BlockSchematic>(_api.Logger, basePath.Path + cat.Name + "/", basePath.Domain);
                    var catRooms = assets.Select(asset => new DungeonRoom(_api, asset.Value, _chunkGenBlockAccessor, asset.Key.Path)).ToList();
                    dungeon.Rooms.Add(cat.Name, catRooms);
                }

                if (dungeon.ReplaceWithRockType == null) return;
                
                dungeon.ResolvedReplaceWithRockType = new Dictionary<int, Dictionary<int, int>>();

                foreach (var val in dungeon.ReplaceWithRockType)
                {
                    var blockIdByRockId = new Dictionary<int, int>();
                    foreach (var strat in RockStrata.Variants)
                    {
                        var rockBlock = _api.World.GetBlock(strat.BlockCode);
                        var resolvedLoc = val.Value.Clone();
                        resolvedLoc.Path = resolvedLoc.Path.Replace("{rock}", rockBlock.LastCodePart());

                        var resolvedBlock = _api.World.GetBlock(resolvedLoc);
                        if (resolvedBlock == null) continue;
                        blockIdByRockId[rockBlock.Id] = resolvedBlock.Id;

                        var quartzBlock = _api.World.GetBlock(new AssetLocation("ore-quartz-" + rockBlock.LastCodePart()));
                        if (quartzBlock != null)
                        {
                            blockIdByRockId[quartzBlock.Id] = resolvedBlock.Id;
                        }
                    }

                    var sourceBlocks = _api.World.SearchBlocks(val.Key);
                    foreach (var sourceBlock in sourceBlocks)
                    {
                        dungeon.ResolvedReplaceWithRockType[sourceBlock.Id] = blockIdByRockId;
                    }
                }
            });
            Mod.Logger.Event($"{_dungeonsConfig.Dungeons.Count} Dungeons Loaded");
            // RuntimeEnv.DebugOutOfRangeBlockAccess = true; 1985799215
        }

        

        private void GenChunkColumn(IServerChunk[] chunks, int chunkX, int chunkZ, ITreeAttribute chunkGenParams = null)
        {
            var data = new DungeonData(chunkX, chunkZ, chunks);

            for (var dx = -_chunkRange; dx <= _chunkRange; dx++)
            {
                for (var dz = -_chunkRange; dz <= _chunkRange; dz++)
                {
                    _chunkRand.InitPositionSeed(chunkX + dx, chunkZ + dz);
                    GenDungeonCheck(data, dx, dz);
                }
            }
        }
        private void GenDungeonCheck(DungeonData data, int dx, int dz)
        {
            data.DungeonConfig = ChooseDungeon();
            if (_dungeonsConfig.Debug == 3)
            {
                if (data.ChunkX + dx == 16000 && data.ChunkZ + dz == 16000)
                {
                    GenDungeon(data, dx, dz);
                }
            }
            else if (_chunkRand.NextFloat() <= _dungeonsConfig.Chance)
            {
                GenDungeon(data, dx, dz);
            }
        }

        private void GenDungeon(DungeonData data, int dx, int dz)
        {
            var chunkXd = data.ChunkX + dx;
            var chunkZd = data.ChunkZ + dz;
            // error with top rooms so we place in center
            // var x = chunkXd * _chunkSize + _chunkRand.NextInt(_chunkSize);
            // var z = chunkZd * _chunkSize + _chunkRand.NextInt(_chunkSize);
            var x = chunkXd * _chunkSize + 15;
            var z = chunkZd * _chunkSize + 15;

            //choose initial room
            data.NextSpawn.Room = GetRandomRoom(data.DungeonConfig.StartRooms);

            // choose initial rotation
            var startRoomRotation = _chunkRand.NextInt(4);
            // int startRoomRotation = 0;
            data.Schematic = data.NextSpawn.Room.Rotations[startRoomRotation];

            // take sea level since that should be consistent (surface will change depending if blocks are added on top while generating)
            data.NextSpawn.Position = new BlockPos(x, _api.World.SeaLevel + data.DungeonConfig.SealevelOffset, z);

            // add start room to overlap check
            if (!data.Initialized)
            {
                var collisionPos = data.NextSpawn.Position.Copy();
                collisionPos.X -= data.Schematic.SizeX / 2;
                collisionPos.Z -= data.Schematic.SizeZ / 2;
                var area = new Cuboidi(collisionPos, collisionPos.AddCopy(data.Schematic.SizeX, data.Schematic.SizeY, data.Schematic.SizeZ));
                data.GeneratedRooms.Add(area);
            }

            //adjust start pos after initial room cuboid is added
            data.Schematic.AdjustStartPos(data.NextSpawn.Position, EnumOrigin.BottomCenter);

            //spawn initial room
            Place(data);

            if (data.DungeonConfig.GenerateEntrance)
            {
                GenEntrance(data, x, z, data.Schematic.SizeY, startRoomRotation);
            }

            for (var i = 0; i < data.DungeonConfig.RoomsToGenerate; i++)
            {
                if (data.DoorPos.Count <= 0) continue;
                
                //chose next room to spawn
                data.NextSpawn.Room = ChooseRoom(data);

                //choose next door pos to gen next room
                var ni = _chunkRand.NextInt(data.DoorPos.Count);
                var current = data.DoorPos.ElementAt(ni);

                // get spawn pos offset from next room and previous room and previous facing
                if (!GetNext(data, current)) continue;
                
                Place(data);
            }

            if (_dungeonsConfig.Debug > 0 && dx == 0 && dz == 0)
            {
                Mod.Logger.VerboseDebug($"GeneratedRooms: {data.GeneratedRooms.Count}");
                Mod.Logger.VerboseDebug($"DoorPos.Count: {data.DoorPos.Count}");
                Mod.Logger.VerboseDebug($"/tp ={x} 120 ={z}");
            }

            GenRoomEnds(data, dx == 0 && dz == 0);

#if DEBUG_WIREFRAME
            if (_dungeonsConfig.Debug > 1 && dx == 0 && dz == 0)
            {
                if (_generatedRoomsC == null)
                {
                    _generatedRoomsC = new List<Cuboidi>();
                }
                _generatedRoomsC.AddRange(data.GeneratedRooms);
                _serverNetworkChannel.BroadcastPacket(_generatedRoomsC);
            }
#endif
            if (!data.Initialized)
            {
                data.Initialized = true;
            }

            if (data.Reinforcements == null) return;
            {
                for (var i = 0; i < data.Chunks.Length; i++)
                {
                    if (data.Reinforcements[i] != null)
                        data.Chunks[i].SetModdata("reinforcements", data.Reinforcements[i]);
                }
            }
        }

        private DungeonRoom GetRandomRoom(List<DungeonRoom> rooms)
        {
            var roomIndex = _chunkRand.NextInt(rooms.Count);
            return rooms[roomIndex];
        }

        private DungeonRoom ChooseRoom(DungeonData data, bool log = false)
        {
            var chance = _chunkRand.NextFloat();
            float chanceStart = 0;

            foreach (var cat in data.DungeonConfig.Categories)
            {
                if (chance >= chanceStart && chance < chanceStart + cat.Chance)
                {
                    var roomIndex = _chunkRand.NextInt(data.DungeonConfig.Rooms[cat.Name].Count);
                    if (log)
                    {
                        Mod.Logger.VerboseDebug($"ChooseRoom: {cat.Name} {chance * 100}%");
                    }
                    return data.DungeonConfig.Rooms[cat.Name].ElementAt(roomIndex);
                }
                chanceStart += cat.Chance;
            }
            Mod.Logger.Error($"Picking default room StartRoom: {chance}");
            return data.DungeonConfig.StartRooms.FirstOrDefault();
        }

        private DungeonConfig ChooseDungeon()
        {
            var chance = _chunkRand.NextFloat();
            float chanceStart = 0;

            foreach (var dungeon in _dungeonsConfig.Dungeons)
            {
                if (chance >= chanceStart && chance < chanceStart + dungeon.Chance)
                {
                    return dungeon;
                }
                chanceStart += dungeon.Chance;
            }
            return _dungeonsConfig.Dungeons.FirstOrDefault();
        }

        private bool GetNext(DungeonData data, DoorPos current, bool randStart = true, bool removeDoorPos = true)
        {
            //add the door offset to the position => will result in new block position where the next door needs to be
            data.NextSpawn.OrigPosition = current.Position.AddCopy(current.Facing);

            // search for next facing
            // iterate over the 4 possible rotations of the room
            // make the rotations random and not in order
            var start = randStart ? _chunkRand.NextInt(4) : 0;
            for (var i = 0; i < 4; i++)
            {
                var index = (start + i) % 4;
                foreach (var nex in data.NextSpawn.Room.Rotations[index].Doors.Where(nex => nex.Facing.Equals(current.Facing.Opposite)))
                {
                    data.Schematic = data.NextSpawn.Room.Rotations[index];
                    // the new spawn position is the original new door position - the offset from the origin of the door position
                    data.NextSpawn.Position = data.NextSpawn.OrigPosition - nex.Position;

                    // get the area that the new room will occupie
                    var area = new Cuboidi(data.NextSpawn.Position, data.NextSpawn.Position.AddCopy(data.Schematic.SizeX, data.Schematic.SizeY, data.Schematic.SizeZ));

                    // if is valid add new location to blocked area else find another one
                    if (!CanSpawn(data, area)) continue;
                    
                    if (removeDoorPos)
                    {
                        data.DoorPos.Remove(current);
                    }
                    if (!data.Initialized)
                    {
                        data.GeneratedRooms.Add(area);
                    }
                    return true;
                }
            }
            return false;
        }

        private bool CanSpawn(DungeonData data, Cuboidi area)
        {
            if (data.DungeonConfig.OnlyBelowSurface)
            {
                var topPositions = new List<BlockPos>
                {
                    new BlockPos(area.X1, area.Y2, area.Z1),
                    new BlockPos(area.X1, area.Y2, area.Z2),
                    new BlockPos(area.X2, area.Y2, area.Z1),
                    new BlockPos(area.X2, area.Y2, area.Z2)
                };
                
                foreach (var pos in topPositions)
                {
                    var height = _chunkGenBlockAccessor.GetTerrainMapheightAt(pos);
                    if (height <= pos.Y)
                        return false;
                }
            }
            
            return data.GeneratedRooms.All(room => !room.Intersects(area)) && data.GeneratedStructures.All(structure => !structure.Location.Intersects(area));
        }

        private void GenEntrance(DungeonData data, int x, int z, int startYSize, int rotation)
        {
            // get original start pos with out center adjustment for start room
            data.Schematic = data.DungeonConfig.Stairs.Rotations[rotation];
            data.NextSpawn.Position.Set(x, data.NextSpawn.Position.Y + startYSize, z);

            //if height is 0 it means that this chunk is not yet generated so nothing todo here
            var height = _chunkGenBlockAccessor.GetTerrainMapheightAt(data.NextSpawn.Position);
            // copy startpos for collision detection
            BlockPos startPos = null;
            if (!data.Initialized)
            {
                startPos = data.NextSpawn.Position.Copy();
            }
            // adjust after getting height
            data.Schematic.AdjustStartPos(data.NextSpawn.Position, EnumOrigin.BottomCenter);
            var startRoomTop = GetRandomRoom(data.DungeonConfig.StartRoomsTop);

            if (height == 0)
            {
                return;
            }
            if (x / _chunkSize == data.ChunkX && z / _chunkSize == data.ChunkZ)
            {
                height += 1;
            }
            // Mod.Logger.VerboseDebug($"Height: {height}");

            var y = data.DungeonConfig.Stairs.Rotations[0].SizeY;
            var rot = rotation;
            while (data.NextSpawn.Position.Y < height)
            {
                data.DungeonConfig.Stairs.Rotations[rot].Place(_chunkGenBlockAccessor, _api.World, data);

                data.NextSpawn.Position.Y += y;
                if (data.DungeonConfig.StairsRotation)
                {
                    rot = (rot + 1) % 4;
                }
            }

            if (!data.Initialized)
            {
                startPos.X -= data.Schematic.SizeX / 2;
                startPos.Z -= data.Schematic.SizeZ / 2;
                var area = new Cuboidi(startPos, data.NextSpawn.Position.AddCopy(data.DungeonConfig.Stairs.Rotations[rot].SizeX, data.DungeonConfig.Stairs.Rotations[rot].SizeY, data.DungeonConfig.Stairs.Rotations[rot].SizeZ));
                data.GeneratedRooms.Add(area);
            }

            // set data for start room top
            data.Schematic = startRoomTop.Rotations[rotation];
            data.NextSpawn.Position.Set(x, data.NextSpawn.Position.Y + data.DungeonConfig.StartTopOffsetY, z);
            // add start room to overlap check
            if (!data.Initialized)
            {
                var topRoomPos = data.NextSpawn.Position.Copy();
                topRoomPos.X -= data.Schematic.SizeX / 2;
                topRoomPos.Z -= data.Schematic.SizeZ / 2;
                var area = new Cuboidi(topRoomPos, topRoomPos.AddCopy(data.Schematic.SizeX, data.Schematic.SizeY, data.Schematic.SizeZ));
                data.GeneratedRooms.Add(area);
            }
            // adjust pos after adding collision check data
            data.Schematic.AdjustStartPos(data.NextSpawn.Position, EnumOrigin.BottomCenter);
            // place top room
            Place(data);

        }

        private void GenRoomEnds(DungeonData data, bool log = false)
        {
            //chose next room to spawn
            data.NextSpawn.Room = GetRandomRoom(data.DungeonConfig.EndRooms);
            foreach (var door in data.DoorPos)
            {
                if (GetNext(data, door, false, false))
                {
                    Place(data);
                }
                else
                {
                    if (_dungeonsConfig.Debug > 0 && log)
                    {
                        Mod.Logger.VerboseDebug($"failed end: {data.NextSpawn.Position}");
                    }
                }
            }
            data.DoorPos.Clear();
        }

        private void Place(DungeonData data)
        {
            // BlockSchematic schematic;
            // get the correct next room rotation based on the facing
            // schematic = GetRoomRotation(data);
            data.Schematic.Place(_chunkGenBlockAccessor, _api.World, data);

            foreach (var doorPos in data.Schematic.Doors.Where(pos => !(data.NextSpawn.Position + pos.Position).Equals(data.NextSpawn.OrigPosition)))
            {
                data.DoorPos.Add(new DoorPos(data.NextSpawn.Position + doorPos.Position, doorPos.Facing));
            }
        }
    }
}