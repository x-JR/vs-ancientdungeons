#define DEBUG_WIREFRAME
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using th3dungeon.Data;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;
using Vintagestory.ServerMods;

namespace th3dungeon
{
    public class Th3Dungeon : ModSystem
    {
        private const int DungeonWorldSeedOffset = 3132;

        private DungeonSaveData _dungeonSaveData;

        private ICoreServerAPI _api;

        private IWorldGenBlockAccessor _chunkGenBlockAccessor;

        private LCGRandom _chunkRand;

        private int _chunkSize;

        private DungeonsConfig _dungeonsConfig;

        private int _chunkRange = 5;

#if DEBUG_WIREFRAME
        private WireframeCube _drawWireframeCube;

        private ICoreClientAPI _capi;

        private IServerNetworkChannel _serverNetworkChannel;

        private List<Cuboidi> _generatedRoomsC;

        private IClientNetworkChannel _clientNetworkChannel;

        private readonly Vec4f _debugColor = new Vec4f(1f, 1f, 0f, 1f);

        private bool _debugDungeonEnabled;
#endif
        private RockStrataConfig RockStrata { get; set; }

        public override double ExecuteOrder()
        {
            return 0.51;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            _api = api;

            _chunkSize = api.WorldManager.ChunkSize;
            var loreContent = api.World.Config.GetBool("loreContent", true);
            if (!loreContent) return;

            _api.Event.GetWorldgenBlockAccessor(OnWorldGenBlockAccessor);
            _api.Event.InitWorldGenerator(InitWorldGen, "standard");

            _api.Event.ChunkColumnGeneration(GenChunkColumn, EnumWorldGenPass.TerrainFeatures, "standard");

            _api.Event.GameWorldSave += OnGameWorldSave;

            var dungeonDataFolder =
                Path.Combine(GamePaths.DataPath, "ModData", _api.WorldManager.SaveGame.SavegameIdentifier);
            if (!Directory.Exists(dungeonDataFolder))
            {
                Directory.CreateDirectory(dungeonDataFolder);
                _dungeonSaveData = new DungeonSaveData();
            }
            else
            {
                var dungeonDataFile = Path.Combine(dungeonDataFolder, "th3dungeon.bin");
                _dungeonSaveData = File.Exists(dungeonDataFile)
                    ? SerializerUtil.Deserialize<DungeonSaveData>(File.ReadAllBytes(dungeonDataFile))
                    : new DungeonSaveData();
            }

            // _api.ChatCommands.Create("ins").WithDescription("seas").RequiresPrivilege(Privilege.root).HandleWith((args) =>
            // {
            //     var cur = args.Caller.Player.Entity.Pos.AsBlockPos;
            //     var mapRegion = _api.WorldManager.GetMapRegion(cur.X / MagicNum.MapRegionSize, cur.Z / MagicNum.MapRegionSize);
            //     var mapRegionGeneratedStructures = mapRegion.GeneratedStructures;
            //     var a = mapRegionGeneratedStructures.Count;
            //     return  TextCommandResult.Success();
            // });

            _api.ChatCommands.Create("deleteth3dungeons")
                .WithAlias("dth3d")
                .WithDescription("deletes th3dungeons within the specified chunk radius from the generated list")
                .WithAdditionalInformation("mainly for testing but maybe helpful for regenerating when changing world gen params")
                .RequiresPrivilege(Privilege.root)
                .RequiresPlayer()
                .WithArgs(_api.ChatCommands.Parsers.OptionalInt("chunk_range", 10))
                .HandleWith((args) =>
                {
                    var distance = (int)args.Parsers[0].GetValue();
                    var chunkX = args.Caller.Player.Entity.Pos.AsBlockPos.X / 32;
                    var chunkZ = args.Caller.Player.Entity.Pos.AsBlockPos.Z / 32;
                    var pos = new ChunkPos();
                    var removed = 0;
                    for (var dx = -distance; dx <= distance; dx++)
                    {
                        for (var dz = -distance; dz <= distance; dz++)
                        {
                            pos.X = chunkX + dx;
                            pos.Z = chunkZ + dz;
                            if (_dungeonSaveData.GeneratedDungeons.Remove(_dungeonSaveData.GeneratedDungeons.Find(cp => cp.Equals(pos))))
                            {
                                removed++;
                                _dungeonSaveData.Modified = true;
                            }
                        }
                    }
                    return TextCommandResult.Success($"Removed {removed} dungeon spawn positions from th3dungeon.bin");
                });

            _api.ChatCommands.Create("mapth3dungeons")
                .WithAlias("mth3d")
                .WithDescription("adds a waypoint for every potential th3dungeon spawn within the specified chunk radius (not 100% accurate)")
                .RequiresPrivilege(Privilege.root)
                .RequiresPlayer()
                .WithArgs(_api.ChatCommands.Parsers.OptionalInt("chunk_range", 10))
                .HandleWith((args) =>
                {
                    var distance = (int)args.Parsers[0].GetValue();
                    var chunkRand = new LCGRandom(api.World.Seed + DungeonWorldSeedOffset);
                    var worldMapManager = _api.ModLoader.GetModSystem<WorldMapManager>();
                    if (!(worldMapManager.MapLayers.FirstOrDefault(l => l is WaypointMapLayer) is
                            WaypointMapLayer waypointMapLayer))
                    {
                        return TextCommandResult.Error("Couldn't find waypoint layer on minimap");
                    }

                    var chunkX = args.Caller.Player.Entity.Pos.AsBlockPos.X / 32;
                    var chunkZ = args.Caller.Player.Entity.Pos.AsBlockPos.Z / 32;
                    var addedWaypoints = 0;

                    var chunkPosListTmp = _dungeonSaveData.GeneratedDungeons.ToList();
                    for (var dx = -distance; dx <= distance; dx++)
                    {
                        for (var dz = -distance; dz <= distance; dz++)
                        {
                            chunkRand.InitPositionSeed(chunkX + dx, chunkZ + dz);
                            var chance = chunkRand.NextFloat();
                            float chanceStart = 0;
                            var dungeonF = -1;
                            for (var i = 0; i < _dungeonsConfig.Dungeons.Count; i++)
                            {
                                var dungeon = _dungeonsConfig.Dungeons[i];
                                if (chance >= chanceStart && chance < chanceStart + dungeon.Chance)
                                {
                                    dungeonF = i;
                                    break;
                                }

                                chanceStart += dungeon.Chance;
                            }

                            var spawnChance = chunkRand.NextFloat();
                            if (!(spawnChance <= _dungeonsConfig.Chance)) continue;

                            var newPos = new ChunkPos(chunkX + dx, chunkZ + dz);
                            var hasDungeon = chunkPosListTmp.Contains(newPos);
                            if (!hasDungeon)
                            {
                                if (chunkPosListTmp.Any(cpos => cpos.Distance(newPos) <= _dungeonsConfig.MinDistanceChunks))
                                {
                                    continue;
                                }

                                chunkPosListTmp.Add(newPos);
                            }

                            var x = 32 * (chunkX + dx) + 15;
                            var z = 32 * (chunkZ + dz) + 15;
                            if(waypointMapLayer.Waypoints.Any(w => w.Position.X == x && w.Position.Y == 100 && w.Position.Z == z && w.Title.Contains("Th3Dungeon:"))) continue;
                            var wp = new Waypoint
                            {
                                Color = -23296, // Orange
                                OwningPlayerUid = args.Caller.Player.PlayerUID,
                                Position = new Vec3d(x, 100, z),
                                Title = "Th3Dungeon: " + dungeonF,
                                Icon = "ruins",
                                Pinned = false,
                            };
                            Mod.Logger.VerboseDebug($"/tp ={x} 120 ={z}");
                            waypointMapLayer.Waypoints.Add(wp);
                            addedWaypoints++;
                        }
                    }

                    return TextCommandResult.Success($"Added {addedWaypoints} waypoints");
                })
                .Validate();

#if DEBUG_WIREFRAME
            _serverNetworkChannel = api.Network.RegisterChannel("th3dungeon-debug");
            _serverNetworkChannel.RegisterMessageType(typeof(List<Cuboidi>));
            _api.Event.PlayerNowPlaying += OnPlayerNowPlaying;
#endif
        }

        private void OnGameWorldSave()
        {
            if (!_dungeonSaveData.Modified) return;

            var dungeonDataFile = Path.Combine(GamePaths.DataPath, "ModData",
                _api.WorldManager.SaveGame.SavegameIdentifier, "th3dungeon.bin");
            var data = SerializerUtil.Serialize(_dungeonSaveData);
            File.WriteAllBytes(dungeonDataFile, data);
            _dungeonSaveData.Modified = false;
        }

#if DEBUG_WIREFRAME
        private void OnPlayerNowPlaying(IServerPlayer byPlayer)
        {
            if (_dungeonsConfig != null && _dungeonsConfig.Debug > 1 && _generatedRoomsC != null)
            {
                _serverNetworkChannel.SendPacket(_generatedRoomsC, byPlayer);
            }
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            _capi = api;
            _drawWireframeCube = WireframeCube.CreateUnitCube(_capi, -1);
            var dummyRenderer = new DummyRenderer
            {
                action = OnRender,
                RenderOrder = 0.5
            };
            if (_debugDungeonEnabled)
            {
                api.Event.RegisterRenderer(dummyRenderer, EnumRenderStage.Opaque, "dungeon-render");
            }

            api.ChatCommands.Create("debugdungeon")
                .WithAlias("dth3d")
                .WithDescription("Show debug boxes for dungeon rooms. Requires Debug: 2 or greater")
                .HandleWith((args) =>
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

                    return TextCommandResult.Success($"Debug Dungeon: {_debugDungeonEnabled}");
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
                var halfSizeX = room.SizeX ;
                var halfSizeY = room.SizeY ;
                var halfSizeZ = room.SizeZ ;
                _drawWireframeCube.Render(_capi, room.X1 , room.Y1 , room.Z1 ,
                    halfSizeX, halfSizeY, halfSizeZ, 4f, _debugColor);
            }
        }
#endif
        private void OnWorldGenBlockAccessor(IChunkProviderThread chunkProvider)
        {
            _chunkGenBlockAccessor = chunkProvider.GetBlockAccessor(true);
        }

        private void InitWorldGen()
        {
            _chunkRand = new LCGRandom(_api.WorldManager.Seed + DungeonWorldSeedOffset);
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

                var dungeonsConfigs =
                    _api.Assets.GetMany<DungeonsConfig>(Mod.Logger, "worldgen/th3dungeon/th3dungeonconfig.json");

                var th3DungeonConfig = dungeonsConfigs.First(d =>
                    d.Key.Domain.Equals(Mod.Info.ModID)
                ).Value;

                if (th3DungeonConfig != null)
                {
                    _dungeonsConfig.Chance = th3DungeonConfig.Chance;
                    _dungeonsConfig.ChunkRange = th3DungeonConfig.ChunkRange;
                    _dungeonsConfig.Debug = th3DungeonConfig.Debug;
                    _dungeonsConfig.MinDistanceChunks = th3DungeonConfig.MinDistanceChunks;
                }

                if (modConfig != null)
                {
                    _dungeonsConfig.ChunkRange = modConfig.ChunkRange != 0 ? modConfig.ChunkRange : _dungeonsConfig.ChunkRange;
                    _dungeonsConfig.Chance = modConfig.Chance != 0 ? modConfig.Chance : _dungeonsConfig.Chance;
                    _dungeonsConfig.Debug = modConfig.Debug != 0 ? modConfig.Debug : _dungeonsConfig.Debug;
                    _dungeonsConfig.MinDistanceChunks = modConfig.MinDistanceChunks != 0 ? modConfig.MinDistanceChunks : _dungeonsConfig.MinDistanceChunks;
                }


                // merge all configs
                var dungeonCount = dungeonsConfigs.Count;
                if (modConfig != null && modConfig.ExcludeTh3Dungeons &&
                    dungeonsConfigs.Any(config => config.Key.Domain.Equals(Mod.Info.ModID)))
                {
                    dungeonCount--;
                }

                foreach (var dungeonConfig in dungeonsConfigs)
                {
                    if (modConfig != null && modConfig.ExcludeTh3Dungeons &&
                        dungeonConfig.Key.Domain.Equals(Mod.Info.ModID))
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
                        Mod.Logger.Fatal(
                            $"DungeonsConfig {dungeonConfig.Key.Domain}:{dungeonConfig.Key.Path} does not add up to 1. [{sumModDungeon}]");
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
                Mod.Logger.Fatal(
                    $"DungeonsConfigs not found check your ModConfig folder and create a th3dungeonconfig.json");
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
                var startRooms =
                    _api.Assets.GetMany<BlockSchematic>(_api.Logger, startRoomsPath.Path, startRoomsPath.Domain);
                dungeon.StartRooms = new List<DungeonRoom>();
                foreach (var asset in startRooms)
                {
                    dungeon.StartRooms.Add(new DungeonRoom(_api, asset.Value, _chunkGenBlockAccessor, asset.Key.Path));
                }

                if (dungeon.GenerateEntrance)
                {
                    var startRoomsTopPath = new AssetLocation(dungeon.StartRoomTopPath);
                    var startRoomsTop = _api.Assets.GetMany<BlockSchematic>(_api.Logger, startRoomsTopPath.Path,
                        startRoomsTopPath.Domain);
                    dungeon.StartRoomsTop = new List<DungeonRoom>();
                    foreach (var asset in startRoomsTop)
                    {
                        dungeon.StartRoomsTop.Add(new DungeonRoom(_api, asset.Value, _chunkGenBlockAccessor,
                            asset.Key.Path));
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
                    var assets = _api.Assets.GetMany<BlockSchematic>(_api.Logger, basePath.Path + cat.Name + "/",
                        basePath.Domain);
                    var catRooms = assets.Select(asset =>
                        new DungeonRoom(_api, asset.Value, _chunkGenBlockAccessor, asset.Key.Path)).ToList();
                    dungeon.Rooms.Add(cat.Name, catRooms);
                }

                if (dungeon.ReplaceWithRockType == null) return;

                dungeon.ResolvedReplaceWithRockType = WorldGenStructuresConfigBase.ResolveRockTypeRemaps(dungeon.ReplaceWithRockType,RockStrata,_api);
            });
            Mod.Logger.Event($"{_dungeonsConfig.Dungeons.Count} Dungeons Loaded");
            // RuntimeEnv.DebugOutOfRangeBlockAccess = true; 1985799215
        }

        private void GenChunkColumn(IChunkColumnGenerateRequest request)
        {
            var data = new DungeonData(request);

            for (var dx = -_chunkRange; dx <= _chunkRange; dx++)
            {
                for (var dz = -_chunkRange; dz <= _chunkRange; dz++)
                {
                    _chunkRand.InitPositionSeed(request.ChunkX + dx, request.ChunkZ + dz);
                    data.Dx = dx;
                    data.Dz = dz;
                    data.ChunkXd = data.ChunkX + dx;
                    data.ChunkZd = data.ChunkZ + dz;
                    GenDungeonCheck(data);
                    // if (data.Logs != null)
                    // {
                    //     Mod.Logger.VerboseDebug(string.Join("\n", data.Logs));
                    //     data.Logs = null;
                    // }
                }
            }
        }

        private void GenDungeonCheck(DungeonData data)
        {
            data.DungeonConfig = ChooseDungeon();
            if (_dungeonsConfig.Debug == 3)
            {
                if (data.ChunkXd == 16000 && data.ChunkZd == 16000)
                {
                    GenDungeon(data);
                }
            }
            else if (_chunkRand.NextFloat() <= _dungeonsConfig.Chance)
            {
                var newPos = new ChunkPos(data.ChunkXd, data.ChunkZd);
                var hasDungeon = _dungeonSaveData.GeneratedDungeons.Contains(newPos);
                if (!hasDungeon)
                {
                    if (_dungeonSaveData.GeneratedDungeons.Any(cpos => cpos.Distance(newPos) <= _dungeonsConfig.MinDistanceChunks))
                    {
                        return;
                    }
                    
                    var worldChunk = _chunkGenBlockAccessor.GetChunk(data.ChunkXd, 0, data.ChunkZd);
                    var height = worldChunk?.MapChunk.WorldGenTerrainHeightMap[15 * _chunkSize + 15];
                    if(height == null || height < _api.World.SeaLevel)
                    {
                        return;
                    }
                }

                // data.Logs = new List<string> { $"X={data.ChunkX} + {data.Dx} : Z={data.ChunkZ} + {data.Dz}" };
                var didGenerate = GenDungeon(data);
                if (!hasDungeon && didGenerate)
                {
                    _dungeonSaveData.GeneratedDungeons.Add(newPos);
                    _dungeonSaveData.Modified = true;
                }
            }
        }

        private bool GenDungeon(DungeonData data)
        {
            // error with top rooms so we place in center
            // var x = chunkXd * _chunkSize + _chunkRand.NextInt(_chunkSize);
            // var z = chunkZd * _chunkSize + _chunkRand.NextInt(_chunkSize);
            var x = data.ChunkXd * _chunkSize + 15;
            var z = data.ChunkZd * _chunkSize + 15;

            //choose initial room
            data.NextSpawn.Room = GetRandomRoom(data.DungeonConfig.StartRooms);

            // choose initial rotation
            var startRoomRotation = _chunkRand.NextInt(4);
            // int startRoomRotation = 0;
            data.Schematic = data.NextSpawn.Room.Rotations[startRoomRotation];

            // take sea level since that should be consistent (surface will change depending if blocks are added on top while generating)
            data.NextSpawn.Position = new BlockPos(x, _api.World.SeaLevel + data.DungeonConfig.SealevelOffset, z);

            if (data.DungeonConfig.GenerateEntrance && data.DungeonConfig.MaxYDiff > 0 && !CanSpawnTopHere(data))
            {
                return false;
            }

            // add start room to overlap check
            if (!data.Initialized)
            {
                var collisionPos = data.NextSpawn.Position.Copy();
                collisionPos.X -= data.Schematic.SizeX / 2;
                collisionPos.Z -= data.Schematic.SizeZ / 2;
                var area = new Cuboidi(collisionPos,
                    collisionPos.AddCopy(data.Schematic.SizeX, data.Schematic.SizeY, data.Schematic.SizeZ));
                data.GeneratedRooms.Add(area);
            }

            //adjust start pos after initial room cuboid is added
            data.Schematic.AdjustStartPos(data.NextSpawn.Position, EnumOrigin.BottomCenter);
            //Mod.Logger.VerboseDebug(string.Join("\n ", data.Chunks[0].MapChunk.MapRegion.GeneratedStructures.Select(g => $"{g.Code}: {g.Location}")));
            //spawn initial room
            Place(data);

            if (data.DungeonConfig.GenerateEntrance)
            {
                GenEntrance(data, x, z, data.Schematic.SizeY, startRoomRotation);
            }

            for (var i = 0; i < data.DungeonConfig.RoomsToGenerate; i++)
            {
                if (data.DoorPos.Count <= 0) break;

                //chose next room to spawn
                data.NextSpawn.Room = ChooseRoom(data);

                //choose next door pos to gen next room
                var ni = _chunkRand.NextInt(data.DoorPos.Count);
                var current = data.DoorPos.ElementAt(ni);

                // get spawn pos offset from next room and previous room and previous facing
                if (!GetNext(data, current)) continue;

                Place(data);
            }

            if (_dungeonsConfig.Debug > 0 && data.Dx == 0 && data.Dz == 0)
            {
                Mod.Logger.VerboseDebug($"GeneratedRooms: {data.GeneratedRooms.Count}");
                Mod.Logger.VerboseDebug($"DoorPos.Count: {data.DoorPos.Count}");
                Mod.Logger.VerboseDebug($"/tp ={x} 120 ={z}");
            }

            GenRoomEnds(data, data.Dx == 0 && data.Dz == 0);

#if DEBUG_WIREFRAME
            if (_dungeonsConfig.Debug > 1 && data.Dx == 0 && data.Dz == 0)
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

            if (data.Reinforcements == null) return true;
            {
                for (var i = 0; i < data.Chunks.Length; i++)
                {
                    if (data.Reinforcements[i] != null)
                        data.Chunks[i].SetModdata("reinforcements", data.Reinforcements[i]);
                }
            }
            return true;
        }

        private bool CanSpawnTopHere(DungeonData data)
        {
            var startPos = data.Schematic.GetStartPos(data.NextSpawn.Position, EnumOrigin.BottomCenter);
            
            int centerY = _chunkGenBlockAccessor.GetTerrainMapheightAt(data.NextSpawn.Position);
            int wdt = data.Schematic.SizeX;
            int len = data.Schematic.SizeZ;

            var tmpPos = new BlockPos(startPos.X, 0, startPos.Z, startPos.dimension);
            int topLeftY = _chunkGenBlockAccessor.GetTerrainMapheightAt(tmpPos);
            tmpPos.Y = topLeftY + 1;
            if (_chunkGenBlockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return false;

            tmpPos.Set(startPos.X + wdt, 0, startPos.Z);
            int topRightY = _chunkGenBlockAccessor.GetTerrainMapheightAt(tmpPos);
            tmpPos.Y = topRightY + 1;
            if (_chunkGenBlockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return false;

            tmpPos.Set(startPos.X, 0, startPos.Z + len);
            int botLeftY = _chunkGenBlockAccessor.GetTerrainMapheightAt(tmpPos);
            tmpPos.Y = botLeftY + 1;
            if (_chunkGenBlockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return false;

            tmpPos.Set(startPos.X + wdt, 0, startPos.Z + len);
            int botRightY = _chunkGenBlockAccessor.GetTerrainMapheightAt(tmpPos);
            tmpPos.Y = botRightY + 1;
            if (_chunkGenBlockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return false;

            int maxY = GameMath.Max(centerY, topLeftY, topRightY, botLeftY, botRightY);
            int minY = GameMath.Min(centerY, topLeftY, topRightY, botLeftY, botRightY);
            // improve flatness check for larger structures
            if (data.Schematic.SizeX >= 30)
            {
                var size = (int)(data.Schematic.SizeX * 0.15 + 8);
                for (int i = size; i < data.Schematic.SizeX; i+=size)
                {
                    tmpPos.Set(startPos.X + i, 0, startPos.Z);
                    var topSide = _chunkGenBlockAccessor.GetTerrainMapheightAt(tmpPos);

                    tmpPos.Set(startPos.X + i, 0, startPos.Z + len);
                    var botSide = _chunkGenBlockAccessor.GetTerrainMapheightAt(tmpPos);

                    tmpPos.Set(startPos.X + i, 0, startPos.Z + len / 2);
                    var centerSide = _chunkGenBlockAccessor.GetTerrainMapheightAt(tmpPos);

                    maxY = GameMath.Max(maxY, topSide, botSide, centerSide);
                    minY = GameMath.Min(minY, topSide, botSide, centerSide);
                }
            }
            else if (data.Schematic.SizeX >= 15) // check center on X
            {
                var size = data.Schematic.SizeX / 2;
                tmpPos.Set(startPos.X + size, 0, startPos.Z);
                var topSide = _chunkGenBlockAccessor.GetTerrainMapheightAt(tmpPos);

                tmpPos.Set(startPos.X + size, 0, startPos.Z + len);
                var botSide = _chunkGenBlockAccessor.GetTerrainMapheightAt(tmpPos);

                tmpPos.Set(startPos.X + size, 0, startPos.Z + len / 2);
                var centerSide = _chunkGenBlockAccessor.GetTerrainMapheightAt(tmpPos);

                maxY = GameMath.Max(maxY, topSide, botSide, centerSide);
                minY = GameMath.Min(minY, topSide, botSide, centerSide);
            }
            if (data.Schematic.SizeZ >= 30)
            {
                var size = (int)(data.Schematic.SizeZ * 0.15 + 8);
                for (int i = size; i < data.Schematic.SizeZ; i+=size)
                {
                    tmpPos.Set(startPos.X + wdt, 0, startPos.Z + i);
                    var rightSide = _chunkGenBlockAccessor.GetTerrainMapheightAt(tmpPos);

                    tmpPos.Set(startPos.X, 0, startPos.Z + i);
                    var leftSide = _chunkGenBlockAccessor.GetTerrainMapheightAt(tmpPos);

                    tmpPos.Set(startPos.X + wdt / 2, 0, startPos.Z + i);
                    var centerSide = _chunkGenBlockAccessor.GetTerrainMapheightAt(tmpPos);

                    maxY = GameMath.Max(maxY, rightSide, leftSide, centerSide);
                    minY = GameMath.Min(minY, rightSide, leftSide, centerSide);
                }
            }
            else if (data.Schematic.SizeZ >= 15) // check center on Z
            {
                var size = data.Schematic.SizeZ / 2;
                tmpPos.Set(startPos.X + wdt, 0, startPos.Z + size);
                var rightSide = _chunkGenBlockAccessor.GetTerrainMapheightAt(tmpPos);

                tmpPos.Set(startPos.X, 0, startPos.Z + size);
                var leftSide = _chunkGenBlockAccessor.GetTerrainMapheightAt(tmpPos);

                tmpPos.Set(startPos.X + wdt / 2, 0, startPos.Z + size);
                var centerSide = _chunkGenBlockAccessor.GetTerrainMapheightAt(tmpPos);

                maxY = GameMath.Max(maxY, rightSide, leftSide, centerSide);
                minY = GameMath.Min(minY, rightSide, leftSide, centerSide);
            }

            int diff = Math.Abs(maxY - minY);
            if (diff > 5) return false;

            return true;
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
                foreach (var nex in data.NextSpawn.Room.Rotations[index].Doors
                             .Where(nex => nex.Facing.Equals(current.Facing.Opposite)))
                {
                    data.Schematic = data.NextSpawn.Room.Rotations[index];
                    // the new spawn position is the original new door position - the offset from the origin of the door position
                    data.NextSpawn.Position = data.NextSpawn.OrigPosition - nex.Position;

                    // get the area that the new room will occupy
                    var area = new Cuboidi(data.NextSpawn.Position,
                        data.NextSpawn.Position.AddCopy(data.Schematic.SizeX, data.Schematic.SizeY,
                            data.Schematic.SizeZ));

                    // if is valid add new location to blocked area else find another one
                    if (!CanSpawn(data, area)) continue;

                    if (removeDoorPos)
                    {
                        data.DoorPos.Remove(current);
                    }


                    if (!data.Initialized)
                    {
                        data.GeneratedRooms.Add(area);
                        if (area.X1 / _chunkGenBlockAccessor.ChunkSize == data.ChunkX && area.Z1 / _chunkGenBlockAccessor.ChunkSize == data.ChunkZ )
                        {
                            var mapRegion = _chunkGenBlockAccessor.GetMapRegion(area.X1 / _chunkGenBlockAccessor.RegionSize , area.Z1 / _chunkGenBlockAccessor.RegionSize);
                            var structure = new GeneratedStructure
                            {
                                Location = area,
                                SuppressRivulets = data.DungeonConfig.SuppressRivulets,
                                Code = $"th3dungeon-{data.NextSpawn.Room.Name}"
                            };
                            mapRegion.GeneratedStructures.Add(structure);
                        }
                    }
                    // data.Logs.Add($"{data.NextSpawn.Room.Name}: {area}");

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

                if ((from pos in topPositions let height = _chunkGenBlockAccessor.GetTerrainMapheightAt(pos) where height > 0 && height <= pos.Y select pos).Any())
                {
                    return false;
                }
            }

            return data.GeneratedRooms.All(room => !room.Intersects(area)) &&
                   data.Chunks[0].MapChunk.MapRegion.GeneratedStructures.Where(s => !s.Code.StartsWith("th3")).All(structure => !structure.Location.Intersects(area));
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
                var area = new Cuboidi(startPos,
                    data.NextSpawn.Position.AddCopy(data.DungeonConfig.Stairs.Rotations[rot].SizeX,
                        data.DungeonConfig.StartTopOffsetY,
                        data.DungeonConfig.Stairs.Rotations[rot].SizeZ));
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
                var area = new Cuboidi(topRoomPos,
                    topRoomPos.AddCopy(data.Schematic.SizeX, data.Schematic.SizeY, data.Schematic.SizeZ));
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
            data.Schematic.PlaceDecors(_chunkGenBlockAccessor, data);

            foreach (var doorPos in data.Schematic.Doors.Where(pos =>
                         !(data.NextSpawn.Position + pos.Position).Equals(data.NextSpawn.OrigPosition)))
            {
                data.DoorPos.Add(new DoorPos(data.NextSpawn.Position + doorPos.Position, doorPos.Facing));
            }
        }
    }
}