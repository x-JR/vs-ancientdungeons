using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;


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