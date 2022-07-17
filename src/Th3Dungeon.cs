// #define DEBUG_WIREFRAME
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Client.NoObf;

namespace Th3Dungeon
{
    public class Th3Dungeon : ModSystem
    {
        private ICoreServerAPI _api;

        private IWorldGenBlockAccessor _chunkGenBlockAccessor;

        private LCGRandom _chunkRand;

        private int _chunkSize;

        private Th3DungeonConfig Th3DungeonConfig;

        private int _chunkRange = 5;

        private IBlockAccessor _worldBlockAccessor;

#if DEBUG_WIREFRAME
        private DrawWireframeCube drawWireframeCube;

        private ClientMain game;

        private IServerNetworkChannel serverNetworkChannel;

        public List<Cuboidi> GeneratedRoomsC;

        IClientNetworkChannel clientNetworkChannel;

        private Vec4f Col = new Vec4f(255f, 255f, 0f, 255f);
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
            _worldBlockAccessor = _api.World.BlockAccessor;

            _api.Event.GetWorldgenBlockAccessor(OnWorldGenBlockAccessor);
            _api.Event.InitWorldGenerator(InitWorldGen, "standard");

            _api.Event.ChunkColumnGeneration(GenChunkColumn, EnumWorldGenPass.TerrainFeatures, "standard");

#if DEBUG_WIREFRAME
            serverNetworkChannel = api.Network.RegisterChannel("th3dungeon-debug");
            serverNetworkChannel.RegisterMessageType(typeof(List<Cuboidi>));
            _api.Event.PlayerNowPlaying += OnPlayerNowPlaying;
#endif
        }
#if DEBUG_WIREFRAME
        private void OnPlayerNowPlaying(IServerPlayer byPlayer)
        {
            serverNetworkChannel.SendPacket(GeneratedRoomsC, byPlayer);
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            game = (ClientMain)api.World;
            drawWireframeCube = new DrawWireframeCube(game);
            DummyRenderer dummyRenderer = new DummyRenderer
            {
                action = OnRender
            };

            api.Event.RegisterRenderer(dummyRenderer, EnumRenderStage.Opaque, "dungeon-render");

            clientNetworkChannel = api.Network.RegisterChannel("th3dungeon-debug");
            clientNetworkChannel.RegisterMessageType(typeof(List<Cuboidi>));
            clientNetworkChannel.SetMessageHandler<List<Cuboidi>>(OnGeneratedRoomsReceiving);
        }

        private void OnGeneratedRoomsReceiving(List<Cuboidi> rooms)
        {
            GeneratedRoomsC = rooms;
        }

        private void OnRender(float obj)
        {
            if (GeneratedRoomsC != null)
            {

                foreach (var room in GeneratedRoomsC)
                {
                    float halfSizeX = room.SizeX / 2f;
                    float halfSizeY = room.SizeY / 2f;
                    float halfSizeZ = room.SizeZ / 2f;
                    drawWireframeCube.Render(game, room.X1 + halfSizeX, room.Y1 + halfSizeY, room.Z1 + halfSizeZ, halfSizeX, halfSizeY, halfSizeZ, 4f, Col);
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


            Th3DungeonConfig = _api.Assets.Get(new AssetLocation("th3dungeon", "worldgen/dungeon/Th3DungeonConfig.json")).ToObject<Th3DungeonConfig>();

            Th3DungeonConfig.Dungeons.ForEach(dungeon =>
            {

                dungeon.Rooms = new Dictionary<string, List<Th3DungeonRoom>>();
                float sum = 0;
                dungeon.Categories.ForEach((cat) => sum += cat.Chance);
                if (sum != 1)
                {
                    Mod.Logger.Fatal($"Th3DungeonConfig categories do not add up to 1. [{sum}]");
                }

                var startRoom = _api.Assets.Get<Th3BlockSchematic>(new AssetLocation(dungeon.StartRoomPath));
                dungeon.StartRoom = new Th3DungeonRoom(_api, startRoom, _chunkGenBlockAccessor, dungeon.StartRoomPath);

                if (dungeon.GenerateEntrance)
                {
                    var startRoomTop = _api.Assets.Get<Th3BlockSchematic>(new AssetLocation(dungeon.StartRoomTopPath));
                    dungeon.StartRoomTop = new Th3DungeonRoom(_api, startRoomTop, _chunkGenBlockAccessor, dungeon.StartRoomTopPath);

                    var stairs = _api.Assets.Get<Th3BlockSchematic>(new AssetLocation(dungeon.StairsPath));
                    dungeon.Stairs = new Th3DungeonRoom(_api, stairs, _chunkGenBlockAccessor, dungeon.StairsPath);
                }

                var endroom = _api.Assets.Get<Th3BlockSchematic>(new AssetLocation(dungeon.EndRoomPath));
                dungeon.EndRoom = new Th3DungeonRoom(_api, endroom, _chunkGenBlockAccessor, dungeon.EndRoomPath);
                foreach (var cat in dungeon.Categories)
                {
                    var assets = _api.Assets.GetMany<Th3BlockSchematic>(_api.Logger, "worldgen/dungeon/" + dungeon.Name + "/" + cat.Name, "th3dungeon");
                    var catRooms = new List<Th3DungeonRoom>();
                    foreach (var asset in assets)
                    {
                        catRooms.Add(new Th3DungeonRoom(_api, asset.Value, _chunkGenBlockAccessor, asset.Key.Path));
                    }
                    dungeon.Rooms.Add(cat.Name, catRooms);
                }
            });

            // RuntimeEnv.DebugOutOfRangeBlockAccess = true;
        }

        protected void GenChunkColumn(IServerChunk[] chunks, int chunkX, int chunkZ, ITreeAttribute chunkGenParams = null)
        {
            Th3DungeonData data = new Th3DungeonData();
            _chunkRand.InitPositionSeed(chunkX, chunkZ);
            _chunkRange = ChooseDungeon().ChunkRange;
            for (int dx = -_chunkRange; dx <= _chunkRange; dx++)
            {
                for (int dz = -_chunkRange; dz <= _chunkRange; dz++)
                {
                    _chunkRand.InitPositionSeed(chunkX + dx, chunkZ + dz);
                    GenDungeon(data, chunkX, chunkZ, dx, dz);
                }
            }
        }

        protected void GenDungeon(Th3DungeonData data, int chunkX, int chunkZ, int dx, int dz)
        {
            int chunkXd = chunkX + dx;
            int chunkZd = chunkZ + dz;

            // keep choosedungeon as the first call for random
            data.DungeonConfig = ChooseDungeon();

            if (_chunkRand.NextInt(1000) > 995)
            // spawn dungeon in first chunk
            // if (chunkXd == 16000 && chunkZd == 16000)
            {
                // int x = chunkXd * _chunkSize + _chunkRand.NextInt(_chunkSize);
                // int z = chunkZd * _chunkSize + _chunkRand.NextInt(_chunkSize);
                int x = chunkXd * _chunkSize + 15;
                int z = chunkZd * _chunkSize + 15;

                //choose initial room
                data.NextSpawn.Room = data.DungeonConfig.StartRoom;

                // choose intital rotation
                // int startRoomRotation = _chunkRand.NextInt(4);
                int startRoomRotation = 0;
                data.Schematic = data.NextSpawn.Room.Rotations[startRoomRotation];

                // take sealevel since that should be consistant (surface will change depending if blocks are added ontop while generating)
                data.NextSpawn.Position = new BlockPos(x, _api.World.SeaLevel - 20, z);

                // add start room to overlap check
                if (!data.Initialized)
                {
                    BlockPos collisionPos = data.NextSpawn.Position.Copy();
                    collisionPos.X -= data.Schematic.SizeX / 2;
                    collisionPos.Z -= data.Schematic.SizeZ / 2;
                    Cuboidi area = new Cuboidi(collisionPos, collisionPos.AddCopy(data.Schematic.SizeX, data.Schematic.SizeY, data.Schematic.SizeZ));
                    data.GeneratedRooms.Add(area);
                }

                //adjust start pos after intial room cuboid is added
                data.Schematic.AdjustStartPos(data.NextSpawn.Position, EnumOrigin.BottomCenter);

                //spawn initial room
                Place(data, chunkX, chunkZ);

                if (data.DungeonConfig.GenerateEntrance)
                {
                    GenEntrance(data, x, z, data.Schematic.SizeY, startRoomRotation, chunkX, chunkZ);
                }

                int a = 0;

                for (int i = 0; i < data.DungeonConfig.RoomsToGenerate; i++)
                {
                    if (data.DoorPos.Count > 0)
                    {
                        //choos next room to spawn
                        data.NextSpawn.Room = ChooseRoom(data);

                        //choose next door pos to gen next room
                        int ni = _chunkRand.NextInt(data.DoorPos.Count);
                        Th3DoorPos current = data.DoorPos.ElementAt(ni);

                        // get spawn pos offset from next room and previouse room and previous facing
                        if (GetNext(data, current))
                        {
                            a++;
                            Place(data, chunkX, chunkZ);
                        }
                    }
                }

                if (dx == 0 && dz == 0)
                {
                    Mod.Logger.VerboseDebug($"placed: {a}");
                    Mod.Logger.VerboseDebug($"pos: {x} {z}");
                    Mod.Logger.VerboseDebug($"/tp {x - 512000} 120 {z - 512000}");
                    Mod.Logger.VerboseDebug($"GeneratedRooms: {data.GeneratedRooms.Count}");
                    Mod.Logger.VerboseDebug($"DoorPos.Count: {data.DoorPos.Count}");
                }

                GenRoomEnds(data, chunkX, chunkZ, dx == 0 && dz == 0);

                if (dx == 0 && dz == 0)
                {
                    Mod.Logger.VerboseDebug($"GeneratedRooms: {data.GeneratedRooms.Count}");
#if DEBUG_WIREFRAME
                    GeneratedRoomsC = data.GeneratedRooms;
#endif
                }
                if (!data.Initialized)
                {
                    data.Initialized = true;
                }
            }
        }

        private Th3DungeonRoom ChooseRoom(Th3DungeonData data, bool log = false)
        {
            float chance = _chunkRand.NextFloat();
            float chanceStart = 0;

            foreach (Th3DungeonRoomCategory cat in data.DungeonConfig.Categories)
            {
                if (chance >= chanceStart && chance < chanceStart + cat.Chance)
                {
                    int roomIndex = _chunkRand.NextInt(data.DungeonConfig.Rooms[cat.Name].Count);
                    if (log)
                    {
                        Mod.Logger.VerboseDebug($"ChooseRoom: {cat.Name} {chance * 100}%");
                    }
                    return data.DungeonConfig.Rooms[cat.Name].ElementAt(roomIndex);
                }
                chanceStart += cat.Chance;
            }
            Mod.Logger.Error($"Picking default room StartRoom: {chance}");
            return data.DungeonConfig.StartRoom;
        }

        private DungeonConfig ChooseDungeon()
        {
            float chance = _chunkRand.NextFloat();
            float chanceStart = 0;

            foreach (DungeonConfig dungeon in Th3DungeonConfig.Dungeons)
            {
                if (chance >= chanceStart && chance < chanceStart + dungeon.Chance)
                {
                    return dungeon;
                }
                chanceStart += dungeon.Chance;
            }
            return Th3DungeonConfig.Dungeons.FirstOrDefault();
        }

        private bool GetNext(Th3DungeonData data, Th3DoorPos current, bool randStart = true, bool removeDoorPos = true)
        {
            //add the door offset to the position => will result in new block position where the next door needs to be
            data.NextSpawn.OrigPosition = current.Position.AddCopy(current.Facing);

            // search for next facing
            // iterate over the 4 possible rotations of the room
            // make the rotaions random and not in order
            int index;
            int start = randStart ? _chunkRand.NextInt(4) : 0;
            for (int i = 0; i < 4; i++)
            {
                index = (start + i) % 4;
                for (int j = 0; j < data.NextSpawn.Room.Rotations[index].Doors.Count; j++)
                {
                    Th3DoorPos nex = data.NextSpawn.Room.Rotations[index].Doors[j];
                    if (nex.Facing.Equals(current.Facing.Opposite))
                    {
                        data.Schematic = data.NextSpawn.Room.Rotations[index];
                        // the new spawn position is the original new door position - the offset from the origin of the door position
                        data.NextSpawn.Position = data.NextSpawn.OrigPosition - nex.Position;

                        // get area that the new room will occupie
                        Cuboidi area = new Cuboidi(data.NextSpawn.Position, data.NextSpawn.Position.AddCopy(data.Schematic.SizeX, data.Schematic.SizeY, data.Schematic.SizeZ));

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

        private bool CanSpawn(Th3DungeonData data, Cuboidi area)
        {
            foreach (Cuboidi room in data.GeneratedRooms)
            {
                if (room.Intersects(area))
                {
                    return false;
                }
            }
            return true;
        }

        public void GenEntrance(Th3DungeonData data, int x, int z, int startYSize, int rotation, int chunkX, int chunkZ)
        {
            // get original start pos with out center adjustment for start room
            data.Schematic = data.DungeonConfig.Stairs.Rotations[rotation];
            data.NextSpawn.Position.Set(x, data.NextSpawn.Position.Y + startYSize, z);

            //if height is 0 it means that this chunk is not yet generated so nothing todo here
            int height = _chunkGenBlockAccessor.GetTerrainMapheightAt(data.NextSpawn.Position);
            // copy startpos for collision detection
            BlockPos startPos = null;
            if (!data.Initialized)
            {
                startPos = data.NextSpawn.Position.Copy();
            }
            // adjust after getting height
            data.Schematic.AdjustStartPos(data.NextSpawn.Position, EnumOrigin.BottomCenter);

            if (height == 0)
            {
                // Mod.Logger.VerboseDebug($"height : 0 ");
                return;
            }
            if (x / _chunkSize == chunkX && z / _chunkSize == chunkZ)
            {
                height += 1;
                // Mod.Logger.VerboseDebug($"height : {height} | {chunkX} {chunkZ}");
            }
            else
            {
                height -= data.DungeonConfig.StartRoomTop.Rotations[rotation].GetHeightAtPos(data.DungeonConfig.StartRoomTop.Rotations[rotation].SizeX / 2, data.DungeonConfig.StartRoomTop.Rotations[rotation].SizeZ / 2) - 1;
                // Mod.Logger.VerboseDebug($"height offset : {height} | {chunkX} {chunkZ}");
                return;
            }

            int y = data.DungeonConfig.Stairs.Rotations[0].SizeY;
            int rot = rotation;
            while (data.NextSpawn.Position.Y < height)
            {
                data.DungeonConfig.Stairs.Rotations[rot].Place(_chunkGenBlockAccessor, _api.World, data.NextSpawn.Position, chunkX, chunkZ);

                data.NextSpawn.Position.Y += y;
                rot = (rot + 1) % 4;
            }

            if (!data.Initialized)
            {
                startPos.X -= data.Schematic.SizeX / 2;
                startPos.Z -= data.Schematic.SizeZ / 2;
                Cuboidi area = new Cuboidi(startPos, data.NextSpawn.Position.AddCopy(data.DungeonConfig.Stairs.Rotations[rot].SizeX, data.DungeonConfig.Stairs.Rotations[rot].SizeY, data.DungeonConfig.Stairs.Rotations[rot].SizeZ));
                data.GeneratedRooms.Add(area);
            }

            // set data for start room top
            data.Schematic = data.DungeonConfig.StartRoomTop.Rotations[rotation];
            data.NextSpawn.Position.Set(x, data.NextSpawn.Position.Y + data.DungeonConfig.StartTopOffsetY, z);
            // add start room to overlap check
            if (!data.Initialized)
            {
                BlockPos topRoomePos = data.NextSpawn.Position.Copy();
                topRoomePos.X -= data.Schematic.SizeX / 2;
                topRoomePos.Z -= data.Schematic.SizeZ / 2;
                Cuboidi area = new Cuboidi(topRoomePos, topRoomePos.AddCopy(data.Schematic.SizeX, data.Schematic.SizeY, data.Schematic.SizeZ));
                data.GeneratedRooms.Add(area);
            }
            // adjust pos after adding collision check data
            data.Schematic.AdjustStartPos(data.NextSpawn.Position, EnumOrigin.BottomCenter);
            // place top room
            Place(data, chunkX, chunkZ);

        }

        private void GenRoomEnds(Th3DungeonData data, int chunkX, int chunkZ, bool log = false)
        {
            //choos next room to spawn
            data.NextSpawn.Room = data.DungeonConfig.EndRoom;
            foreach (Th3DoorPos door in data.DoorPos)
            {
                if (GetNext(data, door, false, false))
                {
                    Place(data, chunkX, chunkZ);
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

        public void Place(Th3DungeonData data, int chunkX, int chunkZ)
        {
            // Th3BlockSchematic schematic;
            // get the correct next room rotation based on the facing
            // schematic = GetRoomRotation(data);
            data.Schematic.Place(_chunkGenBlockAccessor, _api.World, data.NextSpawn.Position, chunkX, chunkZ);

            foreach (Th3DoorPos doorpos in data.Schematic.Doors)
            {
                // only add the doors positions that was not the spawn position of the current room
                if (!(data.NextSpawn.Position + doorpos.Position).Equals(data.NextSpawn.OrigPosition))
                {
                    data.DoorPos.Add(new Th3DoorPos(data.NextSpawn.Position + doorpos.Position, doorpos.Facing));
                }
            }
        }
    }
}