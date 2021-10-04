using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods;

namespace Th3Dungeon
{
  public class Th3Dungeon : ModSystem
  {

    private ICoreServerAPI _api;

    IWorldGenBlockAccessor _chunkGenBlockAccessor;

    private IBlockAccessor _worldBlockAccessor;

    private int _chunkSize;

    private int _creativeBlockId;

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

      _worldBlockAccessor = api.World.BlockAccessor;
      _chunkSize = _worldBlockAccessor.ChunkSize;
      _api.Event.GetWorldgenBlockAccessor(OnWorldGenBlockAccessor);
      _api.Event.ChunkColumnGeneration(OnChunkColumnGeneration, EnumWorldGenPass.TerrainFeatures, "standard");



      _creativeBlockId = _api.WorldManager.GetBlockId(new AssetLocation("game:creativeblock-0"));

      _api.RegisterCommand("spawnstruct", "spawn all structures", string.Empty, OnSpawnStructures);
    }

    private void OnSpawnStructures(IServerPlayer player, int groupId, CmdArgs args)
    {
      player.SendMessage(GlobalConstants.GeneralChatGroup, "loaded assets", EnumChatType.CommandSuccess);
      Dictionary<AssetLocation, BlockSchematicStructure> assets = _api.Assets.GetMany<BlockSchematicStructure>(_api.Logger, "worldgen/schematics");
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
        structure.Value.Init(_worldBlockAccessor);
        structure.Value.Place(_worldBlockAccessor, _api.World, pos, true);
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

    private void OnWorldGenBlockAccessor(IChunkProviderThread chunkProvider)
    {
      _chunkGenBlockAccessor = chunkProvider.GetBlockAccessor(true);
    }

    private void OnChunkColumnGeneration(IServerChunk[] chunks, int chunkX, int chunkZ, ITreeAttribute chunkGenParams = null)
    {
      for (int y = 100; y < 150; y++)
      {
        int x = _chunkSize / 2;
        int z = x;

        int chunkY = y % _chunkSize;
        int index3d = (((chunkY * _chunkSize) + z) * _chunkSize) + x;
        int chunkColIndex = y / _chunkSize;

        chunks[chunkColIndex].Blocks[index3d] = _creativeBlockId;
      }
    }
  }
}