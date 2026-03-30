using Grim.Engine;

namespace Grim.Client;

public sealed class ClientRuntimeContext
{
    public Scene MainScene { get; } = new();
    public string LastStatus { get; set; } = "Client bootstrapped";
    public string NetworkStatus { get; set; } = "Disconnected";
}