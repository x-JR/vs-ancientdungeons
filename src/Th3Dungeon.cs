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

    private List<Th3DungeonRoom> Rooms;

    private List<Th3DoorPos> DoorPos;

    private Th3BlockSchematic connector;

    private readonly int _chunkRange = 1;

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
      // connector = _api.Assets.Get<Th3BlockSchematic>(new AssetLocation("th3dungeon", "worldgen/dungeon/connector.json"));
      // connector.Init(_chunkGenBlockAccessor);
      // connector.LoadMeta(_chunkGenBlockAccessor, _api.World, "worldgen/dungeon/connector.json");

      Rooms = new List<Th3DungeonRoom>();
      DoorPos = new List<Th3DoorPos>();

      var assets = _api.Assets.GetMany<Th3BlockSchematic>(_api.Logger, "worldgen/dungeon", "th3dungeon");
      foreach (KeyValuePair<AssetLocation, Th3BlockSchematic> asset in assets)
      {
        Rooms.Add(new Th3DungeonRoom(asset.Value, _api, _chunkGenBlockAccessor, asset.Key.Path));
        // asset.Value.Init(_chunkGenBlockAccessor, Mod);
        // asset.Value.LoadMeta(_chunkGenBlockAccessor, _api.World, asset.Key.ToString());
      }
      // RuntimeEnv.DebugOutOfRangeBlockAccess = true;
    }

    protected void GenChunkColumn(IServerChunk[] chunks, int chunkX, int chunkZ, ITreeAttribute chunkGenParams = null)
    {
      for (int dx = -_chunkRange; dx <= _chunkRange; dx++)
      {
        for (int dz = -_chunkRange; dz <= _chunkRange; dz++)
        {
          _chunkRand.InitPositionSeed(chunkX + dx, chunkZ + dz);
          GenDungeon(chunkX, chunkZ, dx, dz);
        }
      }
    }

    protected void GenDungeon(int chunkX, int chunkZ, int dx, int dz)
    {
      int chunkXd = chunkX + dx;
      int chunkZd = chunkZ + dz;
      // if (_chunkRand.NextInt(1000) > 9998)
      // spawn dungeon in first chunk
      if (chunkXd == 16000 && chunkZd == 16000)
      {
        // int x = chunkXd * _chunkSize + _chunkRand.NextInt(_chunkSize);
        // int z = chunkZd * _chunkSize + _chunkRand.NextInt(_chunkSize);
        int x = chunkXd * _chunkSize + 16;
        int z = chunkZd * _chunkSize + 16;

        BlockPos start = new BlockPos(x, 0, z);
        int height = _chunkGenBlockAccessor.GetTerrainMapheightAt(start);
        // + 1 so that the height map on that pos does not update which results in all chunks after first being one structure size higher
        // can be done since _chunkRand.NextInt(_chunkSize) exludes the max - so it wont overflow
        start.Add(1, height, 1);
        // start.Y = height;

        // connector.Place(_chunkGenBlockAccessor, _api.World, start, chunkX, chunkZ);
        Place(Rooms.First(), start.Copy(), chunkX, chunkZ, BlockFacing.NORTH);
        start.Add(10, 0, 0);
        Place(Rooms.First(), start.Copy(), chunkX, chunkZ, BlockFacing.EAST);
        start.Add(10, 0, 0);
        Place(Rooms.First(), start.Copy(), chunkX, chunkZ, BlockFacing.SOUTH);
        start.Add(10, 0, 0);
        Place(Rooms.First(), start.Copy(), chunkX, chunkZ, BlockFacing.WEST);

        // for (int i = 1; i <= 1; i++)
        // {
        //   if (DoorPos.Count > 0)
        //   {
        //     // int next = _chunkRand.NextInt(DoorPos.Count);
        //     var pos = DoorPos[0];

        //     Place(Rooms.First(), pos.Position, chunkX, chunkZ, pos.Facing);
        //     DoorPos.Remove(pos);
        //   }
        // }
      }
    }

    public void Place(Th3DungeonRoom room, BlockPos startPos, int chunkX, int chunkZ, BlockFacing facing)
    {
      Th3BlockSchematic schematic;
      switch (facing.Code)
      {
        case "north":
          {
            schematic = room.rotations[2];
            break;
          }
        case "east":
          {
            schematic = room.rotations[3];
            break;
          }
        case "south":
          {
            schematic = room.rotations[0];
            break;
          }
        case "west":
          {
            schematic = room.rotations[1];
            break;
          }
        default:
          {
            schematic = room.rotations[0];
            Mod.Logger.VerboseDebug("wrong facing :: defaulting to north");
            break;
          }
      }
      schematic.Place(_chunkGenBlockAccessor, _api.World, startPos, chunkX, chunkZ);
      foreach (Th3DoorPos doorpos in schematic.Doors)
      {
        DoorPos.Add(new Th3DoorPos(new BlockPos(startPos.X + doorpos.Position.X, startPos.Y + doorpos.Position.Y, startPos.Z + doorpos.Position.Z), doorpos.Facing));
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