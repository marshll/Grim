namespace Grim.Engine;

public sealed class Scene
{
    private readonly List<GameObject> _objects = [];

    public IReadOnlyList<GameObject> Objects => _objects;

    public void Add(GameObject gameObject)
    {
        _objects.Add(gameObject);
    }
}