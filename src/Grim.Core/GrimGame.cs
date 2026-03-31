using Grim.Client;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Grim.Core;

public sealed class GrimGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly ClientBootstrap _client;

    public GrimGame(ClientLaunchOptions launchOptions)
    {
        _client = new ClientBootstrap(
            launchOptions.Host,
            launchOptions.Port,
            launchOptions.Account,
            launchOptions.ClientTag);
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.Title = $"Grim - {launchOptions.ClientTag}";
        _graphics.PreferredBackBufferWidth = 1600;
        _graphics.PreferredBackBufferHeight = 900;
    }

    protected override void Initialize()
    {
        _client.Initialize();
        base.Initialize();
    }

    protected override void Update(GameTime gameTime)
    {
        _client.Update(gameTime.ElapsedGameTime);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(12, 16, 28));
        _client.Draw();
        base.Draw(gameTime);
    }
}