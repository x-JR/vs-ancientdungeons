using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.ServerMods;

namespace Th3Dungeon
{
  public class Th3Dungeon : ModSystem
  {

    private ICoreServerAPI _api;

    IWorldGenBlockAccessor _chunkGenBlockAccessor;

    private int _creativeBlockId;

    public LCGRandom _chunkRand;

    private int _chunkSize;
    private int _regionSize;
    private bool generated = false;

    private Dictionary<AssetLocation, BlockSchematicStructure> assets;

    private BlockSchematicStructure connector;
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
      _regionSize = api.WorldManager.RegionSize;
      _api.Event.GetWorldgenBlockAccessor(OnWorldGenBlockAccessor);
      _api.Event.InitWorldGenerator(InitWorldGen, "standard");

      _api.Event.ChunkColumnGeneration(GenChunkColumn, EnumWorldGenPass.TerrainFeatures, "standard");

      _creativeBlockId = _api.WorldManager.GetBlockId(new AssetLocation("game:creativeblock-0"));

      // _api.RegisterCommand("spawnstruct", "spawn all structures", string.Empty, OnSpawnStructures);
    }

    private void OnWorldGenBlockAccessor(IChunkProviderThread chunkProvider)
    {
      _chunkGenBlockAccessor = chunkProvider.GetBlockAccessor(true);
    }

    private void InitWorldGen()
    {
      _chunkRand = new LCGRandom(_api.WorldManager.Seed);
      // assets = _api.Assets.GetMany<BlockSchematicStructure>(_api.Logger, "worldgen/schematics");
      // connector = _api.Assets.Get<BlockSchematicStructure>(new AssetLocation("th3dungeon:worldgen/dungeon/connector"));
      // foreach (BlockSchematicStructure asset in assets.Values)
      // {
      //   asset.Init(_chunkGenBlockAccessor);
      // }
      // RuntimeEnv.DebugOutOfRangeBlockAccess = true;
    }


    protected void GenChunkColumn(IServerChunk[] chunks, int chunkX, int chunkZ, ITreeAttribute chunkGenParams = null)
    {
      // Get the region x and z from the current chunk to generate
      int RegionX = chunkX * _chunkSize / _regionSize;
      int RegionZ = chunkZ * _chunkSize / _regionSize;

      _chunkRand.InitPositionSeed(RegionX, RegionZ);

      // get random spawn position in the current region using LCG
      int x = RegionX * _regionSize + _chunkSize + _chunkRand.NextInt(_regionSize - _chunkSize * 2);
      int z = RegionZ * _regionSize + _chunkSize + _chunkRand.NextInt(_regionSize - _chunkSize * 2);

      // calculate the chunk x and z from the random pos
      int chunkx = x / _chunkSize;
      int chunkz = z / _chunkSize;
      // Mod.Logger.VerboseDebug($"Spawn Chunkx: {chunkx} , Chunkz: {chunkz} , ChunkX: {chunkX} , ChunkZ: {chunkZ} , RegionX: {RegionX} , RegionZ: {RegionZ}");

      // if current chunk has the spawn position make it spawn
      if (chunkx == chunkX && chunkz == chunkZ)
      {
        Mod.Logger.VerboseDebug("found chunk to spawn stuff in");
        for (int dx = -1; dx < 1; dx++)
        {
          x = (x + dx) % _chunkSize;
          for (int dz = -1; dz < 1; dz++)
          {
            z = (z + dz) % _chunkSize;
            for (int y = 100; y < 200; y++)
            {
              int chunkY = y % _chunkSize;
              int index3d = (((chunkY * _chunkSize) + z) * _chunkSize) + x;
              int chunkColIndex = y / _chunkSize;

              chunks[chunkColIndex].Blocks[index3d] = _creativeBlockId;
            }
          }
        }
      }

      // for (int dx = -_chunkRange; dx <= _chunkRange; dx++)
      // {
      //   for (int dz = -_chunkRange; dz <= _chunkRange; dz++)
      //   {
      //     _chunkRand.InitPositionSeed(chunkX + dx, chunkZ + dz);
      //     GeneratePartial(chunks, chunkX, chunkZ, dx, dz);
      //   }
      // }
    }

    public virtual void GeneratePartial(IServerChunk[] chunks, int chunkX, int chunkZ, int basePosX, int basePosZ)
    {
      // int quantityDungeons = _chunkRand.NextInt(100) < 50 ? 1 : 0;

      // while (quantityDungeons-- > 0)
      // {

      //   int x = basePosX * _chunkSize + _chunkRand.NextInt(_chunkSize);
      //   if (x >= 0 && x < _chunkSize)
      //   {
      //     for (int z = 0; z < _chunkSize; z++)
      //     {
      //       for (int y = 100; y < 150; y++)
      //       {

      //         int chunkY = y % _chunkSize;
      //         int index3d = (((chunkY * _chunkSize) + z) * _chunkSize) + x;
      //         int chunkColIndex = y / _chunkSize;

      //         chunks[chunkColIndex].Blocks[index3d] = _creativeBlockId;
      //       }
      //     }
      //   }

      // }
      GenDungeon(chunkX, chunkZ);

    }

    private void GenDungeon(int chunkX, int chunkZ)
    {

    }

    private void OnChunkColumnGeneration(IServerChunk[] chunks, int chunkX, int chunkZ, ITreeAttribute chunkGenParams = null)
    {
      // IMapRegion region = chunks[0].MapChunk.MapRegion;

      // for (int y = 100; y < 150; y++)
      // {
      //   int x = _chunkSize / 2;
      //   int z = x;

      //   int chunkY = y % _chunkSize;
      //   int index3d = (((chunkY * _chunkSize) + z) * _chunkSize) + x;
      //   int chunkColIndex = y / _chunkSize;

      //   chunks[chunkColIndex].Blocks[index3d] = _creativeBlockId;
      // }
      if (!generated)
      {
        // RuntimeEnv.DebugOutOfRangeBlockAccess = true;
        generated = true;
        GenDungeon(chunkX, chunkZ);
      }
    }

    private void GenDungeonTest(int chunkX, int chunkZ, ITreeAttribute chunkGenParams = null)
    {
      // _chunkRand.InitPositionSeed(chunkX, chunkZ);
      // int dx = Rand.NextInt(_chunkSize);
      // int dz = Rand.NextInt(_chunkSize);
      int mid = _api.WorldManager.MapSizeX / 2;
      BlockPos pos = new BlockPos(mid + _chunkSize / 2 - 500, 120, mid + _chunkSize / 2);
      Mod.Logger.VerboseDebug($"struc count: {assets.Values.Count}");
      foreach (KeyValuePair<AssetLocation, BlockSchematicStructure> structure in assets)
      {
        // Mod.Logger.VerboseDebug($"struc: {structure.Key.GetName()}");
        // structure.Value.Init(_chunkGenBlockAccessor);
        // if (structure.Value.PlaceRespectingBlockLayers(_chunkGenBlockAccessor, _api.World, pos, 1, 1, 1, 1, new int[0], false) == 1)

        int ret = structure.Value.Place(_chunkGenBlockAccessor, _api.World, pos, true);
        Mod.Logger.VerboseDebug($"placed : {structure.Key.GetName()} : {ret}");
        // if (ret == 1)
        // {
        //   Mod.Logger.VerboseDebug($"placed : {structure.Key.GetName()}");
        // }
        // else
        // {
        //   Mod.Logger.VerboseDebug($"failed : {structure.Key.GetName()}");
        // }
        pos.X += structure.Value.SizeX;
        // ret = structure.Place(_chunkGenBlockAccessor, _api.World, pos, true);
        // Mod.Logger.VerboseDebug($"placed : {ret}");
      }
    }

    private void OnSpawnStructures(IServerPlayer player, int groupId, CmdArgs args)
    {

      player.SendMessage(GlobalConstants.GeneralChatGroup, "loaded assets", EnumChatType.CommandSuccess);

      Mod.Logger.VerboseDebug("loaded assets: " + assets.Count);
      BlockPos pos = new BlockPos();
      int x = 0, z = 0;
      int sx = player.Entity.Pos.AsBlockPos.X - 200;
      int sz = player.Entity.Pos.AsBlockPos.Z - 200;
      pos.Y = player.Entity.Pos.AsBlockPos.Y;
      int offset = args.PopInt(20) ?? 20;
      int rowmax = args.PopInt(400) ?? 400;

      pos.X = sx;
      pos.Z = sz;
      int sizeX = offset, sizeZ = offset;
      foreach (KeyValuePair<AssetLocation, BlockSchematicStructure> structure in assets)
      {
        // Mod.Logger.VerboseDebug($"struc: {structure.Key.GetName()}");
        // structure.Value.Init(_worldBlockAccessor);
        structure.Value.Place(_chunkGenBlockAccessor, _api.World, pos, true);
        sizeX = Math.Max(sizeX, structure.Value.SizeX + 2);
        sizeZ = Math.Max(sizeZ, structure.Value.SizeZ + 2);
        if (x >= rowmax)
        {
          x = 0;
          z += Math.Max(offset, sizeZ);
          sizeX = offset;
          sizeZ = offset;
        }
        else
        {
          x += Math.Max(offset, sizeX);
        }
        pos.X = sx + x;
        pos.Z = sz + z;
      }
    }
  }
}