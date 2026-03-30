namespace Grim.Core;

public static class Program
{
    public static void Main()
    {
        using var game = new GrimGame();
        game.Run();
    }
}