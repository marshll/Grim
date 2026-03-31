namespace Grim.Core;

public static class Program
{
    public static void Main(string[] args)
    {
        var options = ClientLaunchOptions.Parse(args);
        using var game = new GrimGame(options);
        game.Run();
    }
}

public sealed record ClientLaunchOptions(string Host, int Port, string Account, string ClientTag)
{
    public static ClientLaunchOptions Parse(string[] args)
    {
        var host = "127.0.0.1";
        var port = 7777;
        var account = "dev_account";
        var clientTag = "client";

        for (var i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--host", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                host = args[++i];
                continue;
            }

            if (string.Equals(args[i], "--port", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                if (int.TryParse(args[++i], out var parsedPort) && parsedPort is > 0 and <= 65535)
                {
                    port = parsedPort;
                }

                continue;
            }

            if (string.Equals(args[i], "--account", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                account = args[++i];
                continue;
            }

            if (string.Equals(args[i], "--client", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                clientTag = args[++i];
            }
        }

        return new ClientLaunchOptions(host, port, account, clientTag);
    }
}