using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
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

    private List<Th3DungeonRoom> RoomsStraight;

    private List<Th3DungeonRoom> RoomsTurn;

    private List<Th3DungeonRoom> RoomsUpDown;

    private readonly int _chunkRange = 5;

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
    }

    private void OnWorldGenBlockAccessor(IChunkProviderThread chunkProvider)
    {
      _chunkGenBlockAccessor = chunkProvider.GetBlockAccessor(true);
    }

    private void InitWorldGen()
    {
      _chunkRand = new LCGRandom(_api.WorldManager.Seed);

      RoomsStraight = new List<Th3DungeonRoom>();
      RoomsTurn = new List<Th3DungeonRoom>();
      RoomsUpDown = new List<Th3DungeonRoom>();

      var assets = _api.Assets.GetMany<Th3BlockSchematic>(_api.Logger, "worldgen/dungeon/straight", "th3dungeon");
      foreach (var asset in assets)
      {
        RoomsStraight.Add(new Th3DungeonRoom(asset.Value, _api, _chunkGenBlockAccessor, asset.Key.Path));
      }

      assets = _api.Assets.GetMany<Th3BlockSchematic>(_api.Logger, "worldgen/dungeon/turn", "th3dungeon");
      foreach (var asset in assets)
      {
        RoomsTurn.Add(new Th3DungeonRoom(asset.Value, _api, _chunkGenBlockAccessor, asset.Key.Path));
      }

      assets = _api.Assets.GetMany<Th3BlockSchematic>(_api.Logger, "worldgen/dungeon/updown", "th3dungeon");
      foreach (var asset in assets)
      {
        RoomsUpDown.Add(new Th3DungeonRoom(asset.Value, _api, _chunkGenBlockAccessor, asset.Key.Path));
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
          GenDungeon(data, chunkX, chunkZ, dx, dz);
        }
      }
    }

    protected void GenDungeon(Th3DungeonData data, int chunkX, int chunkZ, int dx, int dz)
    {
      int chunkXd = chunkX + dx;
      int chunkZd = chunkZ + dz;
      // if (_chunkRand.NextInt(1000) > 9998)
      // spawn dungeon in first chunk
      if (chunkXd == 16000 && chunkZd == 16000)
      {
        // int x = chunkXd * _chunkSize + _chunkRand.NextInt(_chunkSize);
        // int z = chunkZd * _chunkSize + _chunkRand.NextInt(_chunkSize);
        int x = chunkXd * _chunkSize + 15;
        int z = chunkZd * _chunkSize + 15;

        data.nextSpawn.Position = new BlockPos(x, 0, z);
        //TODO height
        // int height = _chunkGenBlockAccessor.GetTerrainMapheightAt(data.nextSpawn.Position);
        // + 1 so that the height map on that pos does not update which results in all chunks after first being one structure size higher
        // can be done since _chunkRand.NextInt(_chunkSize) exludes the max - so it wont overflow
        data.nextSpawn.Position.Add(1, 125, 1);
        // previousRoom.Y = height;

        //choose initial room
        data.nextSpawn.Room = RoomsStraight.First();
        //choose initial Facing
        data.nextSpawn.Facing = BlockFacing.NORTH;
        //spawn initial room
        Place(data, chunkX, chunkZ);
        if (chunkXd == 16000 && chunkZd == 16000 && dx == 0 && dz == 0)
        {
          Mod.Logger.Debug($"nextSpawn: {data.nextSpawn.Position} : {data.nextSpawn.Facing.Code}");
        }

        for (int i = 1; i <= 30; i++)
        {
          if (data.DoorPos.Count > 0)
          {
            //choos next room to spawn
            data.nextSpawn.Room = ChooseRoom();
            // get spawn pos offset from next room and previouse room and previous facing
            GetNextSpawnPos(data, chunkXd == 16000 && chunkZd == 16000 && dx == 0 && dz == 0);

            Place(data, chunkX, chunkZ);
          }
        }
      }
    }

    private Th3DungeonRoom ChooseRoom()
    {
      int chance = _chunkRand.NextInt(100);
      int nir;
      // straight
      if (chance > 0 && chance < 50)
      {
        nir = _chunkRand.NextInt(RoomsStraight.Count);
        return RoomsStraight.ElementAt(nir);
      } // turn
      else if (chance > 50 && chance < 70)
      {
        nir = _chunkRand.NextInt(RoomsTurn.Count);
        return RoomsTurn.ElementAt(nir);
      } // updown
      else
      {
        nir = _chunkRand.NextInt(RoomsUpDown.Count);
        return RoomsUpDown.ElementAt(nir);
      }
    }

    private void GetNextSpawnPos(Th3DungeonData data, bool log)
    {
      //choose next dorr post to gen next room
      int ni = _chunkRand.NextInt(data.DoorPos.Count);
      Th3DoorPos current = data.DoorPos.ElementAt(ni);
      data.DoorPos.Remove(current);

      //add the door offset to the position => will result in new block position where the next door needs to be
      data.nextSpawn.OrigPosition = current.Position.AddCopy(current.Facing);

      if (log)
      {
        Mod.Logger.Debug($"nsop  get: {data.nextSpawn.OrigPosition} : {current.Facing.Opposite}");
      }

      // search for next facing
      bool found = false;
      // iterate over the 4 possible rotations of the room
      for (int i = 0; i < 4 && !found; i++)
      {
        for (int j = 0; j < data.nextSpawn.Room.Rotations[i].Doors.Count && !found; j++)
        {
          Th3DoorPos nex = data.nextSpawn.Room.Rotations[i].Doors[j];
          if (nex.Facing.Equals(current.Facing.Opposite))
          {
            switch (i)
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
            if (log)
            {
              Mod.Logger.Debug($"nsp after: {data.nextSpawn.Position} : {data.nextSpawn.Facing.Code} : {nex.Position} : {nex.Facing}");
            }
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