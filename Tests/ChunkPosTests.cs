using th3dungeon.Data;

namespace Tests;


public class ChunkPosTests
{
    [Fact]
    public void Distance_ShouldCalculateCorrectDistance()
    {
        // Arrange
        ChunkPos pos1 = new ChunkPos(0, 0);
        ChunkPos pos2 = new ChunkPos(0, 10);

        // Act
        double distance = pos1.Distance(pos2);

        // Assert
        Assert.Equal(10, distance, 3);
    }
    
    [Fact]
    public void Equals_ShouldCompareByXAndZProperties()
    {
        // Arrange
        ChunkPos pos1 = new ChunkPos(0, 0);
        ChunkPos pos2 = new ChunkPos(3, 4);
        ChunkPos pos3 = new ChunkPos(3, 4);
        ChunkPos pos4 = new ChunkPos(5, 6);

        // Act & Assert
        Assert.True(pos2.Equals(pos3)); // Same X and Z values
        Assert.False(pos1.Equals(pos2)); // Different X and Z values
        Assert.False(pos2.Equals(pos4)); // Different X and Z values
    }
    
    [Fact]
    public void Contains_ShouldCompareByXAndZProperties()
    {
        // Arrange
        ChunkPos pos1 = new ChunkPos(0, 0);
        ChunkPos pos2 = new ChunkPos(3, 4);
        ChunkPos pos3 = new ChunkPos(3, 4);
        ChunkPos pos4 = new ChunkPos(5, 6);
        var positions = new List<ChunkPos>() { pos1, pos2 };

        // Act & Assert
        Assert.True(positions.Contains(pos2)); // Same X and Z values
        Assert.True(positions.Contains(pos3)); // Same X and Z values, but not same object reference
        Assert.False(positions.Contains(pos4)); // Different X and Z values
    }
}