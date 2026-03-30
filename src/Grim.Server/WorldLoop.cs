namespace Grim.Server;

public sealed class WorldLoop
{
    private const int TickRateHz = 20;
    private long _tick;

    public long Tick => Interlocked.Read(ref _tick);

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var tickDelay = TimeSpan.FromMilliseconds(1000.0 / TickRateHz);

        while (!cancellationToken.IsCancellationRequested)
        {
            var currentTick = Interlocked.Increment(ref _tick);
            if (currentTick % 100 == 0)
            {
                Console.WriteLine($"World tick: {currentTick}");
            }

            await Task.Delay(tickDelay, cancellationToken);
        }
    }
}