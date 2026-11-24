using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Qqtang.Core.Battle;
using Qqtang.Core.Network;
using Qqtang.Core.Protocol;
using Qqtang.Core.State;
using Qqtang.UnityClient.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Qqtang.UnityClient
{
    public enum ClientScreenState
    {
        Login,
        MainShell,
        Room,
        Battle,
        Settlement
    }

    /// <summary>
    /// Complete Unity 3D + Daoist UGUI client coordinator wired directly to the unmodified Go QQTang server.
    /// Manages Login, Lobby, Room, Inventory, Shop, Forge/Combine/BreakEgg, Pets, Friends/Kin/Marriage, and Rule-1 3D Battle.
    /// </summary>
    public sealed class QqtangGameApp : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap()
        {
            if (FindAnyObjectByType<QqtangGameApp>() != null) return;
            var host = new GameObject("QqtangGameApp");
            DontDestroyOnLoad(host);
            host.AddComponent<QqtangGameApp>();
        }

        private QqtangSessionClient _session;
        private ClientStateStore _store;
        private StaticCatalogs _catalogs;
        private ArtAssetResolver _art;
        private AudioPlayer _audio;
        private BattleArena3DView _arenaView;
        private Rule1BattleSimulation _sim;

        private Dictionary<uint, MapElementDef> _mapElements;
        private Dictionary<uint, string> _mapFileById;

        // UI
        private Canvas _canvas;
        private LoginPanelView _loginView;
        private LobbyAndNavView _lobbyView;
        private RoomPanelView _roomView;
        private InventoryShopCraftView _ecoView;
        private PetAndSocialView _socialView;
        private BattleHudAndSettlementView _battleView;
        private RectTransform _toastPanel;
        private Text _toastText;
        private float _toastTimer;

        private ClientScreenState _screen = ClientScreenState.Login;
        private MainTab _activeTab = MainTab.Lobby;
        private int _lastRenderedStateVersion = -1;
        private uint _shopCatalogVersion = 848;
        private int _chatBgCursor;

        // Battle state
        private readonly List<LegacyUdpMulticastTarget> _battlePeers = new List<LegacyUdpMulticastTarget>();
        private readonly List<(ushort playerId, LocalBattleAiBot bot)> _offlineBots = new List<(ushort, LocalBattleAiBot)>();
        private bool _isOfflineSolo;
        private ushort _activeBattleMapId = 201;
        private float _simAccumulatorMs;
        private float _helloTimer;
        private float _lobbyPollTimer;

        private void Awake()
        {
            _session = new QqtangSessionClient();
            _store = new ClientStateStore();
            _catalogs = new StaticCatalogs();
            _art = new ArtAssetResolver();
            _audio = gameObject.AddComponent<AudioPlayer>();
            _arenaView = gameObject.AddComponent<BattleArena3DView>();
            _arenaView.Initialize(_art);

            LoadCatalogsAndMaps();
            BuildUi();
            BindStoreEvents();
            SetScreen(ClientScreenState.Login);
        }

        private void OnDestroy()
        {
            _session?.Dispose();
        }

        private void LoadCatalogsAndMaps()
        {
            string streaming = Application.streamingAssetsPath;
            string repoServer = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "server"));
            string localMapsDir = Path.Combine(streaming, "LocalMaps");
            _catalogs.LoadFromDirectories(
                Path.Combine(streaming, "Catalogs"),
                localMapsDir,
                repoServer,
                @"C:\Users\luyua\Downloads\QQTang-Local\runtime\client-patched");

            string elemPy = Path.Combine(localMapsDir, "mapElem.py");
            string descPy = Path.Combine(localMapsDir, "mapDesc.py");
            if (File.Exists(elemPy))
            {
                try { _mapElements = MapDataLoader.LoadMapElementsPy(elemPy); } catch { }
            }
            if (File.Exists(descPy))
            {
                try { _mapFileById = MapDataLoader.LoadMapFilenamesFromMapDescPy(descPy); }
                catch { _mapFileById = new Dictionary<uint, string>(); }
            }
        }

        private void BuildUi()
        {
            _canvas = UiFactory.CreateRootCanvas("QqtangDaoistCanvas");
            DontDestroyOnLoad(_canvas.gameObject);

            var ctx = new UiContext
            {
                App = this,
                Session = _session,
                Store = _store,
                Catalogs = _catalogs,
                Art = _art
            };

            _loginView = new LoginPanelView(_canvas.transform, ctx);
            _lobbyView = new LobbyAndNavView(_canvas.transform, ctx);
            _ecoView = new InventoryShopCraftView(_lobbyView.ContentArea, ctx);
            _socialView = new PetAndSocialView(_lobbyView.ContentArea, ctx);
            _roomView = new RoomPanelView(_canvas.transform, ctx);
            _battleView = new BattleHudAndSettlementView(_canvas.transform, ctx);

            _lobbyView.OnTabSelected = SelectMainTab;

            // Floating Toast Banner (top-center)
            _toastPanel = UiFactory.CreatePanel(_canvas.transform, "ToastBanner", new Color(0.10f, 0.16f, 0.21f, 0.94f), UiFactory.AccentGold);
            _toastPanel.anchorMin = new Vector2(0.5f, 1f);
            _toastPanel.anchorMax = new Vector2(0.5f, 1f);
            _toastPanel.pivot = new Vector2(0.5f, 1f);
            _toastPanel.anchoredPosition = new Vector2(0f, -68f);
            _toastPanel.sizeDelta = new Vector2(560f, 38f);
            _toastText = UiFactory.CreateText(_toastPanel, "", 14, UiFactory.AccentGold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.Stretch(_toastText.rectTransform, 10, 4, 10, 4);
            _toastPanel.gameObject.SetActive(false);
        }

        private void BindStoreEvents()
        {
            _store.OnToastRequested += ShowToast;
            _store.OnGameBeginReceived += StartBattle;
            _store.OnNextMapStageReceived += StartBattle;
            _store.OnGameOverReceived += over =>
            {
                PlaySettlementAudio(over, _store.PlayerId);
                SetScreen(ClientScreenState.Settlement);
                _battleView.ShowSettlement(over, isOfflineSolo: false);
            };
            _store.OnBattleGameEventReceived += ev =>
            {
                if (_sim != null) _sim.ApplyRemoteMessage(0, ev.Schema, ev.Body);
            };
        }

        private void PlaySettlementAudio(GameOverData over, ushort localPid)
        {
            var res = over?.Results.Find(r => r.PlayerId == localPid);
            if (res == null || res.Result == GameResultCode.Win) _audio.PlaySfx("victory");
            else if (res.Result == GameResultCode.Draw) _audio.PlaySfx("draw");
            else _audio.PlaySfx("defeat");
        }

        private void SetScreen(ClientScreenState screen)
        {
            _screen = screen;
            _loginView.SetVisible(screen == ClientScreenState.Login);
            _lobbyView.SetShellVisible(screen == ClientScreenState.MainShell);
            _roomView.SetVisible(screen == ClientScreenState.Room);
            _battleView.SetHudVisible(screen == ClientScreenState.Battle);
            if (screen != ClientScreenState.Settlement) _battleView.HideSettlement();

            if (screen == ClientScreenState.Battle)
            {
                _audio.PlayBgm("music-battle");
            }
            else if (screen == ClientScreenState.MainShell || screen == ClientScreenState.Room || screen == ClientScreenState.Login)
            {
                _audio.PlayBgm("music-lobby");
            }

            if (screen == ClientScreenState.MainShell)
            {
                _arenaView.ClearArena();
                SelectMainTab(_activeTab);
            }
            else if (screen == ClientScreenState.Room)
            {
                _arenaView.ClearArena();
                _roomView.Refresh();
            }
            else if (screen == ClientScreenState.Login)
            {
                _arenaView.ClearArena();
            }
        }

        private void SelectMainTab(MainTab tab)
        {
            _activeTab = tab;
            _lobbyView.SetActiveTabHighlight(tab);
            _ecoView.SetActiveView(tab);
            _socialView.SetActiveView(tab);

            if (tab == MainTab.Lobby) RequestRefreshLobby();
            else if (tab == MainTab.Pets) RequestRefreshPets();
            else if (tab == MainTab.Social) RequestRefreshSocial();
            else if (tab == MainTab.Shop) _ecoView.RefreshShop();
            else if (tab == MainTab.Inventory) _ecoView.RefreshInventory();
        }

        private void ShowToast(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            _toastText.text = message;
            _toastPanel.gameObject.SetActive(true);
            _toastTimer = 3.2f;
        }

        private void Update()
        {
            DrainNetworkQueues();

            if (_toastTimer > 0f)
            {
                _toastTimer -= Time.deltaTime;
                if (_toastTimer <= 0f) _toastPanel.gameObject.SetActive(false);
            }

            if (_session.IsConnected && _screen != ClientScreenState.Login)
            {
                _helloTimer += Time.deltaTime;
                if (_helloTimer >= 15f)
                {
                    _helloTimer = 0f;
                    _ = _session.SendHelloAsync();
                }

                if (_screen == ClientScreenState.MainShell && _activeTab == MainTab.Lobby)
                {
                    _lobbyPollTimer += Time.deltaTime;
                    if (_lobbyPollTimer >= 6f)
                    {
                        _lobbyPollTimer = 0f;
                        RequestRefreshLobby();
                    }
                }
            }

            // Detect kicked-out transition back to Lobby
            if (_screen == ClientScreenState.Room && !_store.InRoom)
                SetScreen(ClientScreenState.MainShell);

            // Re-render active UI panel only when store version advances
            if (_lastRenderedStateVersion != _store.StateVersion)
            {
                _lastRenderedStateVersion = _store.StateVersion;
                RefreshVisibleScreen();
            }

            if (_screen == ClientScreenState.Battle && _sim != null)
            {
                StepBattleFrame();
            }
        }

        private void RefreshVisibleScreen()
        {
            if (_store.ActiveTreasureRoom == null && _battleView.TreasureModal.gameObject.activeSelf)
                _battleView.HideTreasureRoom();
            if (_screen == ClientScreenState.MainShell)
            {
                _lobbyView.Refresh();
                _ecoView.RefreshAllVisible();
                _socialView.RefreshAllVisible();
            }
            else if (_screen == ClientScreenState.Room)
            {
                _roomView.Refresh();
            }
        }

        private void DrainNetworkQueues()
        {
            while (_session.TryDequeueDiagnostic(out string diag))
                ShowToast(diag);

            while (_session.TryDequeueTcpPacket(out var pkt))
                _store.ApplyServerPacket(pkt);

            while (_session.TryDequeueUdpBatch(out var batch))
            {
                if (_screen == ClientScreenState.Battle && _sim != null && batch.GameId == _sim.GameId)
                {
                    foreach (var entry in batch.Entries)
                    {
                        foreach (var msg in entry.Package.Messages)
                            _sim.ApplyRemoteMessage(entry.Package.PlayerId, msg.DataId, msg.Data);
                    }
                }
            }
        }

        private void StepBattleFrame()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                _battleView.TogglePause();
            if (!_isOfflineSolo && Input.GetKeyDown(KeyCode.Return))
                _battleView.ToggleBattleChat();

            bool blockControls = _battleView.IsPauseOpen || _battleView.IsChatInputFocused;
            BattleDirection moveInput = blockControls ? BattleDirection.None : ReadMoveInput();
            bool bombPressed = !blockControls && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.J));

            if (!blockControls)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1)) TriggerBattleWorldItem(63);
                if (Input.GetKeyDown(KeyCode.Alpha2)) TriggerBattleWorldItem(41);
                if (Input.GetKeyDown(KeyCode.Alpha3)) TriggerBattleWorldItem(64);
                if (Input.GetKeyDown(KeyCode.E)) TriggerBattleEmotion(1);
            }

            _simAccumulatorMs += Time.deltaTime * 1000f;
            const uint fixedStepMs = 25;
            bool bombQueued = bombPressed;
            while (_simAccumulatorMs >= fixedStepMs)
            {
                _simAccumulatorMs -= fixedStepMs;
                int prevBombs = _sim.Bombs.Count;
                int prevFlames = _sim.Flames.Count;
                int prevConsumed = CountConsumedPickups(_sim);
                var prevLocalState = _sim.LocalActor != null ? _sim.LocalActor.State : ActorLifeState.Dead;

                if (_isOfflineSolo)
                {
                    foreach (var entry in _offlineBots)
                    {
                        var (bMove, bBomb) = entry.bot.Decide(_sim);
                        _sim.StepOfflineBot(entry.playerId, fixedStepMs, bMove, bBomb);
                    }
                }
                _sim.Step(fixedStepMs, moveInput, bombQueued);

                if (_sim.Bombs.Count > prevBombs) _audio.PlaySfx("bubble-place", 0.55f);
                if (_sim.Flames.Count > prevFlames)
                {
                    _audio.PlaySfx("bubble-burst", 0.7f);
                    _arenaView.TriggerShake(0.14f, 0.16f);
                }
                if (CountConsumedPickups(_sim) > prevConsumed) _audio.PlaySfx("pickup", 0.65f);
                if (_sim.LocalActor != null && prevLocalState == ActorLifeState.Active && _sim.LocalActor.State == ActorLifeState.Trapped)
                    _audio.PlaySfx("trapped", 0.75f);

                bombQueued = false;
                if (!_isOfflineSolo) FlushOutgoingBattleEvents();

                if (_isOfflineSolo && _sim.IsFinished && _sim.OfflineSettlement != null)
                {
                    PlaySettlementAudio(_sim.OfflineSettlement, 1);
                    SetScreen(ClientScreenState.Settlement);
                    _battleView.ShowSettlement(_sim.OfflineSettlement, isOfflineSolo: true);
                    return;
                }
            }

            _arenaView.SyncView(_sim);
            _battleView.UpdateHud(_sim, _activeBattleMapId);
        }

        private static int CountConsumedPickups(Rule1BattleSimulation sim)
        {
            int c = 0;
            for (int i = 0; i < sim.Pickups.Count; i++) if (sim.Pickups[i].Consumed) c++;
            return c;
        }

        private static BattleDirection ReadMoveInput()
        {
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) return BattleDirection.Up;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) return BattleDirection.Down;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) return BattleDirection.Left;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) return BattleDirection.Right;
            return BattleDirection.None;
        }

        private void FlushOutgoingBattleEvents()
        {
            if (_sim.OutgoingPeerEvents.Count > 0 && _battlePeers.Count > 0)
            {
                var copy = new List<(uint, byte[])>(_sim.OutgoingPeerEvents);
                bool mirrorTcp = false;
                foreach (var ev in copy)
                    if (ev.Item1 != GameEventSchemas.PlayerMove) { mirrorTcp = true; break; }
                _ = _session.SendPeerGameplayAsync(_sim.GameId, _sim.ElapsedMs, _battlePeers, copy, mirrorTcp);
            }
            if (_sim.OutgoingTcpEvents.Count > 0)
            {
                foreach (var ev in _sim.OutgoingTcpEvents)
                    _ = _session.SendGameEventTcpAsync(_store.RoomId, ev.schemaId, ev.body);
            }
        }

        private void StartBattle(GameBeginData begin)
        {
            _isOfflineSolo = false;
            _offlineBots.Clear();
            _activeBattleMapId = (ushort)begin.MapId;
            _battlePeers.Clear();
            foreach (var member in _store.RoomMembers)
            {
                if (member.PlayerId != _session.PlayerId)
                    _battlePeers.Add(new LegacyUdpMulticastTarget { PlayerId = member.PlayerId, Uin = member.Uin });
            }
            foreach (var peer in _battlePeers)
                _ = _session.SendUdpOkAsync(peer.PlayerId, peer.Uin, true);

            LoadedMapData map = LoadBattleMap(begin.MapId);
            _sim = new Rule1BattleSimulation();
            bool freeRule = (_store.RoomFlag & (byte)RoomPropertyFlag.FreeRule) != 0;
            _sim.Initialize(begin, map, _session.PlayerId, freeRule);
            foreach (var m in _store.RoomMembers)
                _sim.RegisterParticipantUin(m.Uin, m.PlayerId);
            _simAccumulatorMs = 0f;

            _arenaView.BuildArena(_sim);
            SetScreen(ClientScreenState.Battle);
            ShowToast($"对局开始！地图: {_catalogs.GetMapName((ushort)begin.MapId)}");
        }

        public void StartOfflineSoloMatch(byte playerRoleId, int botCount)
        {
            _audio.PlaySfx("ui-click", 0.6f);
            _isOfflineSolo = true;
            _battlePeers.Clear();
            _offlineBots.Clear();
            _activeBattleMapId = 201;

            int bots = Mathf.Clamp(botCount, 1, 3);
            int seed = Environment.TickCount;
            var begin = new GameBeginData
            {
                GameId = (uint)(seed & 0x7FFFFFFF),
                MapId = _activeBattleMapId,
                SpawnSeed = (uint)seed,
                ItemSeed = (uint)(seed ^ 0x5A5A5A5A),
                GameTimeMs = 180_000,
                ArbitratorPlayerId = 1
            };
            begin.Items.Add(new GameItemTypeData { ItemId = 1, Quantity = 6 });  // bomb capacity
            begin.Items.Add(new GameItemTypeData { ItemId = 2, Quantity = 6 });  // bomb power
            begin.Items.Add(new GameItemTypeData { ItemId = 3, Quantity = 4 });  // speed
            begin.Items.Add(new GameItemTypeData { ItemId = 63, Quantity = 2 }); // needle
            begin.Items.Add(new GameItemTypeData { ItemId = 41, Quantity = 2 }); // shield
            begin.Players.Add(new PlayerGameInfoData { PlayerId = 1, RoleId = playerRoleId, TeamId = 1 });
            for (int i = 0; i < bots; i++)
            {
                ushort botPid = (ushort)(i + 2);
                byte botRole = (byte)((playerRoleId + i) % 4 + 1);
                begin.Players.Add(new PlayerGameInfoData { PlayerId = botPid, RoleId = botRole, TeamId = (byte)(i + 2) });
                _offlineBots.Add((botPid, new LocalBattleAiBot(botPid, seed + i * 31)));
            }

            LoadedMapData map = LoadBattleMap(_activeBattleMapId);
            _sim = new Rule1BattleSimulation { IsOfflineSoloMode = true };
            _sim.Initialize(begin, map, localPlayerId: 1, usesFreeRule: false);
            _simAccumulatorMs = 0f;

            _arenaView.BuildArena(_sim);
            SetScreen(ClientScreenState.Battle);
            ShowToast($"离线演武开始（1v{bots} AI），无需启动服务端！");
        }

        public void ApplySettings(float master, float bgm, float sfx, bool muted, float zoom)
        {
            _audio.MasterVolume = master;
            _audio.BgmVolume = bgm;
            _audio.SfxVolume = sfx;
            _audio.Muted = muted;
            _audio.ApplyVolumeSettings();
            _arenaView.CameraZoom = zoom;
        }

        private LoadedMapData LoadBattleMap(uint mapId)
        {
            string localMapsDir = Path.Combine(Application.streamingAssetsPath, "LocalMaps");
            if (_mapElements != null && Directory.Exists(localMapsDir))
            {
                string fileName = null;
                if (_mapFileById != null && _mapFileById.TryGetValue(mapId, out var mapped)) fileName = mapped;
                else if (mapId == 201) fileName = "town01.map";
                if (!string.IsNullOrEmpty(fileName))
                {
                    string full = Path.Combine(localMapsDir, fileName);
                    if (File.Exists(full))
                    {
                        try { return MapDataLoader.ParseMapBytes(mapId, File.ReadAllBytes(full), _mapElements); }
                        catch { }
                    }
                }
            }
            return MapDataLoader.BuildFallbackArena(mapId);
        }

        // --- Public UI Action Handlers ---

        public void TriggerBattleWorldItem(uint itemId)
        {
            if (_sim == null) return;
            if (_sim.TryUseWorldItem(itemId))
            {
                if (itemId == 63) _audio.PlaySfx("needle", 0.75f);
                else if (itemId == 41 || itemId == 42 || itemId == 43) _audio.PlaySfx("shield", 0.75f);
                else _audio.PlaySfx("ui-click", 0.6f);
                if (!_isOfflineSolo) FlushOutgoingBattleEvents();
            }
            else
            {
                ShowToast(itemId == 63 ? "需要被困且持有针才能自救" : "当前无法使用该局内道具");
            }
        }

        public void TriggerBattleEmotion(uint emotionId)
        {
            if (_sim == null) return;
            _sim.TriggerLocalEmotion(_isOfflineSolo ? 0u : _store.Uin, emotionId);
            if (!_isOfflineSolo) FlushOutgoingBattleEvents();
        }

        public void ReturnToRoomAfterSettlement()
        {
            _audio.PlaySfx("ui-click", 0.5f);
            if (_isOfflineSolo)
            {
                _isOfflineSolo = false;
                _offlineBots.Clear();
                _sim = null;
                SetScreen(ClientScreenState.Login);
                return;
            }
            _store.ExitBattleToRoom();
            SetScreen(_store.InRoom ? ClientScreenState.Room : ClientScreenState.MainShell);
        }

        public async void RequestLogin(string host, int port, uint uin, string password, byte roleId)
        {
            _loginView.SetStatus($"正在连接 {host}:{port} ...", busy: true);
            try
            {
                var login = await _session.ConnectAndLoginAsync(host, port, uin, password, roleId);
                _store.ApplyLogin(login, uin, _session.Nickname, _session.Gender);
                _loginView.SetStatus("登录成功！", busy: false);
                SetScreen(ClientScreenState.MainShell);
                ShowToast($"欢迎回到灵葫堂，{_session.Nickname}！");
                RequestRefreshLobby();
                RequestRefreshPets();
                RequestRefreshSocial();
            }
            catch (Exception ex)
            {
                _loginView.SetStatus($"登录失败: {ex.Message}", busy: false);
            }
        }

        public async void RequestLogout()
        {
            await RunSafeAsync(async () =>
            {
                if (_store.InRoom) await _session.LeaveRoomAsync(_store.RoomId);
                await _session.LogoutAsync();
                _store.ResetAll();
                SetScreen(ClientScreenState.Login);
                _loginView.SetStatus("已安全注销。");
            });
        }

        public async void RequestRefreshLobby()
        {
            await RunSafeAsync(async () =>
            {
                var rooms = await _session.RefreshRoomListAsync();
                _store.ApplyRoomList(rooms);
                var players = await _session.RefreshPlayerListAsync();
                _store.ApplyPlayerList(players);
            }, silent: true);
        }

        public async void RequestRefreshMoney()
        {
            await RunSafeAsync(async () =>
            {
                var m = await _session.GetGameMoneyAsync();
                if (m.ResultId == 0) _store.UpdateMoney(m.Money);
                ShowToast($"当前糖币余额: {m.Money}");
            });
        }

        public async void RequestCreateRoom(string name, RoomPropertyFlag flag, RoomGameType gameType, string password)
        {
            await RunSafeAsync(async () =>
            {
                ushort rid = await _session.CreateRoomAsync(name, flag, gameType, password);
                var enter = await _session.EnterRoomAsync(rid, _session.RoleId, password);
                _store.ApplyEnterRoom(enter, name);
                if (gameType == RoomGameType.CompetitiveNoItem || gameType == RoomGameType.CompetitiveItem)
                    await _session.SelectMapAsync(rid, 201, gameType);
                SetScreen(ClientScreenState.Room);
            });
        }

        public async void RequestEnterRoom(ushort roomId, string roomName, string password)
        {
            await RunSafeAsync(async () =>
            {
                var enter = await _session.EnterRoomAsync(roomId, _session.RoleId, password);
                if (enter.ResultId != 0)
                {
                    ShowToast($"进入房间失败 (code={enter.ResultId})");
                    return;
                }
                _store.ApplyEnterRoom(enter, roomName);
                SetScreen(ClientScreenState.Room);
            });
        }

        public async void RequestQuickJoin(RoomGameType gameType)
        {
            await RunSafeAsync(async () =>
            {
                var join = await _session.QuickJoinRoomAsync(gameType);
                if (join.ResultId != 0)
                {
                    ShowToast("暂无可快速加入的等待房间，可新建一个房间。");
                    return;
                }
                _store.ApplyJoinRoom(join);
                SetScreen(ClientScreenState.Room);
            });
        }

        public async void RequestLeaveRoom()
        {
            if (_isOfflineSolo)
            {
                _isOfflineSolo = false;
                _offlineBots.Clear();
                _sim = null;
                SetScreen(ClientScreenState.Login);
                return;
            }
            await RunSafeAsync(async () =>
            {
                if (_store.InRoom) await _session.LeaveRoomAsync(_store.RoomId);
                _store.ClearRoom();
                SetScreen(ClientScreenState.MainShell);
                RequestRefreshLobby();
            });
        }

        public async void RequestSelectMap(ushort mapId)
        {
            await RunSafeAsync(async () =>
            {
                await _session.SelectMapAsync(_store.RoomId, mapId, _store.GameType);
                ShowToast($"已切换地图: {_catalogs.GetMapName(mapId)}");
            });
        }

        public async void RequestChangeRoomType(RoomGameType next)
        {
            await RunSafeAsync(async () =>
            {
                await _session.ChangeRoomTypeAsync(_store.RoomId, next);
            });
        }

        public async void RequestModifyRoom(string name, RoomPropertyFlag flag, string password)
        {
            await RunSafeAsync(async () =>
            {
                await _session.ModifyRoomAsync(_store.RoomId, name, flag, password, _store.GameType, true, !string.IsNullOrEmpty(password));
                ShowToast("房间设置已保存");
            });
        }

        public async void RequestSetSeatStatus(byte seatId, bool locked)
        {
            await RunSafeAsync(async () =>
            {
                await _session.SetSeatStatusAsync(_store.RoomId, seatId, locked);
            });
        }

        public async void RequestKickPlayer(ushort playerId, uint uin)
        {
            await RunSafeAsync(async () =>
            {
                ushort res = await _session.KickOffPlayerAsync(_store.RoomId, playerId, uin);
                if (res != 0) ShowToast("无法踢出该玩家（可能拥有防踢保护/紫钻）");
            });
        }

        public async void RequestChangeTeam(byte teamId)
        {
            await RunSafeAsync(async () => await _session.ChangeTeamAsync(_store.RoomId, teamId));
        }

        public async void RequestChangeRole(byte roleId)
        {
            await RunSafeAsync(async () => await _session.ChangeRoleAsync(_store.RoomId, roleId));
        }

        public async void RequestSetReady(bool ready)
        {
            await RunSafeAsync(async () => await _session.SetReadyAsync(_store.RoomId, ready));
        }

        public async void RequestToggleSpecialCard(ushort itemId)
        {
            await RunSafeAsync(async () =>
            {
                byte next = (byte)(_store.HasActiveItem(itemId) ? 0 : 1);
                await _session.SetItemStatusAsync(itemId, next);
                _store.SetInventoryItemStatusLocal(itemId, next, 0);
                ShowToast(next == 1 ? $"已启用 {_catalogs.GetItemName(itemId)}" : $"已停用 {_catalogs.GetItemName(itemId)}");
            });
        }

        public async void RequestCycleChatBackground()
        {
            await RunSafeAsync(async () =>
            {
                var list = await _session.GetBackgroundListAsync();
                if (list.BackgroundIds.Count == 0) return;
                _chatBgCursor = (_chatBgCursor + 1) % list.BackgroundIds.Count;
                uint bgId = list.BackgroundIds[_chatBgCursor];
                await _session.UseBackgroundAsync(_store.RoomId, bgId);
                ShowToast($"聊天室背景已切换为 #{bgId}");
            });
        }

        public async void RequestStartGame()
        {
            await RunSafeAsync(async () =>
            {
                ushort res = await _session.StartGameAsync(_store.RoomId);
                if (res != 0) ShowToast($"无法开始对局 (code=0x{res:X4}，请确认对手已准备或启用 AI对战卡)");
            });
        }

        public async void RequestSendLobbyChat(string text, bool smallBugle)
        {
            await RunSafeAsync(async () =>
            {
                ushort res = await _session.SendSectionChatAsync(text, smallBugle);
                if (res != 0) ShowToast("发送大厅聊天失败");
            });
        }

        public async void RequestSendRoomChat(string text)
        {
            await RunSafeAsync(async () =>
            {
                ushort res = await _session.SendRoomChatAsync(_store.RoomId, text);
                if (res != 0) ShowToast("发送房间聊天失败");
            });
        }

        public async void RequestSetItemStatus(ushort itemId, byte newStatus)
        {
            await RunSafeAsync(async () =>
            {
                await _session.SetItemStatusAsync(itemId, newStatus, _session.RoleId);
                _store.SetInventoryItemStatusLocal(itemId, newStatus, _session.RoleId);
                ShowToast(newStatus == 1 ? "道具已装备/启用" : "道具已卸下");
            });
        }

        public async void RequestQueryShopCatalog()
        {
            await RunSafeAsync(async () =>
            {
                var resp = await _session.QueryShopListAsync(_shopCatalogVersion);
                if (resp.LatestShopListVersion > 0) _shopCatalogVersion = resp.LatestShopListVersion;
                ShowToast($"商城目录已同步（版本 v{_shopCatalogVersion}）");
            });
        }

        public async void RequestBuyCommodity(uint commodityId, string name)
        {
            await RunSafeAsync(async () =>
            {
                var resp = await _session.BuyCommodityAsync(commodityId, ShopPaymentMethod.GameMoney, _shopCatalogVersion);
                if (resp.ResultId == 0)
                {
                    _store.UpdateMoney(resp.TicketLeft);
                    ShowToast($"购买成功：{name}！剩余糖币 {resp.TicketLeft}");
                }
                else
                {
                    ShowToast($"购买失败：{resp.ResultString}");
                }
            });
        }

        public async void RequestBreakEgg(uint hammerId, uint eggId, Action<BreakEggResponseData> onDone)
        {
            await RunSafeAsync(async () =>
            {
                var resp = await _session.BreakEggAsync(hammerId, eggId);
                onDone?.Invoke(resp);
                ShowToast(resp.ResultId == 0 ? $"砸蛋获得 {_catalogs.GetItemName((ushort)resp.ShowItemId)}！" : $"砸蛋失败: {resp.AttachInfo}");
            });
        }

        public async void RequestCombineForge(uint productItemId, uint bookItemId, List<uint> materialIds)
        {
            await RunSafeAsync(async () =>
            {
                var resp = await _session.CombineForgeAsync(0, productItemId, bookItemId, materialIds);
                ShowToast(resp.ResultId == 0 ? $"合成成功：{_catalogs.GetItemName((ushort)productItemId)}" : $"合成未成功：{resp.Reason}");
            });
        }

        public async void RequestForgeAvatar(uint itemId, uint materialId, Action<ForgeResponseData> onDone)
        {
            await RunSafeAsync(async () =>
            {
                var resp = await _session.ForgeAvatarAsync(0, itemId, materialId);
                onDone?.Invoke(resp);
                ShowToast(resp.ResultId == 0 ? "装扮锻造成功！" : $"锻造未成功：{resp.Reason}");
            });
        }

        public async void RequestRefreshPets()
        {
            await RunSafeAsync(async () =>
            {
                var pets = await _session.RefreshPetsAsync();
                _store.ApplyPets(pets);
            }, silent: true);
        }

        public async void RequestHandlePet(PetEventId eventId, uint petId, string param = "")
        {
            await RunSafeAsync(async () =>
            {
                var resp = await _session.HandlePetAsync(eventId, petId, param ?? string.Empty);
                if (resp.ResultId == 0 && resp.Pet != null)
                    _store.ApplySinglePetUpdate(resp.Pet, eventId);
                ShowToast(resp.ResultId == 0 ? $"灵宠操作成功 ({eventId})" : $"灵宠操作失败：{resp.Reason}");
            });
        }

        public async void RequestRefreshSocial()
        {
            await RunSafeAsync(async () =>
            {
                var fl = await _session.RefreshFriendsAsync();
                _store.ApplyFriendList(fl);
                var m = await _session.GetMarriageInfoAsync();
                _store.ApplyMarriageInfo(m);
                if (_store.KinIndex > 0)
                {
                    var kb = await _session.FetchKinBaseAsync(_store.KinIndex);
                    _store.ApplyKinBase(kb);
                    var km = await _session.FetchKinMembersAsync(_store.KinIndex, kb.ListUpdate);
                    _store.ApplyKinMembers(km);
                }
            }, silent: true);
        }

        public async void RequestAddFriend(uint targetUin, string word)
        {
            await RunSafeAsync(async () =>
            {
                await _session.AddFriendAsync(targetUin, word);
                ShowToast($"已向 {targetUin} 发送好友申请");
            });
        }

        public async void RequestFindFriend(uint targetUin)
        {
            await RunSafeAsync(async () =>
            {
                var res = await _session.FindFriendAsync(targetUin);
                _store.LastFindFriend = res;
                _socialView.RefreshSocial();
            });
        }

        public async void RequestAnswerFriend(uint requesterUin, bool accepted)
        {
            await RunSafeAsync(async () =>
            {
                await _session.AnswerFriendAsync(requesterUin, accepted);
                _store.PendingFriendRequests.RemoveAll(r => r.RequesterUin == requesterUin);
                var fl = await _session.RefreshFriendsAsync();
                _store.ApplyFriendList(fl);
            });
        }

        public async void RequestRemoveFriend(uint targetUin)
        {
            await RunSafeAsync(async () =>
            {
                var res = await _session.RemoveFriendAsync(targetUin);
                if (res.ResultId == 0)
                {
                    _store.FriendUins.Remove(targetUin);
                    _store.FriendDetails.Remove(targetUin);
                    _socialView.RefreshSocial();
                    ShowToast($"已移除好友 {targetUin}");
                }
            });
        }

        public async void RequestCreateKin(string name, string declaration)
        {
            await RunSafeAsync(async () =>
            {
                var res = await _session.CreateKinAsync(name, declaration);
                if (res.ErrorNo == 0 && res.KinIndex > 0)
                {
                    var kb = await _session.FetchKinBaseAsync(res.KinIndex);
                    _store.ApplyKinBase(kb);
                    var km = await _session.FetchKinMembersAsync(res.KinIndex);
                    _store.ApplyKinMembers(km);
                    ShowToast($"家族 [{name}] 创建成功！");
                }
                else
                {
                    ShowToast($"创建家族失败：{res.Reason}");
                }
            });
        }

        public async void RequestInviteKin(uint targetUin)
        {
            await RunSafeAsync(async () =>
            {
                var res = await _session.InviteToKinAsync(_store.KinIndex, targetUin);
                ShowToast(string.IsNullOrEmpty(res.Reason) ? $"已邀请 {targetUin} 加入家族" : res.Reason);
            });
        }

        public async void RequestAnswerKinInvite(uint inviterUin, uint kinIndex, bool accepted)
        {
            await RunSafeAsync(async () =>
            {
                await _session.AnswerKinInviteAsync(inviterUin, kinIndex, accepted);
                _store.PendingKinInvites.RemoveAll(x => x.KinIndex == kinIndex && x.InviterUin == inviterUin);
                if (accepted && kinIndex > 0)
                {
                    await Task.Delay(120);
                    var kb = await _session.FetchKinBaseAsync(kinIndex);
                    _store.ApplyKinBase(kb);
                    var km = await _session.FetchKinMembersAsync(kinIndex, kb.ListUpdate);
                    _store.ApplyKinMembers(km);
                    ShowToast($"已加入家族 [{kb.Name}]！");
                }
                _socialView.RefreshSocial();
            });
        }

        public async void RequestSendKinChat(string message)
        {
            await RunSafeAsync(async () => await _session.SendKinChatAsync(_store.KinIndex, message));
        }

        public async void RequestSetKinNotification(string notice)
        {
            await RunSafeAsync(async () =>
            {
                var res = await _session.SetKinNotificationAsync(_store.KinIndex, notice);
                if (res.Result == 0)
                {
                    var kb = await _session.FetchKinBaseAsync(_store.KinIndex);
                    _store.ApplyKinBase(kb);
                    ShowToast("家族公告已更新");
                }
                else ShowToast($"更新失败：{res.Reason}");
            });
        }

        public async void RequestKickKinMember(uint targetUin)
        {
            await RunSafeAsync(async () =>
            {
                var res = await _session.KickKinMemberAsync(_store.KinIndex, targetUin);
                if (res.Result == 0)
                {
                    var km = await _session.FetchKinMembersAsync(_store.KinIndex);
                    _store.ApplyKinMembers(km);
                    ShowToast($"已移出成员 {targetUin}");
                }
                else ShowToast($"操作失败：{res.Reason}");
            });
        }

        public async void RequestExitKin()
        {
            await RunSafeAsync(async () =>
            {
                var res = await _session.ExitKinAsync(_store.KinIndex);
                if (res.Result == 0) { _store.ClearKin(); ShowToast("已退出家族"); }
                else ShowToast($"退出失败：{res.Reason}");
            });
        }

        public async void RequestDismissKin()
        {
            await RunSafeAsync(async () =>
            {
                var res = await _session.DismissKinAsync(_store.KinIndex);
                if (res.Result == 0) { _store.ClearKin(); ShowToast("家族已解散"); }
                else ShowToast($"解散失败：{res.Reason}");
            });
        }

        public async void RequestSpark(uint targetUin, string word)
        {
            await RunSafeAsync(async () =>
            {
                var res = await _session.RequestSparkAsync(targetUin, word);
                ShowToast(res.Result == 0 ? $"已向 {targetUin} 发起求婚！" : $"求婚失败 (code={res.Result})");
            });
        }

        public async void RequestAnswerSpark(uint proposerUin, bool accepted)
        {
            await RunSafeAsync(async () =>
            {
                await _session.AnswerSparkAsync(proposerUin, accepted);
                _store.PendingSparks.RemoveAll(s => s.ProposerUin == proposerUin);
                var m = await _session.GetMarriageInfoAsync();
                _store.ApplyMarriageInfo(m);
            });
        }

        public async void RequestModifyLoveWord(string loveWord)
        {
            await RunSafeAsync(async () =>
            {
                if (_store.Marriage == null || !_store.Marriage.IsMarried) { ShowToast("尚未结为伴侣"); return; }
                var res = await _session.ModifyLoveWordAsync(_store.Marriage.MarriageUin, loveWord);
                if (res.Result == 0)
                {
                    var m = await _session.GetMarriageInfoAsync();
                    _store.ApplyMarriageInfo(m);
                    ShowToast("爱情宣言已更新");
                }
            });
        }

        public async void RequestDivorce()
        {
            await RunSafeAsync(async () =>
            {
                if (_store.Marriage == null || !_store.Marriage.IsMarried) { ShowToast("尚未结为伴侣"); return; }
                var res = await _session.DivorceAsync(_store.Marriage.MarriageUin);
                if (res.Result == 0)
                {
                    _store.ApplyMarriageInfo(null);
                    ShowToast("已解除伴侣关系");
                }
            });
        }

        public async void RequestStartWedding(ushort weddingModeId)
        {
            await RunSafeAsync(async () =>
            {
                if (_store.Marriage == null || !_store.Marriage.IsMarried) { ShowToast("需先结为道侣再发起婚礼"); return; }
                var res = await _session.StartWeddingAsync(_store.Marriage.MarriageUin, weddingModeId);
                ShowToast(res.Result == 0
                    ? $"已向 {_store.Marriage.SpouseNickname} 发起{(weddingModeId == 1 ? "中式" : "西式")}婚礼！"
                    : $"发起婚礼失败 (code={res.Result}): {res.Word}");
            });
        }

        public async void RequestAnswerWedding(uint proposerUin, bool accepted)
        {
            await RunSafeAsync(async () =>
            {
                var res = await _session.AnswerWeddingAsync(proposerUin, accepted);
                _store.PendingWeddings.RemoveAll(w => w.ProposerUin == proposerUin);
                _socialView.RefreshSocial();
                ShowToast(res.Result == 0
                    ? (accepted ? "婚礼礼成！百年好合！" : "已婉拒婚礼邀请")
                    : $"婚礼回应失败 (code={res.Result}): {res.Word}");
            });
        }

        public async void RequestChangeWeddingMode(ushort weddingModeId)
        {
            await RunSafeAsync(async () =>
            {
                var res = await _session.ChangeWeddingModeAsync(weddingModeId);
                ShowToast(res.Result == 0
                    ? $"婚礼礼堂已切换为{(weddingModeId == 1 ? "中式礼堂" : "西式教堂")}"
                    : $"切换礼堂失败 (code={res.Result}): {res.Word}");
            });
        }

        public async void RequestUseItemInRoom(ushort itemId)
        {
            await RunSafeAsync(async () =>
            {
                if (!_store.InRoom) { ShowToast("仅可在房间内使用该道具"); return; }
                ushort res = await _session.UseItemInRoomAsync(_store.RoomId, itemId);
                ShowToast(res == 0 ? $"已在房间中使用 {_catalogs.GetItemName(itemId)}" : $"使用失败 (code=0x{res:X4})");
            });
        }

        public async void RequestEnterTreasureRoom()
        {
            await RunSafeAsync(async () =>
            {
                var resp = await _session.EnterTreasureRoomAsync();
                if (resp.ResultId == 0)
                {
                    _store.ActiveTreasureRoom = resp;
                    _battleView.ShowTreasureRoom(resp);
                    _audio.PlaySfx("pickup", 0.7f);
                }
                else
                {
                    ShowToast($"当前未触发藏宝室资格 (code={resp.ResultId})");
                }
            });
        }

        public async void RequestGetTreasureItem(uint itemId)
        {
            await RunSafeAsync(async () =>
            {
                ushort res = await _session.GetTreasureItemAsync(itemId);
                if (res == 0)
                {
                    _audio.PlaySfx("pickup", 0.8f);
                    if (_store.ActiveTreasureRoom != null)
                    {
                        _store.ActiveTreasureRoom.Items.RemoveAll(it => it.ItemId == itemId);
                        _battleView.ShowTreasureRoom(_store.ActiveTreasureRoom);
                    }
                    ShowToast($"已拾取藏宝室道具 #{itemId} ({_catalogs.GetItemName((ushort)itemId)})");
                }
                else
                {
                    ShowToast($"拾取失败 (code={res})");
                }
            });
        }

        public async void RequestLeaveTreasureRoom()
        {
            await RunSafeAsync(async () =>
            {
                await _session.LeaveTreasureRoomAsync();
                _store.ActiveTreasureRoom = null;
                _battleView.HideTreasureRoom();
            });
        }

        private async Task RunSafeAsync(Func<Task> action, bool silent = false)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                if (!silent) ShowToast($"操作异常: {ex.Message}");
            }
        }
    }
}
