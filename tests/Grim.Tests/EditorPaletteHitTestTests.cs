using Grim.Shared;
using Xunit;

namespace Grim.Tests;

public sealed class EditorPaletteHitTestTests
{
    [Fact]
    public void TryGetPaletteIndex_ReturnsNull_WhenItemCountIsZero()
    {
        var index = EditorPaletteHitTest.TryGetPaletteIndex(900, 190, 790, 122, 530, 0);
        Assert.Null(index);
    }

    [Fact]
    public void TryGetPaletteIndex_ReturnsNull_WhenMouseIsOutsideListArea()
    {
        var outsideLeft = EditorPaletteHitTest.TryGetPaletteIndex(700, 190, 790, 122, 530, 5);
        var outsideTop = EditorPaletteHitTest.TryGetPaletteIndex(900, 160, 790, 122, 530, 5);

        Assert.Null(outsideLeft);
        Assert.Null(outsideTop);
    }

    [Fact]
    public void TryGetPaletteIndex_ReturnsFirstRowIndex()
    {
        var index = EditorPaletteHitTest.TryGetPaletteIndex(900, 180, 790, 122, 530, 5);
        Assert.Equal(0, index);
    }

    [Fact]
    public void TryGetPaletteIndex_ReturnsMiddleRowIndex()
    {
        var index = EditorPaletteHitTest.TryGetPaletteIndex(900, 231, 790, 122, 530, 5);
        Assert.Equal(2, index);
    }

    [Fact]
    public void TryGetPaletteIndex_ReturnsLastRowIndex()
    {
        var index = EditorPaletteHitTest.TryGetPaletteIndex(900, 284, 790, 122, 530, 5);
        Assert.Equal(4, index);
    }
}