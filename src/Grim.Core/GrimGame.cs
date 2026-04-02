using Grim.Client;
using Grim.Shared;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Grim.Core;

public sealed class GrimGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly ClientBootstrap _client;
    private readonly string _clientTag;
    private readonly bool _editorEnabled;
    private readonly ZoneEditorPersistence _zoneEditorPersistence;

    private bool _editorModeActive;
    private Guid? _selectedEntityId;
    private readonly Dictionary<Guid, EditorTransformOverride> _editorOverrides = new();
    private readonly Stack<EditorTransformCommand> _undoCommands = new();
    private readonly Stack<EditorTransformCommand> _redoCommands = new();
    private readonly List<EditorCreatedStaticObject> _editorCreatedObjects = new();
    private readonly HashSet<int> _editorDeletedStaticIndices = new();
    private string _editorStatusMessage = "Editor bereit";
    private bool _isDraggingGizmo;
    private GizmoAxis _activeGizmoAxis = GizmoAxis.None;
    private Guid? _draggedEntityId;
    private float _dragPlaneY;
    private Vector3 _dragOffset;
    private Vector2 _gizmoDragStartMouse;
    private EditorTransformOverride _gizmoDragStartOverride;

    private SpriteBatch? _spriteBatch;
    private Texture2D? _pixel;
    private BasicEffect? _basicEffect;
    private RuntimeModelRegistry? _modelRegistry;

    private double _windowTitleRefreshTimer;
    private float _cameraYaw = 0.8f;
    private float _cameraPitch = 0.85f;
    private float _cameraDistance = 24f;
    private Vector3 _cameraTarget = Vector3.Zero;
    private float _playerYawRadians;
    private bool _cameraInitialized;
    private MouseState _previousMouseState;
    private KeyboardState _previousKeyboardState;

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

    private static readonly Vector3[] ObeliskVertices =
    [
        new Vector3(-0.55f, 0f, -0.55f),
        new Vector3(0.55f, 0f, -0.55f),
        new Vector3(0.55f, 0f, 0.55f),
        new Vector3(-0.55f, 0f, 0.55f),
        new Vector3(0f, 2.2f, 0f)
    ];

    private static readonly short[] ObeliskIndices =
    [
        0, 2, 1,
        0, 3, 2,
        0, 1, 4,
        1, 2, 4,
        2, 3, 4,
        3, 0, 4
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
        _editorEnabled = launchOptions.EditorEnabled;
        _editorModeActive = false;
        _zoneEditorPersistence = new ZoneEditorPersistence();
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
        _previousKeyboardState = Keyboard.GetState();
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

        _modelRegistry = RuntimeModelRegistry.TryLoadFromRepository();
    }

    protected override void Update(GameTime gameTime)
    {
        var deltaSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
        var keyboard = Keyboard.GetState();
        var mouse = Mouse.GetState();

        HandleEditorModeToggle(keyboard);
        var toggleButtonClicked = HandleEditorModeToggleButton(mouse);
        if (_editorModeActive)
        {
            _client.Runtime.SetMovementIntent(0f, 0f, _playerYawRadians);
        }
        else
        {
            HandlePlayerInput(keyboard);
        }

        _client.Runtime.AdvanceFrame(deltaSeconds);
        var snapshotView = _client.Runtime.GetSnapshotView();

        if (_editorModeActive)
        {
            HandleEditorInput(snapshotView, keyboard, mouse, deltaSeconds, toggleButtonClicked);
        }

        _client.Update(gameTime.ElapsedGameTime);
        HandleCameraInput(snapshotView, deltaSeconds, keyboard, mouse);

        _windowTitleRefreshTimer += gameTime.ElapsedGameTime.TotalMilliseconds;
        if (_windowTitleRefreshTimer >= 250)
        {
            var mode = _editorModeActive ? "EDITOR" : "PLAY";
            Window.Title = $"Grim - {_clientTag} [{mode}] | Tick {snapshotView.Tick} | Entities {snapshotView.Entities.Count} | Cam {_cameraDistance:F1}";
            _windowTitleRefreshTimer = 0;
        }

        _previousKeyboardState = keyboard;
        base.Update(gameTime);
    }

    private void HandlePlayerInput(KeyboardState keyboard)
    {
        var localMoveX = 0f;
        var localMoveZ = 0f;

        if (keyboard.IsKeyDown(Keys.Left) || keyboard.IsKeyDown(Keys.NumPad4))
        {
            localMoveX -= 1f;
        }

        if (keyboard.IsKeyDown(Keys.Right) || keyboard.IsKeyDown(Keys.NumPad6))
        {
            localMoveX += 1f;
        }

        if (keyboard.IsKeyDown(Keys.Up) || keyboard.IsKeyDown(Keys.NumPad8))
        {
            localMoveZ -= 1f;
        }

        if (keyboard.IsKeyDown(Keys.Down) || keyboard.IsKeyDown(Keys.NumPad2))
        {
            localMoveZ += 1f;
        }

        var moveLength = MathF.Sqrt((localMoveX * localMoveX) + (localMoveZ * localMoveZ));
        if (moveLength > 1f)
        {
            localMoveX /= moveLength;
            localMoveZ /= moveLength;
        }

        var sin = MathF.Sin(_cameraYaw);
        var cos = MathF.Cos(_cameraYaw);
        var moveX = (localMoveX * cos) - (localMoveZ * sin);
        var moveZ = (localMoveX * sin) + (localMoveZ * cos);

        var worldLength = MathF.Sqrt((moveX * moveX) + (moveZ * moveZ));
        if (worldLength > 1f)
        {
            moveX /= worldLength;
            moveZ /= worldLength;
        }

        if (worldLength > 0f)
        {
            _playerYawRadians = MathF.Atan2(moveZ, moveX);
        }

        _client.Runtime.SetMovementIntent(moveX, moveZ, _playerYawRadians);
    }

    private void HandleEditorModeToggle(KeyboardState keyboard)
    {
        if (!_editorEnabled)
        {
            _editorModeActive = false;
            return;
        }

        if (IsNewKeyPress(Keys.F1, keyboard))
        {
            _editorModeActive = !_editorModeActive;
            _editorStatusMessage = _editorModeActive ? "Editor aktiviert" : "Editor deaktiviert";
            if (!_editorModeActive)
            {
                _selectedEntityId = null;
                _isDraggingGizmo = false;
                _activeGizmoAxis = GizmoAxis.None;
                _draggedEntityId = null;
            }
        }
    }

    private bool HandleEditorModeToggleButton(MouseState mouse)
    {
        if (!_editorEnabled)
        {
            return false;
        }

        if (!IsLeftClick(mouse))
        {
            return false;
        }

        var buttonRect = GetEditorToggleButtonRect();
        if (!buttonRect.Contains(mouse.X, mouse.Y))
        {
            return false;
        }

        _editorModeActive = !_editorModeActive;
        _editorStatusMessage = _editorModeActive ? "Editor aktiviert" : "Editor deaktiviert";
        if (!_editorModeActive)
        {
            _selectedEntityId = null;
            _isDraggingGizmo = false;
            _activeGizmoAxis = GizmoAxis.None;
            _draggedEntityId = null;
        }

        return true;
    }

    private void HandleEditorInput(SnapshotView snapshotView, KeyboardState keyboard, MouseState mouse, float deltaSeconds, bool toggleButtonClicked)
    {
        var entities = snapshotView.Entities.ToArray();
        if (entities.Length == 0)
        {
            _selectedEntityId = null;
            _isDraggingGizmo = false;
            _activeGizmoAxis = GizmoAxis.None;
            _draggedEntityId = null;
            return;
        }

        var controlDown = keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl);
        var shiftDown = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);
        if (controlDown && IsNewKeyPress(Keys.Z, keyboard))
        {
            if (shiftDown)
            {
                RedoLastEditorCommand();
            }
            else
            {
                UndoLastEditorCommand();
            }
            return;
        }

        if (controlDown && IsNewKeyPress(Keys.Y, keyboard))
        {
            RedoLastEditorCommand();
            return;
        }

        if (_selectedEntityId is null)
        {
            _selectedEntityId = entities[0].Id.Value;
        }

        if (IsNewKeyPress(Keys.Tab, keyboard))
        {
            var currentIndex = Array.FindIndex(entities, entity => entity.Id.Value == _selectedEntityId.Value);
            if (currentIndex < 0)
            {
                _selectedEntityId = entities[0].Id.Value;
            }
            else
            {
                _selectedEntityId = entities[(currentIndex + 1) % entities.Length].Id.Value;
            }

            _editorStatusMessage = "Objekt per TAB ausgewaehlt";
        }

        if (IsNewKeyPress(Keys.Insert, keyboard))
        {
            var selectedForCreate = GetSelectedEntity(snapshotView);
            if (selectedForCreate is not null)
            {
                CreateStaticObjectFromSelected(selectedForCreate);
                return;
            }
        }

        if (!toggleButtonClicked && IsLeftClick(mouse) && !GetEditorToggleButtonRect().Contains(mouse.X, mouse.Y))
        {
            var selectedEntity = GetSelectedEntity(snapshotView);
            if (selectedEntity is not null && TryPickGizmoAxis(selectedEntity, mouse, out var axis))
            {
                StartGizmoDrag(selectedEntity, mouse, axis);
            }
            else
            {
                var clickedEntity = FindClickedEntity(entities, mouse);
                if (clickedEntity is not null)
                {
                    _selectedEntityId = clickedEntity.Id.Value;
                    _editorStatusMessage = "Objekt per Klick ausgewaehlt";
                }
            }
        }

        if (_isDraggingGizmo)
        {
            if (mouse.LeftButton != ButtonState.Pressed)
            {
                CommitGizmoDragCommand(snapshotView);
                _isDraggingGizmo = false;
                _activeGizmoAxis = GizmoAxis.None;
                _draggedEntityId = null;
            }
            else
            {
                ContinueGizmoDrag(snapshotView, mouse);
            }
        }

        var selected = GetSelectedEntity(snapshotView);
        if (selected is null)
        {
            _selectedEntityId = entities[0].Id.Value;
            selected = entities[0];
        }

        var moveSpeed = shiftDown ? 8f : 3.5f;
        var rotateSpeed = 1.8f;
        var scaleSpeed = 1.1f;

        var editorOverride = GetOrCreateEditorOverride(selected);
        var position = editorOverride.Position;
        var yaw = editorOverride.YawRadians;
        var scale = editorOverride.Scale;

        if (keyboard.IsKeyDown(Keys.J))
        {
            position.X -= moveSpeed * deltaSeconds;
        }

        if (keyboard.IsKeyDown(Keys.L))
        {
            position.X += moveSpeed * deltaSeconds;
        }

        if (keyboard.IsKeyDown(Keys.I))
        {
            position.Z -= moveSpeed * deltaSeconds;
        }

        if (keyboard.IsKeyDown(Keys.K))
        {
            position.Z += moveSpeed * deltaSeconds;
        }

        if (keyboard.IsKeyDown(Keys.U))
        {
            position.Y += moveSpeed * deltaSeconds;
        }

        if (keyboard.IsKeyDown(Keys.O))
        {
            position.Y -= moveSpeed * deltaSeconds;
        }

        if (keyboard.IsKeyDown(Keys.Z))
        {
            yaw -= rotateSpeed * deltaSeconds;
        }

        if (keyboard.IsKeyDown(Keys.X))
        {
            yaw += rotateSpeed * deltaSeconds;
        }

        if (keyboard.IsKeyDown(Keys.C))
        {
            scale = MathF.Max(0.1f, scale - (scaleSpeed * deltaSeconds));
        }

        if (keyboard.IsKeyDown(Keys.V))
        {
            scale = MathF.Min(10f, scale + (scaleSpeed * deltaSeconds));
        }

        if (IsNewKeyPress(Keys.Delete, keyboard))
        {
            ToggleDeleteSelectedStaticObject(selected);
            return;
        }

        if (IsNewKeyPress(Keys.Back, keyboard))
        {
            if (_editorOverrides.TryGetValue(selected.Id.Value, out var before))
            {
                _editorOverrides.Remove(selected.Id.Value);
                PushEditorCommand(new EditorTransformCommand(selected.Id.Value, before, null, "Override entfernt"));
                _editorStatusMessage = "Lokaler Override entfernt";
            }
            return;
        }

        if (IsNewKeyPress(Keys.F5, keyboard))
        {
            SaveEditorOverrides(snapshotView);
            return;
        }

        _editorOverrides[selected.Id.Value] = editorOverride with
        {
            Position = position,
            YawRadians = yaw,
            Scale = scale
        };
        _editorStatusMessage = _isDraggingGizmo ? "Gizmo-Drag aktiv" : "Lokale Aenderung aktiv (F5 zum Speichern)";
    }

    private void SaveEditorOverrides(SnapshotView snapshotView)
    {
        var updates = new List<ZoneStaticObjectOverride>();
        foreach (var entity in snapshotView.Entities)
        {
            if (entity.OwnerSessionId != Guid.Empty || !entity.ZoneStaticIndex.HasValue)
            {
                continue;
            }

            if (!_editorOverrides.TryGetValue(entity.Id.Value, out var value))
            {
                continue;
            }

            if (_editorDeletedStaticIndices.Contains(entity.ZoneStaticIndex.Value))
            {
                continue;
            }

            updates.Add(new ZoneStaticObjectOverride(
                entity.ZoneStaticIndex.Value,
                new ZonePosition(value.Position.X, value.Position.Y, value.Position.Z),
                value.YawRadians,
                value.Scale,
                value.ModelIdOverride,
                value.HasModelOverride));
        }

        var drafts = _editorCreatedObjects
            .Select(item => new ZoneStaticObjectDraft(
                new ZonePosition(item.Position.X, item.Position.Y, item.Position.Z),
                item.YawRadians,
                item.Scale,
                item.ModelId))
            .ToArray();

        var result = _zoneEditorPersistence.SaveStaticObjectEdits(updates, drafts, _editorDeletedStaticIndices);
        if (result.Success)
        {
            _editorCreatedObjects.Clear();
            _editorDeletedStaticIndices.Clear();
            _editorStatusMessage = $"Gespeichert: {result.AppliedCount} Aenderung(en)";
        }
        else
        {
            _editorStatusMessage = $"Save fehlgeschlagen: {result.Message}";
        }
    }

    private bool IsNewKeyPress(Keys key, KeyboardState keyboard)
    {
        return keyboard.IsKeyDown(key) && _previousKeyboardState.IsKeyUp(key);
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
            DrawEditorHud(_spriteBatch, _pixel, snapshotView);
            _spriteBatch.End();
        }

        _client.Draw();
        base.Draw(gameTime);
    }

    private void HandleCameraInput(SnapshotView snapshotView, float deltaSeconds, KeyboardState keyboard, MouseState mouse)
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

    private Vector3 GetCameraCenter(SnapshotView snapshotView)
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
                    return GetRenderPosition(entity);
                }
            }
        }

        var sum = Vector3.Zero;
        foreach (var entity in snapshotView.Entities)
        {
            sum += GetRenderPosition(entity);
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
            var isStatic = entity.OwnerSessionId == Guid.Empty;

            if (isStatic && entity.ZoneStaticIndex.HasValue && _editorDeletedStaticIndices.Contains(entity.ZoneStaticIndex.Value))
            {
                continue;
            }

            var position = GetRenderPosition(entity);
            var yaw = GetRenderYaw(entity);
            var scaleMultiplier = GetRenderScale(entity);
            var modelId = GetRenderModelId(entity);

            var renderedAsModel = false;

            if (isStatic && !string.IsNullOrWhiteSpace(modelId))
            {
                if (TryDrawRegisteredModel(effect, modelId, view, projection, position, yaw, scaleMultiplier))
                {
                    renderedAsModel = true;
                }

                if (!renderedAsModel && string.Equals(modelId, "obelisk_v1", StringComparison.Ordinal))
                {
                    DrawObelisk3D(effect, view, projection, position, yaw, scaleMultiplier);
                    renderedAsModel = true;
                }
            }

            if (!renderedAsModel)
            {
                var baseColor = isLocal
                    ? new Color(255, 210, 96)
                    : isStatic
                        ? new Color(120, 165, 208)
                        : ColorFromEntity(entity.Id.Value);
                var scale = (isLocal ? new Vector3(0.9f, 1.8f, 0.9f) : isStatic ? new Vector3(1.8f, 2.4f, 1.8f) : new Vector3(0.7f, 1.4f, 0.7f)) * scaleMultiplier;
                var world = Matrix.CreateScale(scale) *
                            Matrix.CreateRotationY(yaw) *
                            Matrix.CreateTranslation(position.X, position.Y + (scale.Y * 0.5f), position.Z);

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
            }

            if (isLocal)
            {
                DrawLocalHighlightRing(effect, new Vector3Snapshot(position.X, position.Y, position.Z), view, projection);
            }

            if (_editorModeActive && IsSelectedEntity(entity))
            {
                DrawEditorSelectionRing(effect, position, view, projection);
                DrawEditorGizmo(effect, position, view, projection);
            }
        }

        DrawCreatedStaticObjects(effect, view, projection);
    }

    private void DrawCreatedStaticObjects(BasicEffect effect, Matrix view, Matrix projection)
    {
        foreach (var created in _editorCreatedObjects)
        {
            var drawn = false;
            if (!string.IsNullOrWhiteSpace(created.ModelId))
            {
                drawn = TryDrawRegisteredModel(effect, created.ModelId, view, projection, created.Position, created.YawRadians, created.Scale);
            }

            if (!drawn)
            {
                var world = Matrix.CreateScale(new Vector3(1.8f, 2.4f, 1.8f) * created.Scale) *
                            Matrix.CreateRotationY(created.YawRadians) *
                            Matrix.CreateTranslation(created.Position.X, created.Position.Y + (1.2f * created.Scale), created.Position.Z);

                var vertices = BuildCubeVertices(new Color(132, 222, 164));
                effect.World = world;
                effect.View = view;
                effect.Projection = projection;

                foreach (var pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    GraphicsDevice.DrawUserIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        vertices,
                        0,
                        vertices.Length,
                        UnitCubeIndices,
                        0,
                        UnitCubeIndices.Length / 3);
                }
            }

            DrawEditorSelectionRing(effect, created.Position, view, projection);
        }
    }

    private void DrawObelisk3D(BasicEffect effect, Matrix view, Matrix projection, Vector3 position, float yaw, float scaleMultiplier)
    {
        effect.TextureEnabled = false;
        effect.VertexColorEnabled = true;

        var baseColor = new Color(186, 214, 238);
        var world = Matrix.CreateScale(1.25f * scaleMultiplier) *
                    Matrix.CreateRotationY(yaw) *
                    Matrix.CreateTranslation(position.X, position.Y, position.Z);

        var vertices = BuildShadedVertices(ObeliskVertices, baseColor);

        effect.World = world;
        effect.View = view;
        effect.Projection = projection;

        foreach (var pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            GraphicsDevice.DrawUserIndexedPrimitives(
                PrimitiveType.TriangleList,
                vertices,
                0,
                vertices.Length,
                ObeliskIndices,
                0,
                ObeliskIndices.Length / 3);
        }
    }

    private bool TryDrawRegisteredModel(BasicEffect effect, string modelId, Matrix view, Matrix projection, Vector3 position, float yaw, float scaleMultiplier)
    {
        if (_modelRegistry is null || string.IsNullOrWhiteSpace(modelId))
        {
            return false;
        }

        if (!_modelRegistry.TryGetModel(modelId, GraphicsDevice, out var modelAsset))
        {
            return false;
        }

        var world = Matrix.CreateScale(modelAsset.Scale * scaleMultiplier) *
                    Matrix.CreateRotationY(yaw) *
                    Matrix.CreateTranslation(position.X, position.Y, position.Z);

        effect.World = world;
        effect.View = view;
        effect.Projection = projection;
        effect.TextureEnabled = true;
        effect.Texture = modelAsset.Texture;
        effect.VertexColorEnabled = false;

        foreach (var pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            GraphicsDevice.DrawUserIndexedPrimitives(
                PrimitiveType.TriangleList,
                modelAsset.Vertices,
                0,
                modelAsset.Vertices.Length,
                modelAsset.Indices,
                0,
                modelAsset.Indices.Length / 3);
        }

        effect.TextureEnabled = false;
        effect.Texture = null;
        effect.VertexColorEnabled = true;
        return true;
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

    private void DrawEditorSelectionRing(BasicEffect effect, Vector3 position, Matrix view, Matrix projection)
    {
        const int segments = 28;
        const float radius = 1.9f;
        var vertices = new VertexPositionColor[segments * 2];

        for (var i = 0; i < segments; i++)
        {
            var angleA = MathHelper.TwoPi * i / segments;
            var angleB = MathHelper.TwoPi * (i + 1) / segments;
            var pA = new Vector3(position.X + (MathF.Cos(angleA) * radius), position.Y + 0.08f, position.Z + (MathF.Sin(angleA) * radius));
            var pB = new Vector3(position.X + (MathF.Cos(angleB) * radius), position.Y + 0.08f, position.Z + (MathF.Sin(angleB) * radius));
            vertices[i * 2] = new VertexPositionColor(pA, new Color(90, 230, 255));
            vertices[(i * 2) + 1] = new VertexPositionColor(pB, new Color(90, 230, 255));
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

    private void DrawEditorGizmo(BasicEffect effect, Vector3 position, Matrix view, Matrix projection)
    {
        const float axisLength = 2.2f;
        var xColor = _activeGizmoAxis == GizmoAxis.X ? new Color(255, 236, 120) : new Color(255, 96, 96);
        var yColor = _activeGizmoAxis == GizmoAxis.Y ? new Color(255, 236, 120) : new Color(96, 255, 120);
        var zColor = _activeGizmoAxis == GizmoAxis.Z ? new Color(255, 236, 120) : new Color(96, 170, 255);
        var vertices = new[]
        {
            new VertexPositionColor(position, xColor),
            new VertexPositionColor(position + new Vector3(axisLength, 0f, 0f), xColor),
            new VertexPositionColor(position, yColor),
            new VertexPositionColor(position + new Vector3(0f, axisLength, 0f), yColor),
            new VertexPositionColor(position, zColor),
            new VertexPositionColor(position + new Vector3(0f, 0f, axisLength), zColor)
        };

        effect.World = Matrix.Identity;
        effect.View = view;
        effect.Projection = projection;

        foreach (var pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            GraphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, vertices, 0, vertices.Length / 2);
        }
    }

    private Vector3 GetRenderPosition(EntitySnapshot entity)
    {
        if (_editorOverrides.TryGetValue(entity.Id.Value, out var value))
        {
            return value.Position;
        }

        return new Vector3(entity.Position.X, entity.Position.Y, entity.Position.Z);
    }

    private float GetRenderScale(EntitySnapshot entity)
    {
        if (_editorOverrides.TryGetValue(entity.Id.Value, out var value))
        {
            return value.Scale;
        }

        return 1f;
    }

    private string? GetRenderModelId(EntitySnapshot entity)
    {
        if (_editorOverrides.TryGetValue(entity.Id.Value, out var value) && value.HasModelOverride)
        {
            return value.ModelIdOverride;
        }

        return entity.ModelId;
    }

    private float GetRenderYaw(EntitySnapshot entity)
    {
        if (_editorOverrides.TryGetValue(entity.Id.Value, out var value))
        {
            return value.YawRadians;
        }

        return entity.YawRadians;
    }

    private bool IsSelectedEntity(EntitySnapshot entity)
    {
        return _editorModeActive && _selectedEntityId.HasValue && entity.Id.Value == _selectedEntityId.Value;
    }

    private static VertexPositionColor[] BuildCubeVertices(Color color)
    {
        return BuildShadedVertices(UnitCubeCorners, color);
    }

    private static VertexPositionColor[] BuildShadedVertices(IReadOnlyList<Vector3> verticesInput, Color color)
    {
        var lightDirection = Vector3.Normalize(new Vector3(0.4f, 1f, -0.3f));
        var vertices = new VertexPositionColor[verticesInput.Count];
        for (var i = 0; i < verticesInput.Count; i++)
        {
            var normalApprox = Vector3.Normalize(verticesInput[i]);
            var diffuse = MathF.Max(0f, Vector3.Dot(normalApprox, lightDirection));
            var brightness = 0.45f + (diffuse * 0.55f);
            var litColor = new Color(
                (byte)Math.Clamp((int)(color.R * brightness), 0, 255),
                (byte)Math.Clamp((int)(color.G * brightness), 0, 255),
                (byte)Math.Clamp((int)(color.B * brightness), 0, 255));

            vertices[i] = new VertexPositionColor(verticesInput[i], litColor);
        }

        return vertices;
    }

    private void DrawEntityLabels(SpriteBatch spriteBatch, Texture2D pixel, SnapshotView snapshotView, Matrix view, Matrix projection)
    {
        foreach (var entity in snapshotView.Entities)
        {
            if (entity.OwnerSessionId == Guid.Empty)
            {
                continue;
            }

            var position = GetRenderPosition(entity);
            var worldPosition = new Vector3(position.X, position.Y + 2.1f, position.Z);
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

    private void DrawEditorHud(SpriteBatch spriteBatch, Texture2D pixel, SnapshotView snapshotView)
    {
        if (!_editorEnabled)
        {
            return;
        }

        var panelX = 14;
        var panelY = 122;
        var panelWidth = 560;
        var panelHeight = 88;
        var panel = _editorModeActive ? new Color(21, 42, 34, 220) : new Color(30, 26, 18, 220);
        var border = _editorModeActive ? new Color(95, 210, 160) : new Color(190, 156, 94);

        spriteBatch.Draw(pixel, new Rectangle(panelX, panelY, panelWidth, panelHeight), panel);
        spriteBatch.Draw(pixel, new Rectangle(panelX, panelY, panelWidth, 2), border);
        spriteBatch.Draw(pixel, new Rectangle(panelX, panelY + panelHeight - 2, panelWidth, 2), border);

        var modeText = _editorModeActive ? "EDITOR MODE: ON" : "EDITOR MODE: OFF";
        DrawText(spriteBatch, pixel, modeText, panelX + 10, panelY + 10, 2, Color.White);
        DrawText(spriteBatch, pixel, "F1 TOGGLE  INS DUPLICATE  DEL TOGGLE DELETE  LMB AXIS DRAG  C/V SCALE  CTRL+Z/Y", panelX + 10, panelY + 36, 1, new Color(184, 212, 232));

        var buttonRect = GetEditorToggleButtonRect();
        var buttonColor = _editorModeActive ? new Color(58, 170, 122) : new Color(148, 110, 68);
        var buttonBorder = _editorModeActive ? new Color(146, 248, 208) : new Color(238, 204, 158);
        spriteBatch.Draw(pixel, buttonRect, buttonColor);
        spriteBatch.Draw(pixel, new Rectangle(buttonRect.X, buttonRect.Y, buttonRect.Width, 2), buttonBorder);
        spriteBatch.Draw(pixel, new Rectangle(buttonRect.X, buttonRect.Bottom - 2, buttonRect.Width, 2), buttonBorder);
        DrawText(spriteBatch, pixel, _editorModeActive ? "EDITOR AUS" : "EDITOR AN", buttonRect.X + 12, buttonRect.Y + 8, 1, new Color(10, 14, 18));

        var selectedText = "SELECTED: NONE";
        if (_editorModeActive)
        {
            var selected = GetSelectedEntity(snapshotView);
            if (selected is not null)
            {
                var pos = GetRenderPosition(selected);
                selectedText = $"SELECTED: {selected.Id.Value.ToString("N")[..4].ToUpperInvariant()}  POS {pos.X:F1},{pos.Y:F1},{pos.Z:F1}  S {GetRenderScale(selected):F2}";
            }
        }

        DrawText(spriteBatch, pixel, selectedText, panelX + 10, panelY + 56, 1, new Color(206, 236, 228));
        var pendingText = $"PENDING ADD: {_editorCreatedObjects.Count}  PENDING DEL: {_editorDeletedStaticIndices.Count}";
        DrawText(spriteBatch, pixel, pendingText, panelX + 10, panelY + 72, 1, new Color(206, 236, 228));
        DrawText(spriteBatch, pixel, _editorStatusMessage.ToUpperInvariant(), panelX + 10, panelY + 82, 1, new Color(168, 244, 194));
    }

    private void CreateStaticObjectFromSelected(EntitySnapshot selected)
    {
        var sourcePosition = GetRenderPosition(selected);
        var created = new EditorCreatedStaticObject(
            sourcePosition + new Vector3(2f, 0f, 0f),
            GetRenderYaw(selected),
            GetRenderScale(selected),
            GetRenderModelId(selected));

        _editorCreatedObjects.Add(created);
        _editorStatusMessage = "Neues Static Object erstellt (pending save)";
    }

    private void ToggleDeleteSelectedStaticObject(EntitySnapshot selected)
    {
        if (!selected.ZoneStaticIndex.HasValue)
        {
            _editorStatusMessage = "Objekt hat keinen static index";
            return;
        }

        var index = selected.ZoneStaticIndex.Value;
        if (_editorDeletedStaticIndices.Contains(index))
        {
            _editorDeletedStaticIndices.Remove(index);
            _editorStatusMessage = $"Delete-Markierung entfernt (#{index})";
        }
        else
        {
            _editorDeletedStaticIndices.Add(index);
            _editorStatusMessage = $"Objekt fuer Loeschen markiert (#{index})";
        }
    }

    private Rectangle GetEditorToggleButtonRect()
    {
        return new Rectangle(430, 130, 128, 24);
    }

    private EntitySnapshot? GetSelectedEntity(SnapshotView snapshotView)
    {
        if (!_selectedEntityId.HasValue)
        {
            return null;
        }

        return snapshotView.Entities.FirstOrDefault(entity => entity.Id.Value == _selectedEntityId.Value);
    }

    private EntitySnapshot? FindClickedEntity(IReadOnlyList<EntitySnapshot> entities, MouseState mouse)
    {
        var projection = Matrix.CreatePerspectiveFieldOfView(
            MathHelper.ToRadians(60f),
            GraphicsDevice.Viewport.AspectRatio,
            0.1f,
            250f);
        var view = Matrix.CreateLookAt(ComputeCameraPosition(), _cameraTarget, Vector3.Up);

        var clicked = new Vector2(mouse.X, mouse.Y);
        EntitySnapshot? bestEntity = null;
        var bestDistanceSquared = float.MaxValue;
        const float maxPickDistancePixels = 44f;
        var maxPickDistanceSquared = maxPickDistancePixels * maxPickDistancePixels;

        for (var i = 0; i < entities.Count; i++)
        {
            var position = GetRenderPosition(entities[i]);
            var projected = GraphicsDevice.Viewport.Project(position, projection, view, Matrix.Identity);
            if (projected.Z is < 0f or > 1f)
            {
                continue;
            }

            var dx = projected.X - clicked.X;
            var dy = projected.Y - clicked.Y;
            var distanceSquared = (dx * dx) + (dy * dy);
            if (distanceSquared > maxPickDistanceSquared)
            {
                continue;
            }

            if (distanceSquared < bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                bestEntity = entities[i];
            }
        }

        return bestEntity;
    }

    private bool TryPickGizmoAxis(EntitySnapshot selectedEntity, MouseState mouse, out GizmoAxis axis)
    {
        var origin = GetRenderPosition(selectedEntity);
        if (!ProjectWorldToScreen(origin, out var origin2D))
        {
            axis = GizmoAxis.None;
            return false;
        }

        var cursor = new Vector2(mouse.X, mouse.Y);
        var bestDistance = float.MaxValue;
        var bestAxis = GizmoAxis.None;

        var axisCandidates = new[]
        {
            (Axis: GizmoAxis.X, Direction: Vector3.UnitX),
            (Axis: GizmoAxis.Y, Direction: Vector3.UnitY),
            (Axis: GizmoAxis.Z, Direction: Vector3.UnitZ)
        };

        foreach (var candidate in axisCandidates)
        {
            var endPoint = origin + (candidate.Direction * 2.2f);
            if (!ProjectWorldToScreen(endPoint, out var end2D))
            {
                continue;
            }

            var distance = DistancePointToSegment(cursor, origin2D, end2D);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestAxis = candidate.Axis;
            }
        }

        axis = bestDistance <= 12f ? bestAxis : GizmoAxis.None;
        return axis != GizmoAxis.None;
    }

    private void StartGizmoDrag(EntitySnapshot selectedEntity, MouseState mouse, GizmoAxis axis)
    {
        _isDraggingGizmo = true;
        _activeGizmoAxis = axis;
        _draggedEntityId = selectedEntity.Id.Value;
        _gizmoDragStartOverride = GetOrCreateEditorOverride(selectedEntity);
        var position = _gizmoDragStartOverride.Position;
        _gizmoDragStartMouse = new Vector2(mouse.X, mouse.Y);
        _dragPlaneY = position.Y;

        if (TryGetWorldPointOnPlane(mouse, _dragPlaneY, out var worldPoint))
        {
            _dragOffset = position - worldPoint;
        }
        else
        {
            _dragOffset = Vector3.Zero;
        }

        _editorStatusMessage = $"Gizmo Drag gestartet ({axis})";
    }

    private void ContinueGizmoDrag(SnapshotView snapshotView, MouseState mouse)
    {
        if (!_draggedEntityId.HasValue)
        {
            return;
        }

        var entity = snapshotView.Entities.FirstOrDefault(item => item.Id.Value == _draggedEntityId.Value);
        if (entity is null)
        {
            return;
        }

        if (!TryGetWorldPointOnPlane(mouse, _dragPlaneY, out var worldPoint))
        {
            return;
        }

        var axisDirection = _activeGizmoAxis switch
        {
            GizmoAxis.X => Vector3.UnitX,
            GizmoAxis.Y => Vector3.UnitY,
            GizmoAxis.Z => Vector3.UnitZ,
            _ => Vector3.Zero
        };

        if (axisDirection == Vector3.Zero)
        {
            return;
        }

        var origin = _gizmoDragStartOverride.Position;
        if (!ProjectWorldToScreen(origin, out var origin2D) || !ProjectWorldToScreen(origin + (axisDirection * 2.2f), out var axisEnd2D))
        {
            return;
        }

        var axisScreen = axisEnd2D - origin2D;
        var axisScreenLength = axisScreen.Length();
        if (axisScreenLength < 0.001f)
        {
            return;
        }

        var axisScreenDirection = axisScreen / axisScreenLength;
        var currentMouse = new Vector2(mouse.X, mouse.Y);
        var mouseDelta = currentMouse - _gizmoDragStartMouse;
        var deltaPixelsOnAxis = Vector2.Dot(mouseDelta, axisScreenDirection);
        var worldUnitsPerPixel = 2.2f / axisScreenLength;
        var deltaWorld = deltaPixelsOnAxis * worldUnitsPerPixel;

        var newPosition = _gizmoDragStartOverride.Position + (axisDirection * deltaWorld);
        _editorOverrides[entity.Id.Value] = _gizmoDragStartOverride with { Position = newPosition };
        _editorStatusMessage = "Gizmo-Drag aktiv";
    }

    private void CommitGizmoDragCommand(SnapshotView snapshotView)
    {
        if (!_draggedEntityId.HasValue)
        {
            return;
        }

        var entity = snapshotView.Entities.FirstOrDefault(item => item.Id.Value == _draggedEntityId.Value);
        if (entity is null)
        {
            return;
        }

        if (!_editorOverrides.TryGetValue(entity.Id.Value, out var current))
        {
            return;
        }

        if (current == _gizmoDragStartOverride)
        {
            return;
        }

        PushEditorCommand(new EditorTransformCommand(entity.Id.Value, _gizmoDragStartOverride, current, $"Gizmo {_activeGizmoAxis} Drag"));
    }

    private void PushEditorCommand(EditorTransformCommand command)
    {
        _undoCommands.Push(command);
        _redoCommands.Clear();
    }

    private void UndoLastEditorCommand()
    {
        if (_undoCommands.Count == 0)
        {
            _editorStatusMessage = "Undo: keine Aktion";
            return;
        }

        var command = _undoCommands.Pop();
        if (command.Before is null)
        {
            _editorOverrides.Remove(command.EntityId);
        }
        else
        {
            _editorOverrides[command.EntityId] = command.Before.Value;
        }

        _redoCommands.Push(command);
        _editorStatusMessage = $"Undo: {command.Label}";
    }

    private void RedoLastEditorCommand()
    {
        if (_redoCommands.Count == 0)
        {
            _editorStatusMessage = "Redo: keine Aktion";
            return;
        }

        var command = _redoCommands.Pop();
        if (command.After is null)
        {
            _editorOverrides.Remove(command.EntityId);
        }
        else
        {
            _editorOverrides[command.EntityId] = command.After.Value;
        }

        _undoCommands.Push(command);
        _editorStatusMessage = $"Redo: {command.Label}";
    }

    private EditorTransformOverride GetOrCreateEditorOverride(EntitySnapshot entity)
    {
        if (_editorOverrides.TryGetValue(entity.Id.Value, out var value))
        {
            return value;
        }

        var created = new EditorTransformOverride(
            new Vector3(entity.Position.X, entity.Position.Y, entity.Position.Z),
            entity.YawRadians,
            1f,
            false,
            null);
        _editorOverrides[entity.Id.Value] = created;
        return created;
    }

    private bool ProjectWorldToScreen(Vector3 worldPoint, out Vector2 screenPoint)
    {
        var projection = Matrix.CreatePerspectiveFieldOfView(
            MathHelper.ToRadians(60f),
            GraphicsDevice.Viewport.AspectRatio,
            0.1f,
            250f);
        var view = Matrix.CreateLookAt(ComputeCameraPosition(), _cameraTarget, Vector3.Up);

        var projected = GraphicsDevice.Viewport.Project(worldPoint, projection, view, Matrix.Identity);
        if (projected.Z is < 0f or > 1f)
        {
            screenPoint = Vector2.Zero;
            return false;
        }

        screenPoint = new Vector2(projected.X, projected.Y);
        return true;
    }

    private static float DistancePointToSegment(Vector2 point, Vector2 segmentA, Vector2 segmentB)
    {
        var segment = segmentB - segmentA;
        var segmentLengthSquared = segment.LengthSquared();
        if (segmentLengthSquared < 0.0001f)
        {
            return Vector2.Distance(point, segmentA);
        }

        var t = Vector2.Dot(point - segmentA, segment) / segmentLengthSquared;
        t = MathHelper.Clamp(t, 0f, 1f);
        var projection = segmentA + (segment * t);
        return Vector2.Distance(point, projection);
    }

    private bool TryGetWorldPointOnPlane(MouseState mouse, float planeY, out Vector3 worldPoint)
    {
        var projection = Matrix.CreatePerspectiveFieldOfView(
            MathHelper.ToRadians(60f),
            GraphicsDevice.Viewport.AspectRatio,
            0.1f,
            250f);
        var view = Matrix.CreateLookAt(ComputeCameraPosition(), _cameraTarget, Vector3.Up);

        var near = GraphicsDevice.Viewport.Unproject(new Vector3(mouse.X, mouse.Y, 0f), projection, view, Matrix.Identity);
        var far = GraphicsDevice.Viewport.Unproject(new Vector3(mouse.X, mouse.Y, 1f), projection, view, Matrix.Identity);
        var direction = far - near;
        if (MathF.Abs(direction.Y) < 0.0001f)
        {
            worldPoint = Vector3.Zero;
            return false;
        }

        var t = (planeY - near.Y) / direction.Y;
        if (t < 0f)
        {
            worldPoint = Vector3.Zero;
            return false;
        }

        worldPoint = near + (direction * t);
        return true;
    }

    private bool IsLeftClick(MouseState mouse)
    {
        return mouse.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released;
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

    private readonly record struct EditorTransformOverride(
        Vector3 Position,
        float YawRadians,
        float Scale,
        bool HasModelOverride,
        string? ModelIdOverride);

    private readonly record struct EditorTransformCommand(Guid EntityId, EditorTransformOverride? Before, EditorTransformOverride? After, string Label);

    private readonly record struct EditorCreatedStaticObject(Vector3 Position, float YawRadians, float Scale, string? ModelId);

    private enum GizmoAxis
    {
        None,
        X,
        Y,
        Z
    }
}
