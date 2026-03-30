namespace Grim.Engine;

public interface IGameModule
{
    void Initialize();
    void Update(TimeSpan deltaTime);
    void Draw();
}