namespace Grim.Engine;

public sealed class GameObject
{
    public GameObject(string name)
    {
        Name = name;
    }

    public string Name { get; }
    public Transform Transform { get; } = new();
}