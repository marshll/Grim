using Grim.Client;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Grim.Core;

public sealed class GrimGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly ClientBootstrap _client;
    private readonly string _clientTag;

    private SpriteBatch? _spriteBatch;
    private Texture2D? _pixel;
    private BasicEffect? _basicEffect;

    private double _windowTitleRefreshTimer;
    private float _cameraYaw = 0.8f;
    private float _cameraPitch = 0.85f;
    private float _cameraDistance = 24f;
    private Vector3 _cameraTarget = Vector3.Zero;
    private float _playerYawRadians;
    private bool _cameraInitialized;
    private MouseState _previousMouseState;

    private static readonly Vector3[] UnitCubeCorners =
    [
        new Vector3(-0.5f, -0.5f, -0.5f),
        new Vector3(0.5f, -0.5f, -0.5f),
        new Vector3(0.5f, 0.5f, -0.5f),
        new Vector3(-0.5f, 0.5f, -0.5f),
        new Vector3(-0.5f, -0.5f, 0.5f),
        new Vector3(0.5f, -0.5f, 0.5f),
        new Vector3(0.5f, 0.5f, 0.5f),
        new Vector3(-0.5f, 0.5f, 0.5f)
    ];

    private static readonly short[] UnitCubeIndices =
    [
        0, 1, 2, 0, 2, 3,
        1, 5, 6, 1, 6, 2,
        5, 4, 7, 5, 7, 6,
        4, 0, 3, 4, 3, 7,
        3, 2, 6, 3, 6, 7,
        4, 5, 1, 4, 1, 0
    ];

    private static readonly Dictionary<char, string[]> Glyphs = new()
    {
        [' '] = ["000", "000", "000", "000", "000"],
        ['-'] = ["000", "000", "111", "000", "000"],
        [':'] = ["000", "010", "000", "010", "000"],
        ['.'] = ["000", "000", "000", "000", "010"],
        ['0'] = ["111", "101", "101", "101", "111"],
        ['1'] = ["010", "110", "010", "010", "111"],
        ['2'] = ["111", "001", "111", "100", "111"],
        ['3'] = ["111", "001", "111", "001", "111"],
        ['4'] = ["101", "101", "111", "001", "001"],
        ['5'] = ["111", "100", "111", "001", "111"],
        ['6'] = ["111", "100", "111", "101", "111"],
        ['7'] = ["111", "001", "001", "010", "010"],
        ['8'] = ["111", "101", "111", "101", "111"],
        ['9'] = ["111", "101", "111", "001", "111"],
        ['A'] = ["111", "101", "111", "101", "101"],
        ['B'] = ["110", "101", "110", "101", "110"],
        ['C'] = ["111", "100", "100", "100", "111"],
        ['D'] = ["110", "101", "101", "101", "110"],
        ['E'] = ["111", "100", "111", "100", "111"],
        ['F'] = ["111", "100", "111", "100", "100"],
        ['G'] = ["111", "100", "101", "101", "111"],
        ['H'] = ["101", "101", "111", "101", "101"],
        ['I'] = ["111", "010", "010", "010", "111"],
        ['K'] = ["101", "101", "110", "101", "101"],
        ['L'] = ["100", "100", "100", "100", "111"],
        ['M'] = ["101", "111", "111", "101", "101"],
        ['N'] = ["101", "111", "111", "111", "101"],
        ['O'] = ["111", "101", "101", "101", "111"],
        ['P'] = ["111", "101", "111", "100", "100"],
        ['R'] = ["111", "101", "111", "101", "101"],
        ['S'] = ["111", "100", "111", "001", "111"],
        ['T'] = ["111", "010", "010", "010", "010"],
        ['U'] = ["101", "101", "101", "101", "111"],
        ['V'] = ["101", "101", "101", "101", "010"],
        ['W'] = ["101", "101", "111", "111", "101"],
        ['X'] = ["101", "101", "010", "101", "101"],
        ['Y'] = ["101", "101", "010", "010", "010"],
        ['Z'] = ["111", "001", "010", "100", "111"]
    };

    public GrimGame(ClientLaunchOptions launchOptions)
    {
        _clientTag = launchOptions.ClientTag;
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
        _previousMouseState = Mouse.GetState();
        _client.Initialize();
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);
        _basicEffect = new BasicEffect(GraphicsDevice)
        {
            VertexColorEnabled = true,
            LightingEnabled = false,
            FogEnabled = true,
            FogStart = 20f,
            FogEnd = 80f,
            FogColor = new Vector3(0.05f, 0.07f, 0.11f)
        };
    }

    protected override void Update(GameTime gameTime)
    {
        var snapshotView = _client.Runtime.GetSnapshotView();
        HandlePlayerInput();
        _client.Update(gameTime.ElapsedGameTime);
        HandleCameraInput(snapshotView, (float)gameTime.ElapsedGameTime.TotalSeconds);

        _windowTitleRefreshTimer += gameTime.ElapsedGameTime.TotalMilliseconds;
        if (_windowTitleRefreshTimer >= 250)
        {
            Window.Title = $"Grim - {_clientTag} | Tick {snapshotView.Tick} | Entities {snapshotView.Entities.Count} | Cam {_cameraDistance:F1}";
            _windowTitleRefreshTimer = 0;
        }

        base.Update(gameTime);
    }

    private void HandlePlayerInput()
    {
        var keyboard = Keyboard.GetState();
        var moveX = 0f;
        var moveZ = 0f;

        if (keyboard.IsKeyDown(Keys.Left) || keyboard.IsKeyDown(Keys.NumPad4))
        {
            moveX -= 1f;
        }

        if (keyboard.IsKeyDown(Keys.Right) || keyboard.IsKeyDown(Keys.NumPad6))
        {
            moveX += 1f;
        }

        if (keyboard.IsKeyDown(Keys.Up) || keyboard.IsKeyDown(Keys.NumPad8))
        {
            moveZ -= 1f;
        }

        if (keyboard.IsKeyDown(Keys.Down) || keyboard.IsKeyDown(Keys.NumPad2))
        {
            moveZ += 1f;
        }

        var moveLength = MathF.Sqrt((moveX * moveX) + (moveZ * moveZ));
        if (moveLength > 1f)
        {
            moveX /= moveLength;
            moveZ /= moveLength;
        }

        if (moveLength > 0f)
        {
            _playerYawRadians = MathF.Atan2(moveZ, moveX);
        }

        _client.Runtime.SetMovementIntent(moveX, moveZ, _playerYawRadians);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(12, 16, 28));
        var snapshotView = _client.Runtime.GetSnapshotView();

        var projection = Matrix.CreatePerspectiveFieldOfView(
            MathHelper.ToRadians(60f),
            GraphicsDevice.Viewport.AspectRatio,
            0.1f,
            250f);
        var cameraPosition = ComputeCameraPosition();
        var view = Matrix.CreateLookAt(cameraPosition, _cameraTarget, Vector3.Up);

        if (_basicEffect is not null)
        {
            GraphicsDevice.BlendState = BlendState.Opaque;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.RasterizerState = RasterizerState.CullNone;

            DrawWorldGrid3D(_basicEffect, view, projection);
            DrawEntities3D(_basicEffect, snapshotView, view, projection);
        }

        if (_spriteBatch is not null && _pixel is not null)
        {
            _spriteBatch.Begin();
            DrawEntityLabels(_spriteBatch, _pixel, snapshotView, view, projection);
            DrawHud(_spriteBatch, _pixel, _clientTag, _client.Runtime.NetworkStatus, snapshotView);
            _spriteBatch.End();
        }

        _client.Draw();
        base.Draw(gameTime);
    }

    private void HandleCameraInput(SnapshotView snapshotView, float deltaSeconds)
    {
        var desiredTarget = GetCameraCenter(snapshotView) + new Vector3(0f, 1.2f, 0f);
        if (!_cameraInitialized)
        {
            _cameraTarget = desiredTarget;
            _cameraInitialized = true;
        }
        else
        {
            var followLerp = MathF.Min(1f, deltaSeconds * 10f);
            _cameraTarget = Vector3.Lerp(_cameraTarget, desiredTarget, followLerp);
        }

        var mouse = Mouse.GetState();
        var keyboard = Keyboard.GetState();

        if (mouse.RightButton == ButtonState.Pressed && _previousMouseState.RightButton == ButtonState.Pressed)
        {
            var deltaX = mouse.X - _previousMouseState.X;
            var deltaY = mouse.Y - _previousMouseState.Y;
            _cameraYaw -= deltaX * 0.01f;
            _cameraPitch -= deltaY * 0.01f;
            _cameraPitch = MathHelper.Clamp(_cameraPitch, 0.25f, 1.4f);
        }

        var wheelDelta = mouse.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
        if (wheelDelta != 0)
        {
            _cameraDistance -= wheelDelta * 0.01f;
            _cameraDistance = MathHelper.Clamp(_cameraDistance, 6f, 80f);
        }

        if (keyboard.IsKeyDown(Keys.Space))
        {
            _cameraYaw = _playerYawRadians + MathF.PI;
        }

        _previousMouseState = mouse;
    }

    private Vector3 ComputeCameraPosition()
    {
        var horizontal = MathF.Cos(_cameraPitch) * _cameraDistance;
        var y = MathF.Sin(_cameraPitch) * _cameraDistance;
        var x = MathF.Cos(_cameraYaw) * horizontal;
        var z = MathF.Sin(_cameraYaw) * horizontal;
        return _cameraTarget + new Vector3(x, y, z);
    }

    private static Vector3 GetCameraCenter(SnapshotView snapshotView)
    {
        if (snapshotView.Entities.Count == 0)
        {
            return Vector3.Zero;
        }

        var localSessionId = snapshotView.LocalSessionId;
        if (localSessionId.HasValue)
        {
            foreach (var entity in snapshotView.Entities)
            {
                if (entity.OwnerSessionId == localSessionId.Value)
                {
                    return new Vector3(entity.Position.X, entity.Position.Y, entity.Position.Z);
                }
            }
        }

        var sum = Vector3.Zero;
        foreach (var entity in snapshotView.Entities)
        {
            sum += new Vector3(entity.Position.X, entity.Position.Y, entity.Position.Z);
        }

        return sum / snapshotView.Entities.Count;
    }

    private void DrawWorldGrid3D(BasicEffect effect, Matrix view, Matrix projection)
    {
        const int extent = 28;
        var vertices = new List<VertexPositionColor>((extent * 2 + 1) * 4);
        var lineColor = new Color(28, 38, 58);
        var axisColor = new Color(76, 104, 156);

        for (var i = -extent; i <= extent; i++)
        {
            var color = i == 0 ? axisColor : lineColor;
            vertices.Add(new VertexPositionColor(new Vector3(i, 0f, -extent), color));
            vertices.Add(new VertexPositionColor(new Vector3(i, 0f, extent), color));
            vertices.Add(new VertexPositionColor(new Vector3(-extent, 0f, i), color));
            vertices.Add(new VertexPositionColor(new Vector3(extent, 0f, i), color));
        }

        effect.World = Matrix.Identity;
        effect.View = view;
        effect.Projection = projection;

        foreach (var pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            GraphicsDevice.DrawUserPrimitives(
                PrimitiveType.LineList,
                vertices.ToArray(),
                0,
                vertices.Count / 2);
        }
    }

    private void DrawEntities3D(BasicEffect effect, SnapshotView snapshotView, Matrix view, Matrix projection)
    {
        foreach (var entity in snapshotView.Entities)
        {
            var isLocal = snapshotView.LocalSessionId.HasValue && entity.OwnerSessionId == snapshotView.LocalSessionId.Value;
            var baseColor = isLocal ? new Color(255, 210, 96) : ColorFromEntity(entity.Id.Value);
            var world = Matrix.CreateScale(isLocal ? 0.9f : 0.7f, isLocal ? 1.8f : 1.4f, isLocal ? 0.9f : 0.7f) *
                        Matrix.CreateTranslation(entity.Position.X, entity.Position.Y + (isLocal ? 0.9f : 0.7f), entity.Position.Z);

            var cubeVertices = BuildCubeVertices(baseColor);
            effect.World = world;
            effect.View = view;
            effect.Projection = projection;

            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.DrawUserIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    cubeVertices,
                    0,
                    cubeVertices.Length,
                    UnitCubeIndices,
                    0,
                    UnitCubeIndices.Length / 3);
            }

            if (isLocal)
            {
                DrawLocalHighlightRing(effect, entity.Position, view, projection);
            }
        }
    }

    private void DrawLocalHighlightRing(BasicEffect effect, Grim.Shared.Vector3Snapshot position, Matrix view, Matrix projection)
    {
        const int segments = 24;
        const float radius = 1.1f;
        var vertices = new VertexPositionColor[segments * 2];

        for (var i = 0; i < segments; i++)
        {
            var angleA = MathHelper.TwoPi * i / segments;
            var angleB = MathHelper.TwoPi * (i + 1) / segments;
            var pA = new Vector3(
                position.X + MathF.Cos(angleA) * radius,
                0.05f,
                position.Z + MathF.Sin(angleA) * radius);
            var pB = new Vector3(
                position.X + MathF.Cos(angleB) * radius,
                0.05f,
                position.Z + MathF.Sin(angleB) * radius);

            vertices[i * 2] = new VertexPositionColor(pA, new Color(255, 200, 110));
            vertices[(i * 2) + 1] = new VertexPositionColor(pB, new Color(255, 200, 110));
        }

        effect.World = Matrix.Identity;
        effect.View = view;
        effect.Projection = projection;

        foreach (var pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            GraphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, vertices, 0, vertices.Length / 2);
        }
    }

    private static VertexPositionColor[] BuildCubeVertices(Color color)
    {
        var lightDirection = Vector3.Normalize(new Vector3(0.4f, 1f, -0.3f));
        var vertices = new VertexPositionColor[UnitCubeCorners.Length];
        for (var i = 0; i < UnitCubeCorners.Length; i++)
        {
            var normalApprox = Vector3.Normalize(UnitCubeCorners[i]);
            var diffuse = MathF.Max(0f, Vector3.Dot(normalApprox, lightDirection));
            var brightness = 0.45f + (diffuse * 0.55f);
            var litColor = new Color(
                (byte)Math.Clamp((int)(color.R * brightness), 0, 255),
                (byte)Math.Clamp((int)(color.G * brightness), 0, 255),
                (byte)Math.Clamp((int)(color.B * brightness), 0, 255));

            vertices[i] = new VertexPositionColor(UnitCubeCorners[i], litColor);
        }

        return vertices;
    }

    private void DrawEntityLabels(SpriteBatch spriteBatch, Texture2D pixel, SnapshotView snapshotView, Matrix view, Matrix projection)
    {
        foreach (var entity in snapshotView.Entities)
        {
            var worldPosition = new Vector3(entity.Position.X, entity.Position.Y + 2.1f, entity.Position.Z);
            var projected = GraphicsDevice.Viewport.Project(worldPosition, projection, view, Matrix.Identity);
            if (projected.Z is < 0f or > 1f)
            {
                continue;
            }

            var x = (int)projected.X;
            var y = (int)projected.Y;
            var isLocal = snapshotView.LocalSessionId.HasValue && entity.OwnerSessionId == snapshotView.LocalSessionId.Value;
            var color = isLocal ? new Color(255, 210, 96) : ColorFromEntity(entity.Id.Value);
            var prefix = isLocal ? "YOU" : "ID";
            var labelText = $"{prefix} {entity.Id.Value.ToString("N")[..4].ToUpperInvariant()}";
            DrawLabel(spriteBatch, pixel, labelText, x, y - 8, color);
        }
    }

    private static void DrawLabel(SpriteBatch spriteBatch, Texture2D pixel, string text, int centerX, int baselineY, Color accent)
    {
        var scale = 2;
        var label = text.ToUpperInvariant();
        var width = MeasureTextWidth(label, scale);
        var height = 5 * scale;
        var x = centerX - (width / 2);
        var y = baselineY - height;

        var background = new Color(8, 10, 16, 210);
        spriteBatch.Draw(pixel, new Rectangle(x - 4, y - 2, width + 8, height + 4), background);
        spriteBatch.Draw(pixel, new Rectangle(x - 4, y + height + 1, width + 8, 1), accent);
        DrawText(spriteBatch, pixel, label, x, y, scale, Color.White);
    }

    private static void DrawHud(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        string clientTag,
        string networkStatus,
        SnapshotView snapshotView)
    {
        var hudX = 14;
        var hudY = 14;
        var hudWidth = 500;
        var hudHeight = 100;

        var panel = new Color(10, 14, 22, 220);
        var border = new Color(66, 92, 145);
        spriteBatch.Draw(pixel, new Rectangle(hudX, hudY, hudWidth, hudHeight), panel);
        spriteBatch.Draw(pixel, new Rectangle(hudX, hudY, hudWidth, 2), border);
        spriteBatch.Draw(pixel, new Rectangle(hudX, hudY + hudHeight - 2, hudWidth, 2), border);

        var statusWord = "WAIT";
        if (networkStatus.Contains("Connected", StringComparison.OrdinalIgnoreCase) || snapshotView.Entities.Count > 0)
        {
            statusWord = "OK";
        }
        else if (networkStatus.Contains("failed", StringComparison.OrdinalIgnoreCase) || networkStatus.Contains("closed", StringComparison.OrdinalIgnoreCase))
        {
            statusWord = "DISC";
        }

        DrawText(spriteBatch, pixel, $"CLIENT: {clientTag}".ToUpperInvariant(), hudX + 10, hudY + 10, 2, new Color(190, 223, 255));
        DrawText(spriteBatch, pixel, $"TICK: {snapshotView.Tick}", hudX + 10, hudY + 30, 2, Color.White);
        DrawText(spriteBatch, pixel, $"ENTS: {snapshotView.Entities.Count}", hudX + 10, hudY + 50, 2, Color.White);
        DrawText(spriteBatch, pixel, "ARROWS MOVE  RMB ORBIT  WHEEL ZOOM  SPACE SNAP BEHIND", hudX + 10, hudY + 70, 1, new Color(168, 190, 220));
        DrawText(spriteBatch, pixel, $"NET: {statusWord}", hudX + 245, hudY + 50, 2, StatusColor(statusWord));
    }

    private static Color StatusColor(string status)
    {
        return status switch
        {
            "OK" => new Color(88, 232, 144),
            "DISC" => new Color(232, 108, 108),
            _ => new Color(236, 212, 122)
        };
    }

    private static void DrawText(SpriteBatch spriteBatch, Texture2D pixel, string text, int x, int y, int scale, Color color)
    {
        var cursorX = x;
        foreach (var rawChar in text)
        {
            var ch = char.ToUpperInvariant(rawChar);
            if (!Glyphs.TryGetValue(ch, out var rows))
            {
                rows = Glyphs[' '];
            }

            for (var row = 0; row < rows.Length; row++)
            {
                var rowData = rows[row];
                for (var col = 0; col < rowData.Length; col++)
                {
                    if (rowData[col] == '1')
                    {
                        spriteBatch.Draw(pixel, new Rectangle(cursorX + (col * scale), y + (row * scale), scale, scale), color);
                    }
                }
            }

            cursorX += (3 * scale) + scale;
        }
    }

    private static int MeasureTextWidth(string text, int scale)
    {
        return text.Length * ((3 * scale) + scale) - scale;
    }

    private static Color ColorFromEntity(Guid entityId)
    {
        var bytes = entityId.ToByteArray();
        var red = (byte)(90 + (bytes[0] % 140));
        var green = (byte)(90 + (bytes[5] % 140));
        var blue = (byte)(90 + (bytes[10] % 140));
        return new Color(red, green, blue);
    }
}
