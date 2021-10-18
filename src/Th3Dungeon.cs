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
      _api.RegisterCommand("logpos", "spawn all structures", string.Empty, (IServerPlayer player, int groupId, CmdArgs args) =>
      {
        Mod.Logger.VerboseDebug(player.Entity.Pos.AsBlockPos.ToString());
        connector.Place(_chunkGenBlockAccessor, _api.World, player.Entity.Pos.AsBlockPos, true);
      });
    }

    private void OnWorldGenBlockAccessor(IChunkProviderThread chunkProvider)
    {
      _chunkGenBlockAccessor = chunkProvider.GetBlockAccessor(true);
    }

    private void InitWorldGen()
    {
      _chunkRand = new LCGRandom(_api.WorldManager.Seed);
      // assets = _api.Assets.GetMany<BlockSchematicStructure>(_api.Logger, "worldgen/schematics");
      connector = _api.Assets.Get<BlockSchematicStructure>(new AssetLocation("worldgen/schematics/overground/civicruins/prtcv1.json"));
      // connector = _api.Assets.Get<BlockSchematicStructure>(new AssetLocation("th3dungeon:worldgen/dungeon/connector.json"));
      connector.Init(_chunkGenBlockAccessor);
      Mod.Logger.VerboseDebug($"connector: {connector.Indices.Count}");
      // connector = _api.Assets.GetMany<BlockSchematicStructure>(_api.Logger, "worldgen/dungeon", "th3dungeon");


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
      IMapChunk mapChunk = chunks[0].MapChunk;

      _chunkRand.InitPositionSeed(RegionX, RegionZ);

      // get random spawn position in the current region using LCG
      int x = RegionX * _regionSize + _chunkSize + _chunkRand.NextInt(_regionSize - _chunkSize * 2);
      int z = RegionZ * _regionSize + _chunkSize + _chunkRand.NextInt(_regionSize - _chunkSize * 2);

      // calculate the chunk x and z from the random pos
      int chunkx = x / _chunkSize;
      int chunkz = z / _chunkSize;

      int height = _chunkGenBlockAccessor.GetTerrainMapheightAt(new BlockPos(x, 0, z));

      // if current chunk has the spawn position make it spawn
      if (chunkx == chunkX && chunkz == chunkZ)
      {
        BlockPos start = new BlockPos(x, height, z);
        height += connector.SizeY;
        connector.Place(_chunkGenBlockAccessor, _api.World, start, true);
        x %= _chunkSize;
        z %= _chunkSize;
        Mod.Logger.VerboseDebug($"pos::{start} /tp {chunkX * _chunkSize + x - _api.WorldManager.MapSizeX / 2} 201 {chunkZ * _chunkSize + z - _api.WorldManager.MapSizeZ / 2}");
        for (int y = height; y < 200; y++)
        {
          int chunkY = y % _chunkSize;
          int index3d = (((chunkY * _chunkSize) + z) * _chunkSize) + x;
          int chunkColIndex = y / _chunkSize;

          chunks[chunkColIndex].Blocks[index3d] = _creativeBlockId;
        }

        // create big flat on top for visibility
        int ytop = 200;
        int xn, zn;
        for (int dx = -2; dx <= 2; dx++)
        {
          xn = (x + dx) % _chunkSize;
          for (int dz = -2; dz <= 2; dz++)
          {
            zn = (z + dz) % _chunkSize;
            int chunkY = ytop % _chunkSize;
            int index3d = (((chunkY * _chunkSize) + zn) * _chunkSize) + xn;
            int chunkColIndex = ytop / _chunkSize;

            chunks[chunkColIndex].Blocks[index3d] = _creativeBlockId;
            mapChunk.RainHeightMap[zn * _chunkSize + xn] = (ushort)ytop;
          }
        }

      }
    }

    private void GenDungeon(int chunkX, int chunkZ)
    {

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