#define DEBUG_WIREFRAME
using System.Collections.Generic;
using System.Linq;
using th3dungeon.Data;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Client.NoObf;

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

        private readonly Vec4f _debugColorH = new Vec4f(1f, 0f, 0f, 1f);

        private bool _debugDungeonEnabled;

        private bool _debugBoxesEnabled;

        private readonly List<Cuboidi> _debugBoxes = new List<Cuboidi>
                            {
                                new Cuboidi(512000, 0, 512000, 512000 + 32, 256, 512000 + 32),
                                new Cuboidi(512000, 4 * 32, 512000, 512000 + 32, 5 * 32, 512000 + 32),
                                new Cuboidi(512000, 3 * 32, 512000, 512000 + 32, 4 * 32, 512000 + 32)
                            };
#endif
        public override bool ShouldLoad(EnumAppSide side)
        {
#if DEBUG_WIREFRAME
            return true;
#else
            return side == EnumAppSide.Server;
#endif
        }

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

#if DEBUG_WIREFRAME
            _serverNetworkChannel = api.Network.RegisterChannel("th3dungeon-debug");
            _serverNetworkChannel.RegisterMessageType(typeof(List<Cuboidi>));
            _api.Event.PlayerNowPlaying += OnPlayerNowPlaying;
#endif
        }

#if DEBUG_WIREFRAME
        private void OnPlayerNowPlaying(IServerPlayer byPlayer)
        {
            _serverNetworkChannel.SendPacket(_generatedRoomsC, byPlayer);
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

            api.Event.RegisterRenderer(dummyRenderer, EnumRenderStage.Opaque, "dungeon-render");
            api.RegisterCommand("debugboxes", string.Empty, string.Empty, (int groupId, CmdArgs args) => { _debugBoxesEnabled = !_debugBoxesEnabled; });
            api.RegisterCommand("debugdungeon", string.Empty, string.Empty, (int groupId, CmdArgs args) => { _debugDungeonEnabled = !_debugDungeonEnabled; });

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
            if (_debugDungeonEnabled && _generatedRoomsC != null)
            {
                foreach (var room in _generatedRoomsC)
                {
                    var halfSizeX = room.SizeX / 2f;
                    var halfSizeY = room.SizeY / 2f;
                    var halfSizeZ = room.SizeZ / 2f;
                    _drawWireframeCube.Render(_game, room.X1 + halfSizeX, room.Y1 + halfSizeY, room.Z1 + halfSizeZ, halfSizeX, halfSizeY, halfSizeZ, 4f, _debugColor);
                }
            }

            if (_debugBoxesEnabled && _debugBoxes != null)
            {
                foreach (var room in _debugBoxes)
                {
                    var halfSizeX = room.SizeX / 2f;
                    var halfSizeY = room.SizeY / 2f;
                    var halfSizeZ = room.SizeZ / 2f;
                    _drawWireframeCube.Render(_game, room.X1 + halfSizeX, room.Y1 + halfSizeY, room.Z1 + halfSizeZ, halfSizeX, halfSizeY, halfSizeZ, 4f, _debugColorH);
                }
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
            // DungeonsConfig = _api.LoadModConfig<DungeonsConfig>("th3dungeonconfig.json");
            _dungeonsConfig = _api.Assets.Get(new AssetLocation(Mod.Info.ModID, "worldgen/dungeon/th3dungeonconfig.json")).ToObject<DungeonsConfig>();
            if (_dungeonsConfig == null)
            {
                Mod.Logger.Fatal($"DungeonsConfigs not found check your ModConfig folder and create a th3dungeonconfig.json");
            }
            float sum = 0;
            _dungeonsConfig.Dungeons.ForEach((dungeon) => sum += dungeon.Chance);
            if (sum != 1)
            {
                Mod.Logger.Fatal($"DungeonsConfigs do not add up to 1. [{sum}]");
            }
            _chunkRange = _dungeonsConfig.ChunkRange;

            _dungeonsConfig.Dungeons.ForEach(dungeon =>
            {

                dungeon.Rooms = new Dictionary<string, List<DungeonRoom>>();
                sum = 0;
                dungeon.Categories.ForEach((cat) => sum += cat.Chance);
                if (sum != 1)
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

                var endroomsPath = new AssetLocation(dungeon.EndRoomPath);
                var endrooms = _api.Assets.GetMany<BlockSchematic>(_api.Logger, endroomsPath.Path, endroomsPath.Domain);
                dungeon.EndRooms = new List<DungeonRoom>();
                foreach (var asset in endrooms)
                {
                    dungeon.EndRooms.Add(new DungeonRoom(_api, asset.Value, _chunkGenBlockAccessor, asset.Key.Path));
                }

                var basePath = new AssetLocation(dungeon.BasePath);
                foreach (var cat in dungeon.Categories)
                {
                    var assets = _api.Assets.GetMany<BlockSchematic>(_api.Logger, basePath.Path + cat.Name + "/", basePath.Domain);
                    var catRooms = new List<DungeonRoom>();
                    foreach (var asset in assets)
                    {
                        catRooms.Add(new DungeonRoom(_api, asset.Value, _chunkGenBlockAccessor, asset.Key.Path));
                    }
                    dungeon.Rooms.Add(cat.Name, catRooms);
                }
            });

            // RuntimeEnv.DebugOutOfRangeBlockAccess = true;
        }

        protected void GenChunkColumn(IServerChunk[] chunks, int chunkX, int chunkZ, ITreeAttribute chunkGenParams = null)
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
        protected void GenDungeonCheck(DungeonData data, int dx, int dz)
        {
            data.DungeonConfig = ChooseDungeon();
            if (_dungeonsConfig.Debug)
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

        protected void GenDungeon(DungeonData data, int dx, int dz)
        {
            var chunkXd = data.ChunkX + dx;
            var chunkZd = data.ChunkZ + dz;
            // error with top rooms so we place in center
            // int x = chunkXd * _chunkSize + _chunkRand.NextInt(_chunkSize);
            // int z = chunkZd * _chunkSize + _chunkRand.NextInt(_chunkSize);
            var x = chunkXd * _chunkSize + 15;
            var z = chunkZd * _chunkSize + 15;

            //choose initial room
            data.NextSpawn.Room = GetRandomRoom(data.DungeonConfig.StartRooms);

            // choose intital rotation
            var startRoomRotation = _chunkRand.NextInt(4);
            // int startRoomRotation = 0;
            data.Schematic = data.NextSpawn.Room.Rotations[startRoomRotation];

            // take sealevel since that should be consistant (surface will change depending if blocks are added ontop while generating)
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

            //adjust start pos after intial room cuboid is added
            data.Schematic.AdjustStartPos(data.NextSpawn.Position, EnumOrigin.BottomCenter);

            //spawn initial room
            Place(data);

            if (data.DungeonConfig.GenerateEntrance)
            {
                GenEntrance(data, x, z, data.Schematic.SizeY, startRoomRotation);
            }

            var placedRooms = 0;

            for (var i = 0; i < data.DungeonConfig.RoomsToGenerate; i++)
            {
                if (data.DoorPos.Count > 0)
                {
                    //choos next room to spawn
                    data.NextSpawn.Room = ChooseRoom(data);

                    //choose next door pos to gen next room
                    var ni = _chunkRand.NextInt(data.DoorPos.Count);
                    var current = data.DoorPos.ElementAt(ni);

                    // get spawn pos offset from next room and previouse room and previous facing
                    if (GetNext(data, current))
                    {
                        placedRooms++;
                        Place(data);
                    }
                }
            }

            if (_dungeonsConfig.Debug && dx == 0 && dz == 0)
            {
                Mod.Logger.VerboseDebug($"placed: {placedRooms}");
                Mod.Logger.VerboseDebug($"pos: {x} {z}");
                Mod.Logger.VerboseDebug($"/tp {x - _api.WorldManager.MapSizeY / 2} 120 {z - _api.WorldManager.MapSizeY / 2}");
                Mod.Logger.VerboseDebug($"GeneratedRooms: {data.GeneratedRooms.Count}");
                Mod.Logger.VerboseDebug($"DoorPos.Count: {data.DoorPos.Count}");
            }

            GenRoomEnds(data, dx == 0 && dz == 0);

#if DEBUG_WIREFRAME
            if (dx == 0 && dz == 0)
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

            if (data.Reinforcements != null)
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
            // make the rotaions random and not in order
            int index;
            var start = randStart ? _chunkRand.NextInt(4) : 0;
            for (var i = 0; i < 4; i++)
            {
                index = (start + i) % 4;
                for (var j = 0; j < data.NextSpawn.Room.Rotations[index].Doors.Count; j++)
                {
                    var nex = data.NextSpawn.Room.Rotations[index].Doors[j];
                    if (nex.Facing.Equals(current.Facing.Opposite))
                    {
                        data.Schematic = data.NextSpawn.Room.Rotations[index];
                        // the new spawn position is the original new door position - the offset from the origin of the door position
                        data.NextSpawn.Position = data.NextSpawn.OrigPosition - nex.Position;

                        // get area that the new room will occupie
                        var area = new Cuboidi(data.NextSpawn.Position, data.NextSpawn.Position.AddCopy(data.Schematic.SizeX, data.Schematic.SizeY, data.Schematic.SizeZ));

                        // if is valid add new location to blocked area else find another one
                        if (CanSpawn(data, area))
                        {
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
                }
            }
            return false;
        }

        private bool CanSpawn(DungeonData data, Cuboidi area)
        {
            foreach (var room in data.GeneratedRooms)
            {
                if (room.Intersects(area))
                {
                    return false;
                }
            }
            foreach (var structure in data.GeneratedStructures)
            {
                if (structure.Location.Intersects(area))
                {
                    return false;
                }
            }
            return true;
        }

        public void GenEntrance(DungeonData data, int x, int z, int startYSize, int rotation)
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

            // TODO: fix getting the height
            if (height == 0)
            {
                // Mod.Logger.VerboseDebug($"height : 0 ");
                return;
            }
            if (x / _chunkSize == data.ChunkX && z / _chunkSize == data.ChunkZ)
            {
                height += 1;
                // Mod.Logger.VerboseDebug($"height : {height} | {data.ChunkX} {chunkZ}");
            }
            else
            {
                height -= startRoomTop.Rotations[rotation].GetHeightAtPos(startRoomTop.Rotations[rotation].SizeX / 2, startRoomTop.Rotations[rotation].SizeZ / 2) - 1;
                // Mod.Logger.VerboseDebug($"height offset : {height} | {data.ChunkX} {chunkZ}");
                return;
            }

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
                var topRoomePos = data.NextSpawn.Position.Copy();
                topRoomePos.X -= data.Schematic.SizeX / 2;
                topRoomePos.Z -= data.Schematic.SizeZ / 2;
                var area = new Cuboidi(topRoomePos, topRoomePos.AddCopy(data.Schematic.SizeX, data.Schematic.SizeY, data.Schematic.SizeZ));
                data.GeneratedRooms.Add(area);
            }
            // adjust pos after adding collision check data
            data.Schematic.AdjustStartPos(data.NextSpawn.Position, EnumOrigin.BottomCenter);
            // place top room
            Place(data);

        }

        private void GenRoomEnds(DungeonData data, bool log = false)
        {
            //choos next room to spawn
            data.NextSpawn.Room = GetRandomRoom(data.DungeonConfig.EndRooms);
            foreach (var door in data.DoorPos)
            {
                if (GetNext(data, door, false, false))
                {
                    Place(data);
                }
                else
                {
                    if (log)
                    {
                        Mod.Logger.VerboseDebug($"failed end: {data.NextSpawn.Position}");
                    }
                }
            }
            data.DoorPos.Clear();
        }

        public void Place(DungeonData data)
        {
            // BlockSchematic schematic;
            // get the correct next room rotation based on the facing
            // schematic = GetRoomRotation(data);
            data.Schematic.Place(_chunkGenBlockAccessor, _api.World, data);

            foreach (var doorpos in data.Schematic.Doors)
            {
                // only add the doors positions that was not the spawn position of the current room
                if (!(data.NextSpawn.Position + doorpos.Position).Equals(data.NextSpawn.OrigPosition))
                {
                    data.DoorPos.Add(new DoorPos(data.NextSpawn.Position + doorpos.Position, doorpos.Facing));
                }
            }
        }
    }
}