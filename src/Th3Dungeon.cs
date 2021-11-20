using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Th3Dungeon
{
  public class Th3Dungeon : ModSystem
  {
    private ICoreServerAPI _api;

    private IWorldGenBlockAccessor _chunkGenBlockAccessor;

    public LCGRandom _chunkRand;

    private int _chunkSize;

    private Th3DungeonConfig Th3DungeonConfig;

    private Dictionary<string, List<Th3DungeonRoom>> Rooms;

    private Th3DungeonRoom StartRoom;

    private int _chunkRange = 5;

    public override bool ShouldLoad(EnumAppSide side)
    {
      return side == EnumAppSide.Server;
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

      // _api.RegisterCommand("spawnstruct", "spawn all structures", string.Empty, OnSpawnStructures);
      _api.RegisterCommand("logpos", "spawn all structures", string.Empty, (IServerPlayer player, int groupId, CmdArgs args) =>
      {
        Mod.Logger.VerboseDebug(player.Entity.Pos.AsBlockPos.ToString());
      });
      Mod.Logger.VerboseDebug("_api.World.SeaLevel: " + _api.World.SeaLevel);
    }

    private void OnWorldGenBlockAccessor(IChunkProviderThread chunkProvider)
    {
      _chunkGenBlockAccessor = chunkProvider.GetBlockAccessor(true);
    }

    private void InitWorldGen()
    {
      _chunkRand = new LCGRandom(_api.WorldManager.Seed);

      Rooms = new Dictionary<string, List<Th3DungeonRoom>>();

      Th3DungeonConfig = _api.Assets.Get(new AssetLocation("th3dungeon", "worldgen/dungeon/Th3DungeonConfig.json")).ToObject<Th3DungeonConfig>();
      _chunkRange = Th3DungeonConfig.ChunkRange;

      float sum = 0;
      Th3DungeonConfig.Categories.ForEach((cat) => sum += cat.Chance);
      if (sum != 1)
      {
        Mod.Logger.Fatal($"Th3DungeonConfig categories do not add up to 1. [{sum}]");
      }

      var startRoom = _api.Assets.Get<Th3BlockSchematic>(new AssetLocation(Th3DungeonConfig.StartRoom));
      StartRoom = new Th3DungeonRoom(_api, startRoom, _chunkGenBlockAccessor, Th3DungeonConfig.StartRoom);

      foreach (var cat in Th3DungeonConfig.Categories)
      {
        var assets = _api.Assets.GetMany<Th3BlockSchematic>(_api.Logger, "worldgen/dungeon/" + cat.Name, "th3dungeon");
        var catRooms = new List<Th3DungeonRoom>();
        foreach (var asset in assets)
        {
          catRooms.Add(new Th3DungeonRoom(_api, asset.Value, _chunkGenBlockAccessor, asset.Key.Path));
        }
        Rooms.Add(cat.Name, catRooms);
      }
      // RuntimeEnv.DebugOutOfRangeBlockAccess = true;
    }

    protected void GenChunkColumn(IServerChunk[] chunks, int chunkX, int chunkZ, ITreeAttribute chunkGenParams = null)
    {
      Th3DungeonData data = new Th3DungeonData();

      for (int dx = -_chunkRange; dx <= _chunkRange; dx++)
      {
        for (int dz = -_chunkRange; dz <= _chunkRange; dz++)
        {
          _chunkRand.InitPositionSeed(chunkX + dx, chunkZ + dz);
          GenDungeon(data, chunks, chunkX, chunkZ, dx, dz);
        }
      }
    }

    protected void GenDungeon(Th3DungeonData data, IServerChunk[] chunks, int chunkX, int chunkZ, int dx, int dz)
    {

      int chunkXd = chunkX + dx;
      int chunkZd = chunkZ + dz;
      // if (_chunkRand.NextInt(1000) > 9998)
      // spawn dungeon in first chunk
      if (chunkXd == 16000 && chunkZd == 16000)
      {
        if (dx == 0 && dz == 0)
        {
          Mod.Logger.Debug($"nextSpawn: {chunkXd} : {chunkZd}");
        }
        // int x = chunkXd * _chunkSize + _chunkRand.NextInt(_chunkSize);
        // int z = chunkZd * _chunkSize + _chunkRand.NextInt(_chunkSize);
        int x = chunkXd * _chunkSize + 15;
        int z = chunkZd * _chunkSize + 15;

        // can be done since _chunkRand.NextInt(_chunkSize) exludes the max - so it wont overflow
        data.nextSpawn.Position = new BlockPos(x, 0, z);

        // TODO check if there is better way to optain a heigh in not generated chunks
        data.nextSpawn.Position.Add(0, _api.World.SeaLevel + 20, 0);

        //choose initial room
        data.nextSpawn.Room = StartRoom;
        //choose initial Facing
        data.nextSpawn.Facing = BlockFacing.NORTH;
        //spawn initial room
        Place(data, chunkX, chunkZ);
        if (chunkXd == 16000 && chunkZd == 16000 && dx == 0 && dz == 0)
        {
          Mod.Logger.Debug($"nextSpawn: {data.nextSpawn.Position} : {data.nextSpawn.Facing.Code}");
        }

        for (int i = 0; i < Th3DungeonConfig.RoomsToGenerate; i++)
        {
          if (data.DoorPos.Count > 0)
          {
            //choos next room to spawn
            data.nextSpawn.Room = ChooseRoom(chunkXd == 16000 && chunkZd == 16000 && dx == 0 && dz == 0);
            // get spawn pos offset from next room and previouse room and previous facing
            GetNextSpawnPos(data);

            Place(data, chunkX, chunkZ);
          }
        }
      }
    }

    private Th3DungeonRoom ChooseRoom(bool log)
    {
      float chance = _chunkRand.NextFloat();
      float chanceStart = 0;

      foreach (Th3DungeonRoomCategory cat in Th3DungeonConfig.Categories)
      {
        if (chance >= chanceStart && chance < chanceStart + cat.Chance)
        {
          int roomIndex = _chunkRand.NextInt(Rooms[cat.Name].Count);
          if (log)
          {
            Mod.Logger.VerboseDebug($"ChooseRoom: {cat.Name} {chance * 100}%");
          }
          return Rooms[cat.Name].ElementAt(roomIndex);
        }
        chanceStart += cat.Chance;
      }
      Mod.Logger.Error($"Picking default room StartRoom: {chance}");
      return StartRoom;
    }

    private void GetNextSpawnPos(Th3DungeonData data)
    {
      //choose next door pos to gen next room
      int ni = _chunkRand.NextInt(data.DoorPos.Count);
      Th3DoorPos current = data.DoorPos.ElementAt(ni);
      data.DoorPos.Remove(current);

      //add the door offset to the position => will result in new block position where the next door needs to be
      data.nextSpawn.OrigPosition = current.Position.AddCopy(current.Facing);

      // search for next facing
      bool found = false;
      // iterate over the 4 possible rotations of the room
      // make the rotaions random and not in order
      int index;
      int start = _chunkRand.NextInt(4);
      for (int i = 0; i < 4 && !found; i++)
      {
        index = (start + i) % 4;
        for (int j = 0; j < data.nextSpawn.Room.Rotations[index].Doors.Count && !found; j++)
        {
          Th3DoorPos nex = data.nextSpawn.Room.Rotations[index].Doors[j];
          if (nex.Facing.Equals(current.Facing.Opposite))
          {
            switch (index)
            {
              case 0:// south
                {
                  data.nextSpawn.Facing = BlockFacing.SOUTH;
                  break;
                }
              case 1:// west
                {
                  data.nextSpawn.Facing = BlockFacing.WEST;
                  break;
                }
              case 2:// north
                {
                  data.nextSpawn.Facing = BlockFacing.NORTH;
                  break;
                }
              case 3:// east
                {
                  data.nextSpawn.Facing = BlockFacing.EAST;
                  break;
                }
            }
            // the new spawn position is the original new door position - the offset from the origin of the door position
            data.nextSpawn.Position = data.nextSpawn.OrigPosition - nex.Position;
            found = true;
            break;
          }
        }
      }
    }

    public void Place(Th3DungeonData data, int chunkX, int chunkZ)
    {
      Th3BlockSchematic schematic;
      // get the correct next room rotation based on the 
      switch (data.nextSpawn.Facing.Code)
      {
        case "north":
          {
            schematic = data.nextSpawn.Room.Rotations[2];
            break;
          }
        case "east":
          {
            schematic = data.nextSpawn.Room.Rotations[3];
            break;
          }
        case "south":
          {
            schematic = data.nextSpawn.Room.Rotations[0];
            break;
          }
        case "west":
          {
            schematic = data.nextSpawn.Room.Rotations[1];
            break;
          }
        default:
          {
            schematic = data.nextSpawn.Room.Rotations[0];
            Mod.Logger.VerboseDebug("wrong facing :: defaulting to north");
            break;
          }
      }
      schematic.Place(_chunkGenBlockAccessor, _api.World, data.nextSpawn.Position, chunkX, chunkZ);
      foreach (Th3DoorPos doorpos in schematic.Doors)
      {
        // only add the doors positions that was not the spawn position of the current room
        if (!(data.nextSpawn.Position + doorpos.Position).Equals(data.nextSpawn.OrigPosition))
        {
          data.DoorPos.Add(new Th3DoorPos(data.nextSpawn.Position + doorpos.Position, doorpos.Facing));
        }
      }
    }

    private void OnSpawnStructures(IServerPlayer player, int groupId, CmdArgs args)
    {

      //   player.SendMessage(GlobalConstants.GeneralChatGroup, "loaded assets", EnumChatType.CommandSuccess);

      //   Mod.Logger.VerboseDebug("loaded assets: " + Schematics.Count);
      //   BlockPos pos = new BlockPos();
      //   int x = 0, z = 0;
      //   int sx = player.Entity.Pos.AsBlockPos.X - 200;
      //   int sz = player.Entity.Pos.AsBlockPos.Z - 200;
      //   pos.Y = player.Entity.Pos.AsBlockPos.Y;
      //   int offset = args.PopInt(20) ?? 20;
      //   int rowmax = args.PopInt(400) ?? 400;

      //   pos.X = sx;
      //   pos.Z = sz;
      //   int sizeX = offset, sizeZ = offset;
      //   foreach (KeyValuePair<AssetLocation, Th3BlockSchematic> structure in Schematics)
      //   {
      //     // Mod.Logger.VerboseDebug($"struc: {structure.Key.GetName()}");
      //     // structure.Value.Init(_worldBlockAccessor);
      //     structure.Value.Place(_chunkGenBlockAccessor, _api.World, pos, true);
      //     sizeX = Math.Max(sizeX, structure.Value.SizeX + 2);
      //     sizeZ = Math.Max(sizeZ, structure.Value.SizeZ + 2);
      //     if (x >= rowmax)
      //     {
      //       x = 0;
      //       z += Math.Max(offset, sizeZ);
      //       sizeX = offset;
      //       sizeZ = offset;
      //     }
      //     else
      //     {
      //       x += Math.Max(offset, sizeX);
      //     }
      //     pos.X = sx + x;
      //     pos.Z = sz + z;
      //   }
    }
  }
}