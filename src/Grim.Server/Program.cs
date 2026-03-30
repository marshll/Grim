namespace Grim.Server;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var port = 7777;
        if (args.Length > 0 && int.TryParse(args[0], out var parsedPort))
        {
            port = parsedPort;
        }

        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cts.Cancel();
        };

        var host = new ServerHost(port);
        await host.RunAsync(cts.Token);
    }
}