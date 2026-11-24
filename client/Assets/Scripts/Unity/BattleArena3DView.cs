using System;
using System.Collections.Generic;
using Qqtang.Core.Battle;
using UnityEngine;

namespace Qqtang.UnityClient
{
    /// <summary>
    /// Renders Rule1BattleSimulation in Unity 3D:
    ///   - 3D grid floor & extruded solid/breakable blocks mapped with Daoist/original textures
    ///   - 2D billboard SpriteRenderers for characters (4-dir), bombs, flames, and pickups
    /// </summary>
    public sealed class BattleArena3DView : MonoBehaviour
    {
        private const float TileSize = 1.0f;

        private ArtAssetResolver _art;
        private Transform _arenaRoot;
        private Transform _dynamicRoot;
        private Camera _camera;
        private Light _sunLight;

        private int _width;
        private int _height;
        private GameObject[] _blockObjects;
        private CellCollision[] _lastCellStates;

        private readonly Dictionary<ushort, ActorVisual> _actorVisuals = new Dictionary<ushort, ActorVisual>();
        private readonly Dictionary<ushort, BossVisual> _bossVisuals = new Dictionary<ushort, BossVisual>();
        private readonly List<GameObject> _pooledBombs = new List<GameObject>();
        private readonly List<GameObject> _pooledFlames = new List<GameObject>();
        private readonly List<GameObject> _pooledPickups = new List<GameObject>();

        private Vector3 _baseCameraPos;
        private Vector3 _cameraLookTarget;
        private float _shakeTimer;
        private float _shakeIntensity;
        public float CameraZoom { get; set; } = 1.0f;

        private sealed class ActorVisual
        {
            public GameObject Root;
            public Transform SpriteTransform;
            public SpriteRenderer Body;
            public GameObject TrappedBubble;
            public GameObject ShieldAura;
            public GameObject EmoteBadge;
            public GameObject BunHalo;
            public GameObject TeamRing;
            public BattleDirection LastDir = BattleDirection.None;
            public byte RoleId;
        }

        private sealed class BossVisual
        {
            public GameObject Root;
            public Transform HpFill;
        }

        public void TriggerShake(float intensity = 0.16f, float duration = 0.18f)
        {
            _shakeIntensity = Mathf.Max(_shakeIntensity, intensity);
            _shakeTimer = Mathf.Max(_shakeTimer, duration);
        }

        public void Initialize(ArtAssetResolver art)
        {
            _art = art;
            EnsureCameraAndLight();
        }

        private void EnsureCameraAndLight()
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                var camObj = new GameObject("Main Camera");
                camObj.tag = "MainCamera";
                _camera = camObj.AddComponent<Camera>();
            }
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.11f, 0.14f, 0.19f, 1f);
            _camera.fieldOfView = 36f;

            if (_sunLight == null)
            {
                var lightObj = new GameObject("Arena Directional Light");
                _sunLight = lightObj.AddComponent<Light>();
                _sunLight.type = LightType.Directional;
                _sunLight.intensity = 1.15f;
                _sunLight.color = new Color(1f, 0.97f, 0.9f);
                lightObj.transform.rotation = Quaternion.Euler(52f, -25f, 0f);
            }
            RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.65f);
        }

        public void BuildArena(Rule1BattleSimulation sim)
        {
            ClearArena();
            if (sim == null || sim.Map == null) return;

            _width = sim.Map.Width;
            _height = sim.Map.Height;
            _blockObjects = new GameObject[_width * _height];
            _lastCellStates = new CellCollision[_width * _height];

            _arenaRoot = new GameObject("BattleArena3D").transform;
            _dynamicRoot = new GameObject("DynamicEntities").transform;
            _dynamicRoot.SetParent(_arenaRoot, false);

            // Position 3D camera at classic angled top-down perspective over the board
            float cx = (_width - 1) * 0.5f * TileSize;
            float cz = -(_height - 1) * 0.5f * TileSize;
            _cameraLookTarget = new Vector3(cx, 0f, cz - 0.35f);
            _baseCameraPos = new Vector3(cx, Mathf.Max(_width, _height) * 1.18f, cz - _height * 0.62f);
            ApplyCameraTransform();

            Shader litShader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default");

            for (int r = 0; r < _height; r++)
            {
                for (int c = 0; c < _width; c++)
                {
                    int idx = r * _width + c;
                    BattleCell cell = sim.Map.Cells[idx];
                    _lastCellStates[idx] = cell.Collision;
                    Vector3 tilePos = CellToWorld(r, c, 0f);

                    // Ground tile
                    Color groundColor = ((r + c) & 1) == 0
                        ? new Color(0.76f, 0.78f, 0.72f)
                        : new Color(0.68f, 0.71f, 0.64f);
                    var ground = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    ground.name = $"Ground_{r}_{c}";
                    ground.transform.SetParent(_arenaRoot, false);
                    ground.transform.position = tilePos;
                    ground.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                    ground.transform.localScale = new Vector3(TileSize, TileSize, 1f);
                    Destroy(ground.GetComponent<Collider>());
                    var gMat = new Material(litShader);
                    gMat.mainTexture = _art.GetMapElementTexture(cell.VisualGroundElementId, groundColor);
                    ground.GetComponent<MeshRenderer>().sharedMaterial = gMat;

                    // 3D obstacle block (Solid or Breakable)
                    if (cell.Collision != CellCollision.Open)
                    {
                        bool breakable = cell.Collision == CellCollision.Breakable;
                        var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        block.name = $"Block_{r}_{c}";
                        block.transform.SetParent(_arenaRoot, false);
                        float h = breakable ? 0.72f : 0.96f;
                        block.transform.position = CellToWorld(r, c, h * 0.5f);
                        block.transform.localScale = new Vector3(0.94f, h, 0.94f);
                        Destroy(block.GetComponent<Collider>());

                        Color fallback = breakable
                            ? new Color(0.78f, 0.54f, 0.28f)
                            : new Color(0.36f, 0.42f, 0.50f);
                        var bMat = new Material(litShader);
                        int visId = cell.VisualSceneElementId > 0 ? cell.VisualSceneElementId : (int)cell.MapElementId;
                        bMat.mainTexture = _art.GetMapElementTexture(visId, fallback);
                        block.GetComponent<MeshRenderer>().sharedMaterial = bMat;

                        // Also mount front-facing 2D sprite card for painted Daoist/original tile art
                        if (visId > 0)
                        {
                            var cardObj = new GameObject("ArtCard");
                            cardObj.transform.SetParent(block.transform, false);
                            cardObj.transform.localPosition = new Vector3(0f, 0.52f, 0f);
                            cardObj.transform.localScale = new Vector3(1.1f, 1.1f / h, 1.1f);
                            var sr = cardObj.AddComponent<SpriteRenderer>();
                            sr.sprite = _art.GetSprite($"maps/elem_{visId}", fallback);
                            cardObj.AddComponent<BillboardSprite>();
                        }

                        _blockObjects[idx] = block;
                    }
                }
            }

            // Create character billboard visuals
            foreach (var actor in sim.Actors)
            {
                _actorVisuals[actor.PlayerId] = CreateActorVisual(actor);
            }
        }

        private ActorVisual CreateActorVisual(BattleActor actor)
        {
            var root = new GameObject($"Actor_{actor.PlayerId}_T{actor.TeamId}");
            root.transform.SetParent(_dynamicRoot, false);

            Color teamColor = TeamColor(actor.TeamId);

            // Ground team ring indicator
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "TeamRing";
            ring.transform.SetParent(root.transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            ring.transform.localScale = new Vector3(0.72f, 0.02f, 0.72f);
            Destroy(ring.GetComponent<Collider>());
            var ringMat = new Material(Shader.Find("Standard") ?? Shader.Find("Sprites/Default"));
            ringMat.color = actor.IsLocal ? Color.Lerp(teamColor, Color.white, 0.25f) : teamColor;
            ring.GetComponent<MeshRenderer>().sharedMaterial = ringMat;

            // 2D paper-sprite billboard character
            var spriteObj = new GameObject("Sprite");
            spriteObj.transform.SetParent(root.transform, false);
            spriteObj.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            spriteObj.transform.localScale = new Vector3(1.15f, 1.15f, 1.15f);
            var sr = spriteObj.AddComponent<SpriteRenderer>();
            sr.sprite = _art.GetCharacterSprite(actor.RoleId, "S", teamColor);
            sr.sortingOrder = 10;
            spriteObj.AddComponent<BillboardSprite>();

            // Trapped bubble overlay
            var trap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            trap.name = "TrappedBubble";
            trap.transform.SetParent(root.transform, false);
            trap.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            trap.transform.localScale = new Vector3(1.05f, 1.05f, 1.05f);
            Destroy(trap.GetComponent<Collider>());
            var trapMat = new Material(Shader.Find("Sprites/Default"));
            trapMat.color = new Color(0.35f, 0.78f, 1f, 0.48f);
            trap.GetComponent<MeshRenderer>().sharedMaterial = trapMat;
            trap.SetActive(false);

            // Shield invulnerability aura ring
            var shield = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shield.name = "ShieldAura";
            shield.transform.SetParent(root.transform, false);
            shield.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            shield.transform.localScale = new Vector3(0.96f, 0.42f, 0.96f);
            Destroy(shield.GetComponent<Collider>());
            var shieldMat = new Material(Shader.Find("Sprites/Default"));
            shieldMat.color = new Color(0.95f, 0.78f, 0.28f, 0.35f);
            shield.GetComponent<MeshRenderer>().sharedMaterial = shieldMat;
            shield.SetActive(false);

            // Overhead emote indicator
            var emote = new GameObject("EmoteBadge");
            emote.transform.SetParent(root.transform, false);
            emote.transform.localPosition = new Vector3(0f, 1.45f, 0f);
            emote.transform.localScale = new Vector3(0.55f, 0.55f, 0.55f);
            var emoteSr = emote.AddComponent<SpriteRenderer>();
            emoteSr.sprite = _art.GetSprite("effects/emote", new Color(1f, 0.88f, 0.32f), circleMask: true);
            emoteSr.sortingOrder = 20;
            emote.AddComponent<BillboardSprite>();
            emote.SetActive(false);

            // Bun carrier halo (for Capture-the-Bun objective maps)
            var bun = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bun.name = "BunHalo";
            bun.transform.SetParent(root.transform, false);
            bun.transform.localPosition = new Vector3(0f, 1.38f, 0f);
            bun.transform.localScale = new Vector3(0.45f, 0.38f, 0.45f);
            Destroy(bun.GetComponent<Collider>());
            var bunMat = new Material(Shader.Find("Standard") ?? Shader.Find("Sprites/Default"));
            bunMat.color = new Color(0.98f, 0.86f, 0.45f, 1f);
            bun.GetComponent<MeshRenderer>().sharedMaterial = bunMat;
            bun.SetActive(false);

            return new ActorVisual
            {
                Root = root,
                SpriteTransform = spriteObj.transform,
                Body = sr,
                TrappedBubble = trap,
                ShieldAura = shield,
                EmoteBadge = emote,
                BunHalo = bun,
                TeamRing = ring,
                RoleId = actor.RoleId
            };
        }

        private BossVisual CreateBossVisual(BattleBossEntity boss)
        {
            var root = new GameObject($"Boss_{boss.BossId}");
            root.transform.SetParent(_dynamicRoot, false);

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.transform.SetParent(root.transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            ring.transform.localScale = new Vector3(1.1f, 0.03f, 1.1f);
            Destroy(ring.GetComponent<Collider>());
            var rMat = new Material(Shader.Find("Standard") ?? Shader.Find("Sprites/Default"));
            rMat.color = new Color(0.88f, 0.18f, 0.16f, 1f);
            ring.GetComponent<MeshRenderer>().sharedMaterial = rMat;

            var spObj = new GameObject("BossSprite");
            spObj.transform.SetParent(root.transform, false);
            spObj.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            spObj.transform.localScale = new Vector3(1.55f, 1.55f, 1.55f);
            var sr = spObj.AddComponent<SpriteRenderer>();
            sr.sprite = _art.GetCharacterSprite(boss.RoleId, "S", new Color(0.92f, 0.25f, 0.22f));
            sr.color = new Color(1f, 0.85f, 0.85f, 1f);
            sr.sortingOrder = 12;
            spObj.AddComponent<BillboardSprite>();

            // Floating HP bar
            var hpBg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hpBg.transform.SetParent(root.transform, false);
            hpBg.transform.localPosition = new Vector3(0f, 1.72f, 0f);
            hpBg.transform.localScale = new Vector3(1.1f, 0.11f, 0.06f);
            Destroy(hpBg.GetComponent<Collider>());
            var bgMat = new Material(Shader.Find("Sprites/Default"));
            bgMat.color = new Color(0.12f, 0.12f, 0.14f, 0.9f);
            hpBg.GetComponent<MeshRenderer>().sharedMaterial = bgMat;

            var hpFill = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hpFill.transform.SetParent(root.transform, false);
            hpFill.transform.localPosition = new Vector3(0f, 1.72f, -0.04f);
            hpFill.transform.localScale = new Vector3(1.04f, 0.08f, 0.06f);
            Destroy(hpFill.GetComponent<Collider>());
            var fMat = new Material(Shader.Find("Sprites/Default"));
            fMat.color = new Color(0.92f, 0.22f, 0.20f, 1f);
            hpFill.GetComponent<MeshRenderer>().sharedMaterial = fMat;

            return new BossVisual { Root = root, HpFill = hpFill.transform };
        }

        private void ApplyCameraTransform()
        {
            if (_camera == null) return;
            Vector3 offset = (_baseCameraPos - _cameraLookTarget) * Mathf.Clamp(CameraZoom, 0.7f, 1.35f);
            Vector3 pos = _cameraLookTarget + offset;
            if (_shakeTimer > 0f)
            {
                _shakeTimer -= Time.deltaTime;
                pos += UnityEngine.Random.insideUnitSphere * _shakeIntensity;
                if (_shakeTimer <= 0f) _shakeIntensity = 0f;
            }
            _camera.transform.position = pos;
            _camera.transform.LookAt(_cameraLookTarget);
        }

        public void SyncView(Rule1BattleSimulation sim)
        {
            if (sim == null || sim.Map == null || _arenaRoot == null) return;

            // 1. Update destroyed or pushed blocks
            Shader litShader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default");
            for (int i = 0; i < _lastCellStates.Length; i++)
            {
                if (_lastCellStates[i] != sim.Map.Cells[i].Collision)
                {
                    _lastCellStates[i] = sim.Map.Cells[i].Collision;
                    if (_blockObjects[i] != null && sim.Map.Cells[i].Collision == CellCollision.Open)
                    {
                        Destroy(_blockObjects[i]);
                        _blockObjects[i] = null;
                    }
                    else if (_blockObjects[i] == null && sim.Map.Cells[i].Collision != CellCollision.Open)
                    {
                        int r = i / _width, c = i % _width;
                        var cell = sim.Map.Cells[i];
                        bool breakable = cell.Collision == CellCollision.Breakable;
                        var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        block.name = $"Block_{r}_{c}";
                        block.transform.SetParent(_arenaRoot, false);
                        float h = breakable ? 0.72f : 0.96f;
                        block.transform.position = CellToWorld(r, c, h * 0.5f);
                        block.transform.localScale = new Vector3(0.94f, h, 0.94f);
                        Destroy(block.GetComponent<Collider>());
                        Color fallback = breakable ? new Color(0.78f, 0.54f, 0.28f) : new Color(0.36f, 0.42f, 0.50f);
                        var bMat = new Material(litShader);
                        int visId = cell.VisualSceneElementId > 0 ? cell.VisualSceneElementId : (int)cell.MapElementId;
                        bMat.mainTexture = _art.GetMapElementTexture(visId, fallback);
                        block.GetComponent<MeshRenderer>().sharedMaterial = bMat;
                        _blockObjects[i] = block;
                    }
                }
            }

            // 2. Update characters
            foreach (var actor in sim.Actors)
            {
                if (!_actorVisuals.TryGetValue(actor.PlayerId, out var vis))
                {
                    vis = CreateActorVisual(actor);
                    _actorVisuals[actor.PlayerId] = vis;
                }
                if (actor.State == ActorLifeState.Dead)
                {
                    vis.Root.SetActive(false);
                    continue;
                }
                vis.Root.SetActive(true);
                vis.Root.transform.position = PixelToWorld(actor.PosX, actor.PosY, 0f);
                vis.TrappedBubble.SetActive(actor.State == ActorLifeState.Trapped);
                if (vis.ShieldAura != null)
                    vis.ShieldAura.SetActive(actor.IsInvulnerable(sim.ElapsedMs));
                if (vis.EmoteBadge != null)
                    vis.EmoteBadge.SetActive(actor.ActiveEmotionId != 0 && sim.ElapsedMs < actor.EmotionExpireAtMs);
                if (vis.BunHalo != null)
                    vis.BunHalo.SetActive(sim.BunCarrierPlayerId == actor.PlayerId);
                if (vis.SpriteTransform != null)
                {
                    float walkBob = actor.Moving ? Mathf.Abs(Mathf.Sin(Time.time * 12f)) * 0.10f : 0f;
                    vis.SpriteTransform.localPosition = new Vector3(0f, 0.08f + walkBob, 0f);
                }

                if (vis.LastDir != actor.Facing)
                {
                    vis.LastDir = actor.Facing;
                    vis.Body.sprite = _art.GetCharacterSprite(vis.RoleId, DirectionSuffix(actor.Facing), TeamColor(actor.TeamId));
                }
            }

            // 3. Sync bombs
            EnsurePool(_pooledBombs, sim.Bombs.Count, "Bomb", () => CreateBillboardEntity("Bomb", "effects/bubble", new Color(0.18f, 0.68f, 1f), 0.95f, 5));
            for (int i = 0; i < _pooledBombs.Count; i++)
            {
                bool active = i < sim.Bombs.Count;
                _pooledBombs[i].SetActive(active);
                if (active)
                {
                    var b = sim.Bombs[i];
                    float pulse = 1.0f + 0.12f * Mathf.Sin(Time.time * 9f);
                    _pooledBombs[i].transform.position = CellToWorld(b.Cell.Row, b.Cell.Col, 0.12f);
                    _pooledBombs[i].transform.localScale = new Vector3(0.9f * pulse, 0.9f / pulse, 0.9f);
                }
            }

            // 4. Sync flames
            EnsurePool(_pooledFlames, sim.Flames.Count, "Flame", () => CreateBillboardEntity("Flame", "effects/flame", new Color(1f, 0.55f, 0.15f), 1.05f, 8));
            for (int i = 0; i < _pooledFlames.Count; i++)
            {
                bool active = i < sim.Flames.Count;
                _pooledFlames[i].SetActive(active);
                if (active)
                {
                    var f = sim.Flames[i];
                    _pooledFlames[i].transform.position = CellToWorld(f.Cell.Row, f.Cell.Col, 0.15f);
                }
            }

            // 5. Sync visible pickups
            int visiblePickups = 0;
            for (int i = 0; i < sim.Pickups.Count; i++)
                if (!sim.Pickups[i].Hidden && !sim.Pickups[i].Consumed) visiblePickups++;
            EnsurePool(_pooledPickups, visiblePickups, "Pickup", () => CreateBillboardEntity("Pickup", "pickups/scene_1", new Color(0.98f, 0.85f, 0.2f), 0.72f, 4));
            int pIdx = 0;
            for (int i = 0; i < sim.Pickups.Count; i++)
            {
                var p = sim.Pickups[i];
                if (p.Hidden || p.Consumed) continue;
                var obj = _pooledPickups[pIdx++];
                obj.SetActive(true);
                float bob = 0.15f + 0.08f * Mathf.Sin(Time.time * 4f + pIdx);
                obj.transform.position = CellToWorld(p.Cell.Row, p.Cell.Col, bob);
                var sr = obj.GetComponent<SpriteRenderer>();
                sr.sprite = _art.GetSprite($"pickups/scene_{p.SceneId}", PickupColor(p.SceneId), circleMask: true);
            }
            for (int i = pIdx; i < _pooledPickups.Count; i++) _pooledPickups[i].SetActive(false);

            // 6. Sync Boss / PVE NPC entities
            foreach (var boss in sim.Bosses)
            {
                if (!_bossVisuals.TryGetValue(boss.BossId, out var bVis))
                {
                    bVis = CreateBossVisual(boss);
                    _bossVisuals[boss.BossId] = bVis;
                }
                bVis.Root.SetActive(boss.Alive);
                if (!boss.Alive) continue;
                float bob = 0.08f * Mathf.Sin(Time.time * 3.5f + boss.BossId);
                bVis.Root.transform.position = PixelToWorld(boss.PosX, boss.PosY, bob);
                float ratio = boss.MaxHp > 0 ? Mathf.Clamp01((float)boss.Hp / boss.MaxHp) : 0f;
                bVis.HpFill.localScale = new Vector3(1.04f * ratio, 0.08f, 0.06f);
            }

            ApplyCameraTransform();
        }

        private GameObject CreateBillboardEntity(string name, string artKey, Color fallbackColor, float scale, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_dynamicRoot, false);
            go.transform.localScale = Vector3.one * scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _art.GetSprite(artKey, fallbackColor, circleMask: true);
            sr.sortingOrder = sortingOrder;
            go.AddComponent<BillboardSprite>();
            return go;
        }

        private static void EnsurePool(List<GameObject> pool, int needed, string prefix, Func<GameObject> factory)
        {
            while (pool.Count < needed) pool.Add(factory());
        }

        public void ClearArena()
        {
            if (_arenaRoot != null)
            {
                Destroy(_arenaRoot.gameObject);
                _arenaRoot = null;
                _dynamicRoot = null;
            }
            _actorVisuals.Clear();
            _bossVisuals.Clear();
            _pooledBombs.Clear();
            _pooledFlames.Clear();
            _pooledPickups.Clear();
            _blockObjects = null;
            _lastCellStates = null;
        }

        private static Vector3 CellToWorld(int row, int col, float y) =>
            new Vector3(col * TileSize, y, -row * TileSize);

        private static Vector3 PixelToWorld(int px, int py, float y) =>
            new Vector3((px - 20f) / Rule1BattleSimulation.CellSizePixels * TileSize,
                        y,
                        -((py - 20f) / Rule1BattleSimulation.CellSizePixels * TileSize));

        private static string DirectionSuffix(BattleDirection dir)
        {
            switch (dir)
            {
                case BattleDirection.Up: return "N";
                case BattleDirection.Left: return "W";
                case BattleDirection.Right: return "E";
                default: return "S";
            }
        }

        private static Color TeamColor(byte teamId)
        {
            switch (teamId)
            {
                case 1: return new Color(0.95f, 0.30f, 0.28f);
                case 2: return new Color(0.24f, 0.56f, 0.98f);
                case 3: return new Color(0.25f, 0.82f, 0.42f);
                case 4: return new Color(0.96f, 0.78f, 0.20f);
                default: return new Color(0.75f, 0.45f, 0.95f);
            }
        }

        private static Color PickupColor(uint sceneId)
        {
            switch (sceneId)
            {
                case 1: case 6: return new Color(0.25f, 0.75f, 1.0f);
                case 2: case 7: return new Color(1.0f, 0.45f, 0.20f);
                case 3: case 8: case 47: return new Color(0.35f, 0.95f, 0.45f);
                default: return new Color(0.98f, 0.82f, 0.25f);
            }
        }
    }
}
