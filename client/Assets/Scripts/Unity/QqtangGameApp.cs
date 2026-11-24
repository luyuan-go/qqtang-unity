using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Qqtang.Core.Battle;
using Qqtang.Core.Network;
using Qqtang.Core.Protocol;
using UnityEngine;

namespace Qqtang.UnityClient
{
    public enum ClientScreenState
    {
        Login,
        Lobby,
        Room,
        Battle,
        Settlement
    }

    /// <summary>
    /// End-to-end playable Unity 3D client controller:
    ///   Login (QQTAUTH1 + 0x0064) -> Lobby/Room -> Rule-1 3D Battle (QQTPPP UDP + TCP) -> Settlement.
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
        private ArtAssetResolver _art;
        private BattleArena3DView _arenaView;
        private Rule1BattleSimulation _sim;
        private Dictionary<uint, MapElementDef> _mapElements;
        private Dictionary<uint, string> _mapFileById;

        private ClientScreenState _state = ClientScreenState.Login;
        private string _statusMessage = "Ready to connect to local Go qqt-server.";
        private bool _busy;

        // Login form
        private string _host = "127.0.0.1";
        private string _portText = "18000";
        private string _uinText = "1000001";
        private string _password = "123456";
        private byte _selectedRoleId = 1;

        // Lobby & Room state
        private string _newRoomName = "DaoistRoom";
        private bool _createFreeRule;
        private List<RoomListEntryData> _rooms = new List<RoomListEntryData>();
        private ushort _currentRoomId;
        private ushort _currentMapId = 201;
        private ushort _roomOwnerId;
        private bool _roomFreeRule;
        private bool _isReady;
        private bool _aiCardActive;
        private List<RoomMemberData> _roomMembers = new List<RoomMemberData>();

        // Battle state
        private readonly List<LegacyUdpMulticastTarget> _battlePeers = new List<LegacyUdpMulticastTarget>();
        private float _simAccumulatorMs;
        private float _helloTimer;
        private GameOverData _lastSettlement;

        private void Awake()
        {
            _session = new QqtangSessionClient();
            _art = new ArtAssetResolver();
            _arenaView = gameObject.AddComponent<BattleArena3DView>();
            _arenaView.Initialize(_art);
            LoadLocalMapCatalogs();
        }

        private void OnDestroy()
        {
            _session?.Dispose();
        }

        private void LoadLocalMapCatalogs()
        {
            string localMapsDir = Path.Combine(Application.streamingAssetsPath, "LocalMaps");
            string elemPy = Path.Combine(localMapsDir, "mapElem.py");
            string descPy = Path.Combine(localMapsDir, "mapDesc.py");
            if (File.Exists(elemPy))
            {
                try { _mapElements = MapDataLoader.LoadMapElementsPy(elemPy); }
                catch (Exception ex) { _statusMessage = $"mapElem.py load note: {ex.Message}"; }
            }
            if (File.Exists(descPy))
            {
                try { _mapFileById = MapDataLoader.LoadMapFilenamesFromMapDescPy(descPy); }
                catch { _mapFileById = new Dictionary<uint, string>(); }
            }
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
                        catch (Exception ex) { _statusMessage = $"Map {mapId} fallback: {ex.Message}"; }
                    }
                }
            }
            return MapDataLoader.BuildFallbackArena(mapId);
        }

        private void Update()
        {
            DrainNetworkQueues();

            if (_session.IsConnected && _state != ClientScreenState.Login)
            {
                _helloTimer += Time.deltaTime;
                if (_helloTimer >= 15f)
                {
                    _helloTimer = 0f;
                    _ = _session.SendHelloAsync();
                }
            }

            if (_state == ClientScreenState.Battle && _sim != null)
            {
                BattleDirection moveInput = ReadMoveInput();
                bool bombPressed = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.J);

                _simAccumulatorMs += Time.deltaTime * 1000f;
                const uint fixedStepMs = 25;
                bool bombQueued = bombPressed;
                while (_simAccumulatorMs >= fixedStepMs)
                {
                    _simAccumulatorMs -= fixedStepMs;
                    _sim.Step(fixedStepMs, moveInput, bombQueued);
                    bombQueued = false;
                    FlushOutgoingBattleEvents();
                }
                _arenaView.SyncView(_sim);
            }
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
                {
                    if (ev.Item1 != GameEventSchemas.PlayerMove) { mirrorTcp = true; break; }
                }
                _ = _session.SendPeerGameplayAsync(_sim.GameId, _sim.ElapsedMs, _battlePeers, copy, mirrorTcp);
            }
            if (_sim.OutgoingTcpEvents.Count > 0)
            {
                foreach (var ev in _sim.OutgoingTcpEvents)
                {
                    _ = _session.SendGameEventTcpAsync(_currentRoomId, ev.schemaId, ev.body);
                }
            }
        }

        private void DrainNetworkQueues()
        {
            while (_session.TryDequeueDiagnostic(out string diag))
                _statusMessage = diag;

            while (_session.TryDequeueTcpPacket(out var pkt))
                HandleServerTcpPush(pkt);

            while (_session.TryDequeueUdpBatch(out var batch))
            {
                if (_state == ClientScreenState.Battle && _sim != null && batch.GameId == _sim.GameId)
                {
                    foreach (var entry in batch.Entries)
                    {
                        foreach (var msg in entry.Package.Messages)
                            _sim.ApplyRemoteMessage(entry.Package.PlayerId, msg.DataId, msg.Data);
                    }
                }
            }
        }

        private void HandleServerTcpPush(DecodedLocalPacket pkt)
        {
            try
            {
                switch (pkt.Command)
                {
                    case GameCommands.RoomPush:
                        var pushed = RoomListResponseData.ParseRoomPush(pkt.Payload);
                        foreach (var r in pushed)
                        {
                            _rooms.RemoveAll(x => x.RoomId == r.RoomId);
                            if (r.CurrentPlayers > 0) _rooms.Add(r);
                        }
                        break;

                    case GameCommands.EnterRoomNotify:
                        var (member, roomId) = EnterRoomResponseData.ParseEnterRoomNotify(pkt.Payload);
                        if (roomId == _currentRoomId)
                        {
                            _roomMembers.RemoveAll(m => m.PlayerId == member.PlayerId);
                            _roomMembers.Add(member);
                        }
                        break;

                    case GameCommands.LeaveRoomNotify:
                        var left = LeaveRoomNotifyData.Parse(pkt.Payload);
                        _roomMembers.RemoveAll(m => m.PlayerId == left.PlayerId);
                        if (left.NewRoomOwnerId != 0) _roomOwnerId = left.NewRoomOwnerId;
                        break;

                    case GameCommands.ReadyStateNotify:
                        var rr = new BigEndianReader(pkt.Payload);
                        rr.ReadUInt32(); // UIN
                        ushort pid = rr.ReadUInt16();
                        byte ready = rr.ReadByte();
                        var rm = _roomMembers.Find(m => m.PlayerId == pid);
                        if (rm != null) rm.Status = ready;
                        break;

                    case GameCommands.ChangeTeamNotify:
                        var tr = new BigEndianReader(pkt.Payload);
                        tr.ReadUInt32();
                        ushort tpid = tr.ReadUInt16();
                        byte newTeam = tr.ReadByte();
                        var tm = _roomMembers.Find(m => m.PlayerId == tpid);
                        if (tm != null) tm.TeamId = newTeam;
                        break;

                    case GameCommands.ChangeRoleNotify:
                        var cr = new BigEndianReader(pkt.Payload);
                        cr.ReadUInt32();
                        ushort rpid = cr.ReadUInt16();
                        byte newRole = cr.ReadByte();
                        var cm = _roomMembers.Find(m => m.PlayerId == rpid);
                        if (cm != null && cm.GameInfo != null) cm.GameInfo.RoleId = newRole;
                        break;

                    case GameCommands.ModifyRoomInfoNotify:
                        var mr = new BigEndianReader(pkt.Payload);
                        _currentMapId = mr.ReadUInt16();
                        break;

                    case GameCommands.GameBeginNotify:
                        var begin = GameBeginData.ParseLengthPrefixed(pkt.Payload);
                        StartBattle(begin);
                        break;

                    case GameCommands.GameEventNotify:
                        var ev = NotifyGameEventMessage.Parse(pkt.Payload);
                        if (ev.Schema == GameEventSchemas.NotifyGameOver)
                        {
                            _lastSettlement = GameOverData.Parse(ev.Body);
                            _state = ClientScreenState.Settlement;
                            _statusMessage = "Match settled by Go server.";
                        }
                        else if (_sim != null)
                        {
                            _sim.ApplyRemoteMessage(0, ev.Schema, ev.Body);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _statusMessage = $"Packet 0x{pkt.Command:X4} error: {ex.Message}";
            }
        }

        private void StartBattle(GameBeginData begin)
        {
            _currentMapId = (ushort)begin.MapId;
            _battlePeers.Clear();
            foreach (var p in begin.Players)
            {
                if (p.PlayerId == _session.PlayerId) continue;
                var member = _roomMembers.Find(m => m.PlayerId == p.PlayerId);
                uint peerUin = member != null ? member.Uin : (uint)(900000000u + p.PlayerId);
                _battlePeers.Add(new LegacyUdpMulticastTarget { PlayerId = p.PlayerId, Uin = peerUin });
                _ = _session.SendUdpOkAsync(p.PlayerId, peerUin, readyMode: true);
            }

            LoadedMapData map = LoadBattleMap(begin.MapId);
            _sim = new Rule1BattleSimulation();
            _sim.Initialize(begin, map, _session.PlayerId, _roomFreeRule);
            _simAccumulatorMs = 0f;
            _arenaView.BuildArena(_sim);
            _arenaView.SyncView(_sim);
            _state = ClientScreenState.Battle;
            _statusMessage = $"Battle started! GameID={begin.GameId}, Map={begin.MapId}, Players={begin.Players.Count}";
        }

        private async void RunAsyncAction(string label, Func<Task> action)
        {
            if (_busy) return;
            _busy = true;
            _statusMessage = $"{label}...";
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                _statusMessage = $"{label} failed: {ex.Message}";
            }
            finally
            {
                _busy = false;
            }
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(10, 10, 440, 26), $"[QQTang Unity 3D] {_state} | {_statusMessage}");

            switch (_state)
            {
                case ClientScreenState.Login: DrawLoginGui(); break;
                case ClientScreenState.Lobby: DrawLobbyGui(); break;
                case ClientScreenState.Room: DrawRoomGui(); break;
                case ClientScreenState.Battle: DrawBattleHud(); break;
                case ClientScreenState.Settlement: DrawSettlementGui(); break;
            }
        }

        private void DrawLoginGui()
        {
            GUILayout.BeginArea(new Rect(20, 50, 360, 280), "Login (QQTAUTH1 -> 0x0064)", GUI.skin.window);
            GUILayout.BeginHorizontal(); GUILayout.Label("Host:", GUILayout.Width(80)); _host = GUILayout.TextField(_host); GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(); GUILayout.Label("Port:", GUILayout.Width(80)); _portText = GUILayout.TextField(_portText); GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(); GUILayout.Label("Account UIN:", GUILayout.Width(80)); _uinText = GUILayout.TextField(_uinText); GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(); GUILayout.Label("Password:", GUILayout.Width(80)); _password = GUILayout.PasswordField(_password, '*'); GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Role (1-4):", GUILayout.Width(80));
            for (byte r = 1; r <= 4; r++)
            {
                if (GUILayout.Toggle(_selectedRoleId == r, $"R{r}", "Button")) _selectedRoleId = r;
            }
            GUILayout.EndHorizontal();

            GUI.enabled = !_busy;
            if (GUILayout.Button("Connect & Login", GUILayout.Height(36)))
            {
                if (uint.TryParse(_uinText, out uint uin) && int.TryParse(_portText, out int port))
                {
                    RunAsyncAction("Login", async () =>
                    {
                        var resp = await _session.ConnectAndLoginAsync(_host, port, uin, _password, _selectedRoleId);
                        _aiCardActive = resp.Items.Exists(i => i.ItemId == SpecialItemIds.CompetitiveAiCard && i.Status == 1);
                        var list = await _session.RefreshRoomListAsync();
                        _rooms = list.Rooms;
                        _state = ClientScreenState.Lobby;
                        _statusMessage = $"Logged in as {_session.Nickname} (PlayerID={resp.PlayerId})";
                    });
                }
            }
            GUI.enabled = true;
            GUILayout.EndArea();
        }

        private void DrawLobbyGui()
        {
            GUILayout.BeginArea(new Rect(20, 50, 480, 420), $"Lobby - {_session.Nickname} (ID={_session.PlayerId})", GUI.skin.window);
            GUILayout.BeginHorizontal();
            _newRoomName = GUILayout.TextField(_newRoomName, GUILayout.Width(160));
            _createFreeRule = GUILayout.Toggle(_createFreeRule, "Free Rule");
            GUI.enabled = !_busy;
            if (GUILayout.Button("Create Room"))
            {
                RunAsyncAction("CreateRoom", async () =>
                {
                    var flag = _createFreeRule ? RoomPropertyFlag.FreeRule : RoomPropertyFlag.Standard;
                    ushort rid = await _session.CreateRoomAsync(_newRoomName, flag, RoomGameType.CompetitiveNoItem);
                    await _session.SelectMapAsync(rid, _currentMapId, RoomGameType.CompetitiveNoItem);
                    _currentRoomId = rid;
                    _roomOwnerId = _session.PlayerId;
                    _roomFreeRule = _createFreeRule;
                    _isReady = false;
                    _roomMembers = new List<RoomMemberData>
                    {
                        new RoomMemberData { Uin = _session.Uin, Nickname = _session.Nickname, PlayerId = _session.PlayerId, TeamId = 1, SeatId = 1, GameInfo = new GameInfoData { RoleId = _selectedRoleId } }
                    };
                    _state = ClientScreenState.Room;
                    _statusMessage = $"Created Room #{rid} (Map {_currentMapId})";
                });
            }
            if (GUILayout.Button("Refresh"))
            {
                RunAsyncAction("RefreshRooms", async () =>
                {
                    var list = await _session.RefreshRoomListAsync();
                    _rooms = list.Rooms;
                    _statusMessage = $"Found {_rooms.Count} rooms.";
                });
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Space(8);
            GUILayout.Label($"Rooms ({_rooms.Count}):");
            foreach (var r in _rooms)
            {
                GUILayout.BeginHorizontal("box");
                GUILayout.Label($"#{r.RoomId} {r.Name} | Map:{r.MapId} | {r.CurrentPlayers}/{r.Capacity}");
                GUI.enabled = !_busy;
                if (GUILayout.Button("Join", GUILayout.Width(70)))
                {
                    ushort targetId = r.RoomId;
                    RunAsyncAction("EnterRoom", async () =>
                    {
                        var res = await _session.EnterRoomAsync(targetId, _selectedRoleId);
                        if (res.ResultId != 0) throw new Exception($"ResultID={res.ResultId}");
                        _currentRoomId = res.RoomId;
                        _currentMapId = res.MapId;
                        _roomOwnerId = res.RoomOwnerId;
                        _roomFreeRule = (res.RoomFlag & (byte)RoomPropertyFlag.FreeRule) != 0;
                        _roomMembers = res.Players;
                        _isReady = false;
                        _state = ClientScreenState.Room;
                        _statusMessage = $"Joined Room #{res.RoomId}";
                    });
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            GUILayout.EndArea();
        }

        private void DrawRoomGui()
        {
            bool isOwner = _roomOwnerId == _session.PlayerId;
            GUILayout.BeginArea(new Rect(20, 50, 480, 420), $"Room #{_currentRoomId} | Map {_currentMapId} | Owner={_roomOwnerId}", GUI.skin.window);

            GUILayout.Label("Players:");
            foreach (var m in _roomMembers)
            {
                string tag = m.PlayerId == _roomOwnerId ? "[OWNER]" : (m.Status == 1 ? "[READY]" : "[WAIT]");
                GUILayout.Label($"  Seat {m.SeatId} | P{m.PlayerId} {m.Nickname} | Team {m.TeamId} | Role {m.GameInfo?.RoleId ?? 1} {tag}");
            }

            GUILayout.Space(8);
            GUI.enabled = !_busy;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Team:", GUILayout.Width(45));
            for (byte t = 1; t <= 4; t++)
            {
                byte teamChoice = t;
                if (GUILayout.Button($"T{t}"))
                    RunAsyncAction("ChangeTeam", () => _session.ChangeTeamAsync(_currentRoomId, teamChoice));
            }
            GUILayout.EndHorizontal();

            if (isOwner)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Map:", GUILayout.Width(45));
                foreach (ushort mid in new ushort[] { 201, 202, 203, 101 })
                {
                    ushort mapChoice = mid;
                    if (GUILayout.Button($"{mid}"))
                        RunAsyncAction("SelectMap", async () => { await _session.SelectMapAsync(_currentRoomId, mapChoice); _currentMapId = mapChoice; });
                }
                GUILayout.EndHorizontal();

                if (GUILayout.Button(_aiCardActive ? "AI Card (30099): ON" : "Activate AI Card (30099)"))
                {
                    byte nextStatus = (byte)(_aiCardActive ? 0 : 1);
                    RunAsyncAction("ToggleAI", async () => { await _session.SetItemStatusAsync(SpecialItemIds.CompetitiveAiCard, nextStatus); _aiCardActive = nextStatus == 1; });
                }

                if (GUILayout.Button("START GAME", GUILayout.Height(36)))
                {
                    RunAsyncAction("StartGame", async () =>
                    {
                        ushort res = await _session.StartGameAsync(_currentRoomId);
                        if (res != 0) _statusMessage = $"Start rejected (0x{res:X4}) - need opponent or AI card.";
                    });
                }
            }
            else
            {
                if (GUILayout.Button(_isReady ? "Cancel Ready" : "READY", GUILayout.Height(36)))
                {
                    bool nextReady = !_isReady;
                    RunAsyncAction("Ready", async () => { await _session.SetReadyAsync(_currentRoomId, nextReady); _isReady = nextReady; });
                }
            }

            if (GUILayout.Button("Leave Room"))
            {
                RunAsyncAction("LeaveRoom", async () =>
                {
                    await _session.LeaveRoomAsync(_currentRoomId);
                    _arenaView.ClearArena();
                    _state = ClientScreenState.Lobby;
                });
            }
            GUI.enabled = true;
            GUILayout.EndArea();
        }

        private void DrawBattleHud()
        {
            if (_sim == null) return;
            var me = _sim.LocalActor;
            float remainSec = Mathf.Max(0f, (_sim.RoundDurationMs - _sim.ElapsedMs) / 1000f);
            string artSrc = _art.GetResolvedSource("characters/role_0_S");

            GUILayout.BeginArea(new Rect(10, 42, 290, 175), "Rule-1 Battle HUD", GUI.skin.window);
            GUILayout.Label($"Time Left: {remainSec:F1}s | Art: {artSrc}");
            if (_sim.ElapsedMs < Rule1BattleSimulation.RoundStartClockMs)
                GUILayout.Label($"READY... {(Rule1BattleSimulation.RoundStartClockMs - _sim.ElapsedMs) / 1000f:F1}s");
            if (me != null)
            {
                GUILayout.Label($"Me: P{me.PlayerId} Team {me.TeamId} [{me.State}]");
                GUILayout.Label($"Bombs: {me.BombCapacity}/{me.MaxBombCapacity} | Power: {me.BombPower} | Speed: {me.SpeedRate}");
            }
            GUILayout.Label("Controls: WASD / Arrows = Move, Space = Bomb");
            GUILayout.EndArea();
        }

        private void DrawSettlementGui()
        {
            GUILayout.BeginArea(new Rect(Screen.width * 0.5f - 180, Screen.height * 0.5f - 120, 360, 240), "Match Settlement", GUI.skin.window);
            if (_lastSettlement != null)
            {
                foreach (var r in _lastSettlement.Results)
                    GUILayout.Label($"Player {r.PlayerId}: {r.Result} (Points: +{r.Point})");
            }
            GUILayout.Space(12);
            if (GUILayout.Button("Return to Room", GUILayout.Height(36)))
            {
                _arenaView.ClearArena();
                _sim = null;
                _state = ClientScreenState.Room;
            }
            GUILayout.EndArea();
        }
    }
}
