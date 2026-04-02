namespace Grim.Shared;

public static class EditorPaletteHitTest
{
    public static int? TryGetPaletteIndex(
        int mouseX,
        int mouseY,
        int panelX,
        int panelY,
        int panelWidth,
        int itemCount,
        int listOffsetY = 56,
        int listPaddingX = 6,
        int rowHeight = 24)
    {
        if (itemCount <= 0 || panelWidth <= 0 || rowHeight <= 0)
        {
            return null;
        }

        var listX = panelX + listPaddingX;
        var listY = panelY + listOffsetY;
        var listWidth = panelWidth - (listPaddingX * 2);
        var listHeight = itemCount * rowHeight;

        if (listWidth <= 0 || listHeight <= 0)
        {
            return null;
        }

        if (mouseX < listX || mouseX >= listX + listWidth || mouseY < listY || mouseY >= listY + listHeight)
        {
            return null;
        }

        var relativeY = mouseY - listY;
        var index = relativeY / rowHeight;
        if (index < 0 || index >= itemCount)
        {
            return null;
        }

        return index;
    }
}