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

    private Th3BlockSchematic connector;

    private readonly int _chunkRange = 7;

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
      connector = _api.Assets.Get<Th3BlockSchematic>(new AssetLocation("th3dungeon", "worldgen/dungeon/connector.json"));
      connector.Init(_chunkGenBlockAccessor, Mod);
      connector.LoadMetaInformationAndValidate(_chunkGenBlockAccessor, _api.World, "worldgen/dungeon/connector.json");
      // connector = _api.Assets.GetMany<BlockSchematic>(_api.Logger, "worldgen/dungeon", "th3dungeon");


      // foreach (BlockSchematic asset in assets.Values)
      // {
      //   asset.Init(_chunkGenBlockAccessor);
      // }
      // RuntimeEnv.DebugOutOfRangeBlockAccess = true;
    }
    protected void GenChunkColumn(IServerChunk[] chunks, int chunkX, int chunkZ, ITreeAttribute chunkGenParams = null)
    {
      for (int dx = -_chunkRange; dx <= _chunkRange; dx++)
      {
        for (int dz = -_chunkRange; dz <= _chunkRange; dz++)
        {
          _chunkRand.InitPositionSeed(chunkX + dx, chunkZ + dz);
          GenDungeon(chunks, chunkX, chunkZ, dx, dz);
        }
      }
    }

    protected void GenDungeon(IServerChunk[] chunks, int chunkX, int chunkZ, int dx, int dz)
    {
      if (_chunkRand.NextInt(1000) < 2)
      {
        int chunkXd = chunkX + dx;
        int chunkZd = chunkZ + dz;

        int x = chunkXd * _chunkSize + _chunkRand.NextInt(_chunkSize);
        int z = chunkZd * _chunkSize + _chunkRand.NextInt(_chunkSize);

        BlockPos start = new BlockPos(x, 0, z);
        int height = _chunkGenBlockAccessor.GetTerrainMapheightAt(start);
        start.Y = height;

        // if current chunk has the spawn position make it spawn
        if (dx == 0 && dz == 0)
        {
          x %= _chunkSize;
          z %= _chunkSize;
          generated = true;
          Mod.Logger.VerboseDebug($"pos::{start} /tp {chunkXd * _chunkSize + x - _api.WorldManager.MapSizeX / 2} {height} {chunkZd * _chunkSize + z - _api.WorldManager.MapSizeZ / 2}");
        }

        connector.Place(_chunkGenBlockAccessor, _api.World, start, chunkX, chunkZ);

        for (int i = 0; i < 100; i++)
        {
          int dir = _chunkRand.NextInt(2);
          if (dir == 0)
          {
            start.X += connector.SizeX;
          }
          else
          {
            start.Z += connector.SizeZ;
          }
          connector.Place(_chunkGenBlockAccessor, _api.World, start, chunkX, chunkZ);
        }
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