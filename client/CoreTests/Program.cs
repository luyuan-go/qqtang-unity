using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Qqtang.Core.Battle;
using Qqtang.Core.Network;
using Qqtang.Core.Protocol;
using Qqtang.Core.State;

namespace Qqtang.Core.Tests
{
    internal static class Program
    {
        private static int _passed;

        private static async Task<int> Main(string[] args)
        {
            try
            {
                TestQqTeaRoundTrip();
                TestUdpChecksumAndRoundTrip();
                TestGameplayBatchRoundTrip();
                TestRule1SimulationMechanics();
                TestOfflineSoloAiAndBossMechanics();
                TestStaticCatalogs();
                TestRealMapFileIfPresent();

                if (!args.Contains("--unit-only"))
                {
                    await TestLiveGoServerEndToEndAsync();
                }

                Console.WriteLine($"\nALL {_passed} CHECKS PASSED!");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"\nTEST FAILED: {ex}");
                return 1;
            }
        }

        private static void Assert(bool cond, string message)
        {
            if (!cond) throw new Exception($"Assertion failed: {message}");
        }

        private static void Pass(string name)
        {
            _passed++;
            Console.WriteLine($"[PASS] {name}");
        }

        private static void TestQqTeaRoundTrip()
        {
            byte[] plain = Encoding.UTF8.GetBytes("QQTang-Unity-Protocol-Check-2026!");
            byte[] enc = QqTea.Encrypt(plain, QqTea.LocalKey);
            byte[] dec = QqTea.Decrypt(enc, QqTea.LocalKey);
            Assert(plain.SequenceEqual(dec), "QQ-TEA round-trip mismatch");

            byte[] frame = LocalPacketCodec.EncodeLocalPacket(1, 2, 1000001, GameCommands.Hello, 2, 1, new byte[] { 1, 2, 3, 4 });
            DecodedLocalPacket decoded = LocalPacketCodec.DecodeLocalPacket(frame);
            Assert(decoded.Command == GameCommands.Hello && decoded.EnvelopeUin == 1000001 && decoded.Payload.Length == 4,
                "LocalPacketCodec round-trip mismatch");
            Pass("QQ-TEA & LocalPacketCodec");
        }

        private static void TestUdpChecksumAndRoundTrip()
        {
            var ep = new LegacyUdpEndpoint { IPv4 = new byte[] { 127, 0, 0, 1 }, Port = 45678 };
            byte[] dgram = QqtPppUdpCodec.EncodePresence(1, 7, 1000001, ep);
            Assert(QqtPppUdpCodec.ComputeChecksum(dgram) == 0, "UDP presence checksum non-zero");
            var pkt = QqtPppUdpCodec.DecodeControlPacket(dgram);
            Assert(pkt.Type == LegacyUdpMessageType.Presence && pkt.PlayerId == 7 && pkt.Presence.Value.Port == 45678,
                "UDP presence decode mismatch");
            Pass("QQTPPP UDP Checksum & Presence");
        }

        private static void TestGameplayBatchRoundTrip()
        {
            var move = new PlayerMoveMessage
            {
                PlayerId = 1,
                Entries = new List<PlayerMoveEntryData>
                {
                    new PlayerMoveEntryData
                    {
                        PlayerId = 1,
                        Move = new PlayerMoveSequenceData
                        {
                            Sequence = 3, TimeStamp = 3100,
                            CurrentPosX = 60, CurrentPosY = 60, EndPosX = 100, EndPosY = 60,
                            WalkAndDirection = Rule1BattleSimulation.ToNativeWalkByte(BattleDirection.Right, true, false),
                            Speed = 4
                        }
                    }
                }
            };
            var pkg = new GameplayDataPackage
            {
                PlayerId = 1, Time = 3100, GameId = 42,
                MessageIndexes = new List<uint> { 1 },
                Messages = new List<BattleMessageData>
                {
                    new BattleMessageData { Time = 3100, DataId = GameEventSchemas.PlayerMove, Sequence = 1, Data = move.Marshal(), GameTime = 3100 }
                }
            };
            var batch = new GameplayBatch
            {
                GameId = 42, FirstIndex = 0,
                Entries = new List<GameplayBatchEntry> { new GameplayBatchEntry { Index = 1, Package = pkg } }
            };
            byte[] enc = batch.EncodeEncrypted();
            var dec = GameplayBatch.DecodeEncrypted(enc);
            Assert(dec.GameId == 42 && dec.Entries.Count == 1 && dec.Entries[0].Package.Messages.Count == 1,
                "GameplayBatch decode mismatch");
            var parsedMove = PlayerMoveMessage.Parse(dec.Entries[0].Package.Messages[0].Data);
            Assert(parsedMove.Entries[0].Move.EndPosX == 100, "PlayerMove decode mismatch");
            Pass("GameplayBatch (0x0FBD) & PlayerMove (0x0FA2)");
        }

        private static void TestRule1SimulationMechanics()
        {
            var map = MapDataLoader.BuildFallbackArena(201);
            var begin = new GameBeginData
            {
                GameId = 1, MapId = 201, SpawnSeed = 12345, ItemSeed = 67890,
                ArbitratorPlayerId = 1, GameTimeMs = 240_000,
                Players = new List<PlayerGameInfoData>
                {
                    new PlayerGameInfoData { PlayerId = 1, RoleId = 1, TeamId = 1 },
                    new PlayerGameInfoData { PlayerId = 2, RoleId = 2, TeamId = 2 }
                },
                NewItems = new List<GameItemTypeData> { new GameItemTypeData { ItemId = 1, Quantity = 5 } }
            };
            var sim = new Rule1BattleSimulation();
            sim.Initialize(begin, map, 1, false);
            Assert(sim.Actors.Count == 2 && sim.Pickups.Count == 5, "Simulation init mismatch");

            // Advance past 3000ms Ready/Go
            sim.Step(3000, BattleDirection.None, false);
            int startX = sim.LocalActor.PosX;
            BattleDirection moveDir = sim.LocalActor.CurrentCell.Col < map.Width / 2
                ? BattleDirection.Right
                : BattleDirection.Left;
            sim.Step(100, moveDir, true);
            Assert(sim.LocalActor.PosX != startX, $"Local actor at {sim.LocalActor.CurrentCell} should move {moveDir}");
            Assert(sim.Bombs.Count == 1, "Local bomb should be placed");
            Assert(sim.OutgoingPeerEvents.Any(e => e.schemaId == GameEventSchemas.PlayerUseBomb), "Should emit PlayerUseBomb");

            // Advance 3050ms to detonate bomb
            sim.Step(3050, BattleDirection.None, false);
            Assert(sim.Bombs.Count == 0 && sim.Flames.Count > 0, "Bomb should detonate into flames");
            Assert(sim.OutgoingTcpEvents.Any(e => e.schemaId == GameEventSchemas.NotifyBombExplode), "Should emit NotifyBombExplode");

            // Test extended mechanics: Needle self-rescue (63), Shield (41), Remote Relive (0x0FBA), Emotion (0x1194)
            sim.LocalActor.State = ActorLifeState.Trapped;
            sim.LocalActor.NeedleCount = 1;
            Assert(sim.TryUseWorldItem(63), "Needle self-rescue should succeed");
            Assert(sim.LocalActor.State == ActorLifeState.Active && sim.LocalActor.NeedleCount == 0, "Actor should be active after needle");
            sim.LocalActor.ShieldCount = 1;
            Assert(sim.TryUseWorldItem(41), "Shield activation should succeed");
            Assert(sim.LocalActor.IsInvulnerable(sim.ElapsedMs), "Actor should be invulnerable while shield active");
            sim.TriggerLocalEmotion(1000001, 5);
            Assert(sim.LocalActor.ActiveEmotionId == 5, "Emotion ID should be set");
            var reliveBody = new PlayerReliveEventData { PlayerId = 2, ClientTime = sim.ElapsedMs, Row = 1, Col = 1 }.Marshal();
            sim.GetActor(2).State = ActorLifeState.Dead;
            sim.ApplyRemoteMessage(2, ExtendedGameEventSchemas.NotifyPlayerRelive, reliveBody);
            Assert(sim.GetActor(2).State == ActorLifeState.Active, "Remote relive should revive actor");

            Pass("Rule-1 BattleSimulation (move, bomb, fuse, blast, needle, shield, relive, emote)");
        }

        private static void TestOfflineSoloAiAndBossMechanics()
        {
            var map = MapDataLoader.BuildFallbackArena(201);
            var begin = new GameBeginData
            {
                GameId = 99, MapId = 201, SpawnSeed = 777, ItemSeed = 888,
                ArbitratorPlayerId = 1, GameTimeMs = 120_000,
                Players = new List<PlayerGameInfoData>
                {
                    new PlayerGameInfoData { PlayerId = 1, RoleId = 1, TeamId = 1 },
                    new PlayerGameInfoData { PlayerId = 2, RoleId = 2, TeamId = 2 }
                },
                NewItems = new List<GameItemTypeData> { new GameItemTypeData { ItemId = 1, Quantity = 4 } }
            };
            var sim = new Rule1BattleSimulation { IsOfflineSoloMode = true };
            sim.Initialize(begin, map, 1, false);
            var bot = new LocalBattleAiBot(2, 42);
            int botStartX = sim.GetActor(2).PosX;
            int botStartY = sim.GetActor(2).PosY;
            sim.Step(3000, BattleDirection.None, false);

            bool botPlacedBomb = false;
            for (int tick = 0; tick < 80 && !sim.IsFinished; tick++)
            {
                var (dir, bomb) = bot.Decide(sim);
                if (bomb) botPlacedBomb = true;
                sim.StepOfflineBot(2, 50, dir, bomb);
                sim.Step(50, BattleDirection.None, false);
            }
            var botActor = sim.GetActor(2);
            Assert(botActor.PosX != botStartX || botActor.PosY != botStartY, "Offline AI bot should navigate away from spawn");
            Assert(botPlacedBomb, "Offline AI bot should place at least one tactical bomb");

            // Test PVE Boss creation (0x138A) + Boss damage (0x10F4) + Bun pickup/delivery (0x0FB7/0x0FB9)
            var pveBuf = new BigEndianBuffer(32);
            pveBuf.WriteUInt32(1); // 1 group
            pveBuf.WriteUInt16(101); // BossID
            pveBuf.WriteByte(2); // 2 instances
            pveBuf.WriteUInt16(0); // 0 normal items
            sim.ApplyRemoteMessage(0, ModeEventSchemas.CreatePveNpcBoss, pveBuf.ToArray());
            Assert(sim.Bosses.Count == 2 && sim.Bosses[0].Alive, "PVE Boss group should spawn 2 boss entities");

            var harmBody = new EntityHarmedEventData { ObjectId = sim.Bosses[0].BossId, Time = sim.ElapsedMs, PosX = 100, PosY = 100, LossHp = 500 }.Marshal();
            sim.ApplyRemoteMessage(0, ModeEventSchemas.PlayerBeHarmed, harmBody);
            Assert(sim.Bosses[0].Hp == 1000, $"Expected boss HP 1000 after 500 loss, got {sim.Bosses[0].Hp}");

            var getBun = new BunActionEventData { PlayerId = 1, ClientTime = sim.ElapsedMs, PosX = 60, PosY = 60, BunId = 1, BunTeamId = 2 }.Marshal();
            sim.ApplyRemoteMessage(0, ModeEventSchemas.NotifyPlayerGetBun, getBun);
            Assert(sim.BunCarrierPlayerId == 1, "Bun carrier should be P1");
            sim.ApplyRemoteMessage(0, ModeEventSchemas.NotifyPlayerPutBun, getBun);
            Assert(sim.BunCarrierPlayerId == 0 && sim.TeamBunScores[1] == 1, "Team 1 bun score should increment to 1");

            // Force offline elimination -> verify OfflineSettlement generated
            botActor.State = ActorLifeState.Dead;
            sim.Step(50, BattleDirection.None, false);
            Assert(sim.IsFinished && sim.OfflineSettlement != null && sim.OfflineSettlement.Results.Count == 2,
                "Offline solo mode should generate settlement when only 1 team survives");
            Pass("Offline Solo AI Bot + PVE Boss (0x138A/0x10F4) + Bun Objective (0x0FB7/0x0FB9)");
        }

        private static void TestStaticCatalogs()
        {
            string repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
            string serverDir = Path.Combine(repoRoot, "server");
            var catalogs = new StaticCatalogs();
            catalogs.LoadFromDirectories(serverDir, @"C:\Users\luyua\Downloads\QQTang-Local\runtime\client-patched");
            Assert(catalogs.Maps.Count >= 300, $"Expected >=300 maps, got {catalogs.Maps.Count}");
            Assert(catalogs.ItemsById.Count >= 2000, $"Expected >=2000 items, got {catalogs.ItemsById.Count}");
            Assert(catalogs.CombineRecipes.Count >= 10, $"Expected >=10 combine recipes, got {catalogs.CombineRecipes.Count}");
            Assert(catalogs.PetsByTypeId.Count >= 10, $"Expected >=10 pet types, got {catalogs.PetsByTypeId.Count}");
            Assert(StaticCatalogs.GetRankTitle(1).Contains("QQ堂平民"), "Rank title mismatch");
            Pass($"StaticCatalogs (maps={catalogs.Maps.Count}, items={catalogs.ItemsById.Count}, recipes={catalogs.CombineRecipes.Count}, pets={catalogs.PetsByTypeId.Count}, shop={catalogs.ShopCommodities.Count})");
        }

        private static void TestRealMapFileIfPresent()
        {
            string clientRoot = @"C:\Users\luyua\Downloads\QQTang-Local\runtime\client-patched";
            string mapElemPath = Path.Combine(clientRoot, "object", "mapElem", "mapElem.py");
            string mapPath = Path.Combine(clientRoot, "map", "bomb01_8.map");
            if (!File.Exists(mapElemPath) || !File.Exists(mapPath))
            {
                Console.WriteLine("[SKIP] Real .map parse (local QQTang-Local not present)");
                return;
            }
            var elements = MapDataLoader.LoadMapElementsPy(mapElemPath);
            Assert(elements.Count > 500, $"Expected >500 map elements, got {elements.Count}");
            var parsed = MapDataLoader.ParseMapBytes(1, File.ReadAllBytes(mapPath), elements);
            Assert(parsed.Width == 15 && parsed.Height == 13 && parsed.SpawnGroupA.Count > 0,
                $"Unexpected real .map geometry {parsed.Width}x{parsed.Height}");
            Pass($"Real .map parser ({elements.Count} elements, {parsed.Width}x{parsed.Height}, spawnsA={parsed.SpawnGroupA.Count})");
        }

        private static readonly Random PortRng = new Random();

        private static int FindFreeTcpPort()
        {
            int start = PortRng.Next(28000, 34000);
            for (int i = 0; i < 500; i++)
            {
                int port = 28000 + ((start - 28000 + i) % 6000);
                try
                {
                    var l = new TcpListener(IPAddress.Loopback, port);
                    l.Start();
                    l.Stop();
                    using var u = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
                    return port;
                }
                catch { }
            }
            throw new InvalidOperationException("Could not find a free TCP+UDP port.");
        }

        private static async Task TestLiveGoServerEndToEndAsync()
        {
            string repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
            string serverDir = Path.Combine(repoRoot, "server");
            string clientPatchedRoot = @"C:\Users\luyua\Downloads\QQTang-Local\runtime\client-patched";
            if (!Directory.Exists(clientPatchedRoot))
            {
                Console.WriteLine("[SKIP] Live Go server E2E (QQTang-Local/runtime/client-patched not found)");
                return;
            }

            string tempDir = Path.Combine(Path.GetTempPath(), "qqtang-unity-e2e-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(tempDir);
            string serverExe = Path.Combine(tempDir, "qqt-server.exe");
            Process serverProc = null;

            try
            {
                // 1. Build qqt-server from our monorepo's server/
                var build = Process.Start(new ProcessStartInfo
                {
                    FileName = "go",
                    Arguments = $"build -o \"{serverExe}\" ./cmd/qqt-server",
                    WorkingDirectory = serverDir,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                });
                build.WaitForExit();
                Assert(build.ExitCode == 0, "go build ./cmd/qqt-server failed: " + build.StandardError.ReadToEnd());

                // 2. Write isolated test server config on ephemeral port
                int gamePort = FindFreeTcpPort();
                int shopPort = FindFreeTcpPort();
                int gmPort = FindFreeTcpPort();
                string dbPath = Path.Combine(tempDir, "test.sqlite");
                string logPath = Path.Combine(tempDir, "server.jsonl");
                string cfgPath = Path.Combine(tempDir, "config.json");

                string cfgJson = $@"{{
  ""capture_root"": ""{EscapeJson(Path.Combine(tempDir, "captures"))}"",
  ""log_file"": ""{EscapeJson(logPath)}"",
  ""client_root"": ""{EscapeJson(clientPatchedRoot)}"",
  ""competitive_map_rules_path"": ""{EscapeJson(Path.Combine(serverDir, "data", "qqt_competitive_map_rules.json"))}"",
  ""database_path"": ""{EscapeJson(dbPath)}"",
  ""seed_uin"": 1000001,
  ""initial_accounts"": [
    {{ ""uin"": 1000001, ""nickname"": ""PlayerOne"", ""gender"": 1 }},
    {{ ""uin"": 1000002, ""nickname"": ""PlayerTwo"", ""gender"": 0, ""identity"": 0 }}
  ],
  ""adventure_rules_path"": ""{EscapeJson(Path.Combine(serverDir, "configs", "adventure-rules.json"))}"",
  ""role_rules_path"": ""{EscapeJson(Path.Combine(serverDir, "configs", "role-rules.json"))}"",
  ""equipment_seeds_path"": ""{EscapeJson(Path.Combine(serverDir, "configs", "starter-loadouts.json"))}"",
  ""combine_recipes_path"": ""{EscapeJson(Path.Combine(serverDir, "data", "qqt_combine_recipes.json"))}"",
  ""break_egg_rewards_path"": ""{EscapeJson(Path.Combine(serverDir, "configs", "break-egg-rewards.json"))}"",
  ""forge_rules_path"": ""{EscapeJson(Path.Combine(serverDir, "data", "qqt_forge_rules.json"))}"",
  ""item_image_aliases_path"": ""{EscapeJson(Path.Combine(serverDir, "configs", "item-image-aliases.json"))}"",
  ""gm_http_address"": ""127.0.0.1:{gmPort}"",
  ""directory_hall"": {{
    ""server_ip"": ""127.0.0.1"",
    ""server_port"": {gamePort},
    ""server_udp_port"": {gamePort},
    ""shop_server_id"": 2,
    ""shop_server_port"": {shopPort},
    ""shop_server_udp_port"": {shopPort}
  }},
  ""max_packet_bytes"": 1048576,
  ""idle_timeout_ms"": 60000,
  ""player_profile"": {{
    ""nickname"": ""PlayerOne"", ""gender"": 1, ""icon_id"": 0, ""player_id"": 1, ""identity"": 8192,
    ""section_id"": 1, ""room_count"": 100, ""minimum_room_id"": 1, ""tutorial_completed"": true,
    ""game_info"": {{ ""wins"": 10, ""losses"": 2, ""draws"": 1, ""points"": 5000, ""money"": 99999, ""degree"": 5, ""role_id"": 1 }},
    ""inventory"": [ {{ ""id"": 99, ""quantity"": 1 }}, {{ ""id"": 30098, ""quantity"": 1 }}, {{ ""id"": 9001, ""quantity"": 1 }}, {{ ""id"": 9011, ""quantity"": 1 }}, {{ ""id"": 2235, ""quantity"": 2 }}, {{ ""id"": 9020, ""quantity"": 2 }}, {{ ""id"": 9021, ""quantity"": 2 }} ]
  }},
  ""listeners"": [
    {{
      ""name"": ""game-tcp"",
      ""network"": ""tcp"",
      ""address"": ""127.0.0.1:{gamePort}"",
      ""response"": {{
        ""qqt_directory_response"": true,
        ""qqt_login_success"": true,
        ""qqt_room_list"": true,
        ""qqt_player_list"": true,
        ""qqt_create_room_success"": true,
        ""qqt_start_game_success"": true,
        ""qqt_game_begin"": true,
        ""qqt_game_event_relay"": true
      }}
    }},
    {{
      ""name"": ""shop-service"",
      ""network"": ""tcp"",
      ""address"": ""127.0.0.1:{shopPort}"",
      ""response"": {{
        ""qqt_login_success"": true,
        ""qqt_shop_catalog"": true,
        ""qqt_shop_type2_session"": true
      }}
    }},
    {{
      ""name"": ""game-udp"",
      ""network"": ""udp"",
      ""address"": ""127.0.0.1:{gamePort}"",
      ""response"": {{}}
    }}
  ]
}}";
                File.WriteAllText(cfgPath, cfgJson, new UTF8Encoding(false));

                serverProc = Process.Start(new ProcessStartInfo
                {
                    FileName = serverExe,
                    Arguments = $"-config \"{cfgPath}\"",
                    WorkingDirectory = tempDir,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                });
                var serverErr = new StringBuilder();
                serverProc.OutputDataReceived += (_, e) => { };
                serverProc.ErrorDataReceived += (_, e) => { if (e.Data != null) serverErr.AppendLine(e.Data); };
                serverProc.BeginOutputReadLine();
                serverProc.BeginErrorReadLine();

                // Wait for TCP listener to accept connections
                bool ready = false;
                for (int i = 0; i < 40; i++)
                {
                    if (serverProc.HasExited)
                        throw new Exception("qqt-server exited early: " + serverErr);
                    try
                    {
                        using (var probe = new TcpClient())
                        {
                            await probe.ConnectAsync("127.0.0.1", gamePort);
                            ready = true;
                            break;
                        }
                    }
                    catch { await Task.Delay(150); }
                }
                Assert(ready, "Timed out waiting for qqt-server to listen");

                using var c1 = new QqtangSessionClient();
                using var c2 = new QqtangSessionClient();

                // 3. Authenticate & Login both clients via QQTAUTH1 + 0x0064 + UDP Type-1
                var login1 = await c1.ConnectAndLoginAsync("127.0.0.1", gamePort, 1000001, "123456", roleId: 1);
                var login2 = await c2.ConnectAndLoginAsync("127.0.0.1", gamePort, 1000002, "123456", roleId: 2);
                Assert(login1.PlayerId > 0 && login2.PlayerId > 0 && login1.PlayerId != login2.PlayerId,
                    $"Invalid player IDs {login1.PlayerId}/{login2.PlayerId}");

                // Wait briefly for UDP Type-1 presence reply
                for (int i = 0; i < 20 && (!c1.UdpPresenceConfirmed || !c2.UdpPresenceConfirmed); i++)
                    await Task.Delay(50);
                Assert(c1.UdpPresenceConfirmed && c2.UdpPresenceConfirmed, "UDP Type-1 presence not confirmed");
                Pass($"Live Go Server Login + QQTAUTH1 + UDP Presence (P1={login1.PlayerId}, P2={login2.PlayerId})");

                // 4. Create room, select map 201 (中国城01), join, change team, ready, start
                ushort roomId = await c1.CreateRoomAsync("UnityTestRoom", RoomPropertyFlag.Standard, RoomGameType.CompetitiveNoItem);
                Assert(roomId > 0, "CreateRoom returned roomId=0");
                await c1.SelectMapAsync(roomId, 201, RoomGameType.CompetitiveNoItem);

                var rooms = await c2.RefreshRoomListAsync();
                Assert(rooms.Rooms.Any(r => r.RoomId == roomId), "Room not visible in RoomList");

                var enter2 = await c2.EnterRoomAsync(roomId, roleId: 2);
                Assert(enter2.ResultId == 0 && enter2.RoomId == roomId && enter2.Players.Count == 2,
                    $"EnterRoom failed: res={enter2.ResultId} count={enter2.Players.Count}");

                await c2.ChangeTeamAsync(roomId, 2);
                await c2.SetReadyAsync(roomId, true);
                ushort startRes = await c1.StartGameAsync(roomId);
                Assert(startRes == 0, $"StartGame rejected with 0x{startRes:X4}");

                // 5. Receive GameBeginNotify (0x0089) on both clients
                GameBeginData begin1 = await WaitForCommandAsync(c1, GameCommands.GameBeginNotify, p => GameBeginData.ParseLengthPrefixed(p.Payload));
                GameBeginData begin2 = await WaitForCommandAsync(c2, GameCommands.GameBeginNotify, p => GameBeginData.ParseLengthPrefixed(p.Payload));
                Assert(begin1.GameId == begin2.GameId && begin1.MapId == 201 && begin1.Players.Count == 2,
                    "GameBegin mismatch between clients");
                Pass($"Live Lobby -> Room({roomId}) -> StartGame -> GameBegin(GameID={begin1.GameId}, Map={begin1.MapId})");

                // 6. Exchange realtime QQTPPP Type-2 UDP movement + bomb placement
                var mapElems = MapDataLoader.LoadMapElementsPy(Path.Combine(clientPatchedRoot, "object", "mapElem", "mapElem.py"));
                string townMapFile = Path.Combine(clientPatchedRoot, "map", "town01.map");
                var mapData = File.Exists(townMapFile)
                    ? MapDataLoader.ParseMapBytes(201, File.ReadAllBytes(townMapFile), mapElems)
                    : MapDataLoader.BuildFallbackArena(201);

                var sim1 = new Rule1BattleSimulation();
                var sim2 = new Rule1BattleSimulation();
                sim1.Initialize(begin1, mapData, c1.PlayerId, false);
                sim2.Initialize(begin2, mapData, c2.PlayerId, false);
                sim1.Step(3000, BattleDirection.None, false);
                sim2.Step(3000, BattleDirection.None, false);

                sim1.Step(100, BattleDirection.Right, true);
                var peersForC1 = new List<LegacyUdpMulticastTarget>
                {
                    new LegacyUdpMulticastTarget { PlayerId = c2.PlayerId, Uin = c2.Uin }
                };
                await c1.SendPeerGameplayAsync(begin1.GameId, sim1.ElapsedMs, peersForC1, sim1.OutgoingPeerEvents, mirrorReliableTcp: true);

                // Verify C2 receives C1's movement & bomb over live QQTPPP UDP relay!
                GameplayBatch receivedBatch = null;
                for (int i = 0; i < 40; i++)
                {
                    if (c2.TryDequeueUdpBatch(out receivedBatch)) break;
                    await Task.Delay(50);
                }
                Assert(receivedBatch != null && receivedBatch.GameId == begin1.GameId && receivedBatch.Entries.Count == 1,
                    "C2 did not receive QQTPPP Type-2 UDP batch from C1");
                foreach (var msg in receivedBatch.Entries[0].Package.Messages)
                    sim2.ApplyRemoteMessage(receivedBatch.Entries[0].Package.PlayerId, msg.DataId, msg.Data);
                Assert(sim2.Bombs.Count == 1, "C2 simulation did not materialize C1's relayed bomb");
                Pass("Live QQTPPP Type-2 UDP Relay (PlayerMove + PlayerUseBomb synced C1 -> Go Server -> C2)");

                // 7. Report C2 death over TCP GameEvent (0x0083) -> verify Go server settles match with NotifyGameOver (0x0FBB)
                var death = new PlayerDeathMessage
                {
                    PlayerId = c2.PlayerId,
                    ClientTime = sim2.ElapsedMs,
                    PosX = (ushort)sim2.LocalActor.PosX,
                    PosY = (ushort)sim2.LocalActor.PosY
                };
                await c2.SendGameEventTcpAsync(roomId, GameEventSchemas.NotifyPlayerDie, death.Marshal());

                GameOverData over1 = await WaitForGameOverAsync(c1);
                GameOverData over2 = await WaitForGameOverAsync(c2);
                Assert(over1 != null && over2 != null && over1.Results.Count == 2, "Missing GameOver settlement");
                var p1Res = over1.Results.First(r => r.PlayerId == c1.PlayerId);
                var p2Res = over1.Results.First(r => r.PlayerId == c2.PlayerId);
                Assert(p1Res.Result == GameResultCode.Win && p2Res.Result == GameResultCode.Loss,
                    $"Unexpected settlement: P1={p1Res.Result} P2={p2Res.Result}");
                Pass($"Live Match Settlement (P1={p1Res.Result}, P2={p2Res.Result})");

                // 8. Live Room Chat, Seat Lock, ModifyRoom, ChangeRoomType, KickOffPlayer, QuickJoin
                c1.ShopPort = shopPort;
                c2.ShopPort = shopPort;
                var store1 = new ClientStateStore();
                store1.ApplyLogin(login1, c1.Uin, c1.Nickname, c1.Gender);

                ushort chatRes = await c1.SendRoomChatAsync(roomId, "HelloRoom");
                Assert(chatRes == 0, $"RoomChat rejected: {chatRes}");
                var rChat = await WaitForCommandAsync(c2, GameCommands.RoomChatNotify, p => RoomChatNotifyData.Parse(p.Payload));
                Assert(rChat.Content == "HelloRoom", $"RoomChat content mismatch: {rChat.Content}");

                Assert(await c1.SetSeatStatusAsync(roomId, 8, true) == 0, "SetSeatStatus lock failed");
                Assert(await c1.ModifyRoomAsync(roomId, "DaoistArena", RoomPropertyFlag.FreeRule, "", RoomGameType.CompetitiveNoItem, true, false) == 0, "ModifyRoom failed");
                Assert(await c1.ChangeRoomTypeAsync(roomId, RoomGameType.Chat) == 0, "ChangeRoomType to Chat failed");
                var bgs = await c1.GetBackgroundListAsync();
                Assert(bgs.BackgroundIds.Count > 0, "BackgroundList empty in chat room");
                Assert(await c1.UseBackgroundAsync(roomId, bgs.BackgroundIds[1]) == 0, "UseBackground failed");
                Assert(await c1.ChangeRoomTypeAsync(roomId, RoomGameType.CompetitiveNoItem) == 0, "ChangeRoomType restore failed");
                Assert(await c1.KickOffPlayerAsync(roomId, c2.PlayerId, c2.Uin) == 0, "KickOffPlayer failed");
                var kicked = await WaitForCommandAsync(c2, ExtendedGameCommands.KickOffRoomNotify, p => KickOffRoomNotifyData.Parse(p.Payload));
                Assert(kicked.PlayerUin == c2.Uin, "KickOffRoomNotify UIN mismatch");

                var qj = await c2.QuickJoinRoomAsync(RoomGameType.CompetitiveNoItem);
                Assert(qj.ResultId == 0 && qj.RoomId == roomId, $"QuickJoin failed: res={qj.ResultId}");
                await c2.LeaveRoomAsync(roomId);
                await c1.LeaveRoomAsync(roomId);
                Pass($"Live Room Extended (RoomChat, SeatLock, ModifyRoom, ChatRoom+Bg={bgs.BackgroundIds.Count}, Kick, QuickJoin)");

                // 9. Live Lobby PlayerList + SectionChat
                var pList = await c1.RefreshPlayerListAsync();
                Assert(pList.ResultId == 0 && pList.Players.Count >= 2, $"PlayerList expected >=2, got {pList.Players.Count}");
                Assert(await c1.SendSectionChatAsync("HelloLobby") == 0, "SectionChat failed");
                var sChat = await WaitForCommandAsync(c2, ExtendedGameCommands.SectionChatNotify, p => SectionChatNotifyData.Parse(p.Payload));
                Assert(sChat.Content == "HelloLobby", $"SectionChat mismatch: {sChat.Content}");
                Pass($"Live Lobby Extended (PlayerList={pList.Players.Count}, SectionChat)");

                // 10. Live Economy + Shop (auxiliary port) + BreakEgg + Pets
                var money = await c1.GetGameMoneyAsync();
                Assert(money.ResultId == 0 && money.Money > 0, $"GetGameMoney failed: {money.Money}");
                var shopList = await c1.QueryShopListAsync();
                Assert(shopList.ResultId == 0 && shopList.LatestShopListVersion > 0, $"ShopList failed: res={shopList.ResultId}");
                var bought = await c1.BuyCommodityAsync(294, ShopPaymentMethod.GameMoney, shopList.LatestShopListVersion);
                Assert(bought.ResultId == 0 && bought.ItemIds.Contains(294u), $"ShopBuy failed: res={bought.ResultId} msg={bought.ResultString}");
                var itemPush = await WaitForCommandAsync(c1, ExtendedGameCommands.PlayerItemAddNotify, p => PlayerItemAddNotifyData.Parse(p.Payload));
                store1.ApplyServerPacket(new DecodedLocalPacket { Command = ExtendedGameCommands.PlayerItemAddNotify, Payload = Array.Empty<byte>() });
                store1.UpsertInventoryItems(itemPush.Items);
                Assert(store1.OwnsItem(294), "Purchased item 294 not reflected in ClientStateStore");

                var eggRes = await c1.BreakEggAsync(9011, 9001);
                Assert(eggRes.ResultId == 0 && eggRes.ShowItemId > 0, $"BreakEgg failed: res={eggRes.ResultId}");
                var pets = await c1.RefreshPetsAsync();
                Assert(pets != null, "RefreshPets returned null");
                Pass($"Live Economy/Shop/Egg/Pets (ShopVer={shopList.LatestShopListVersion}, Bought=294, EggReward={eggRes.ShowItemId}, Pets={pets.Pets.Count})");

                // 11. Live Friends (Add -> Notify -> Answer -> FriendUpdate -> FindFriend -> Remove)
                await c1.AddFriendAsync(c2.Uin, "HiFriend");
                var fReq = await WaitForCommandAsync(c2, ExtendedGameCommands.AddFriend, p => AddFriendRequestNotifyData.Parse(p.Payload));
                Assert(fReq.RequesterUin == c1.Uin && fReq.Word == "HiFriend", "AddFriend notification mismatch");
                var fAns = await c2.AnswerFriendAsync(c1.Uin, true);
                Assert(fAns.ResultId == 0, $"AnswerFriend failed: {fAns.ResultId}");
                var fUpd = await WaitForCommandAsync(c1, ExtendedGameCommands.FriendUpdateNotify, p => FriendUpdateNotifyData.Parse(p.Payload));
                Assert(fUpd.FriendUin == c2.Uin && fUpd.Online, "FriendUpdateNotify mismatch");
                var found = await c1.FindFriendAsync(c2.Uin);
                Assert(found.ResultId == 0 && found.FriendUin == c2.Uin, $"FindFriend failed: {found.ResultId}");
                var fList = await c1.RefreshFriendsAsync();
                Assert(fList.FriendUins.Contains(c2.Uin), "FriendList missing C2");
                var rem = await c1.RemoveFriendAsync(c2.Uin);
                Assert(rem.ResultId == 0, $"RemoveFriend failed: {rem.ResultId}");
                Pass("Live Friends (Add, Answer, UpdateNotify, FindFriend, List, Remove)");

                // 12. Live Kin (Create -> FetchBase -> Invite C2 -> C2 Answer -> FetchMembers -> Declaration -> Notification -> Chat -> Top -> Kick C2 -> Dismiss)
                var kinCreate = await c1.CreateKinAsync("DaoistClan", "WayOfTao");
                Assert(kinCreate.ErrorNo == 0 && kinCreate.KinIndex > 0, $"CreateKin failed: err={kinCreate.ErrorNo} reason={kinCreate.Reason}");
                var kinBase = await c1.FetchKinBaseAsync(kinCreate.KinIndex);
                Assert(kinBase.Index == kinCreate.KinIndex && kinBase.Name == "DaoistClan", $"FetchKinBase mismatch: {kinBase.Name}");
                var inviteResp = await c1.InviteToKinAsync(kinCreate.KinIndex, c2.Uin);
                Assert(inviteResp.KinIndex == kinCreate.KinIndex, "InviteToKin response mismatch");
                var kinInvPush = await WaitForCommandAsync(c2, ExtendedGameCommands.ServerOperateKin, p => KinServerOperationData.Parse(p.Payload));
                Assert(kinInvPush.KinIndex == kinCreate.KinIndex && kinInvPush.InviterUin == c1.Uin, "C2 did not receive Kin invite");
                await c2.AnswerKinInviteAsync(c1.Uin, kinCreate.KinIndex, true);
                var kinJoinedEvent = await WaitForCommandAsync(c1, ExtendedGameCommands.KinEventNotify, p => KinEventNotifyData.Parse(p.Payload));
                Assert(kinJoinedEvent.EventType == KinConstants.EventMemberJoined && kinJoinedEvent.Uin == c2.Uin, "C1 did not receive Kin join event");
                var c2KinBase = await c2.FetchKinBaseAsync(kinCreate.KinIndex);
                Assert(c2KinBase.Index == kinCreate.KinIndex && c2KinBase.Name == "DaoistClan", "C2 FetchKinBase mismatch after joining");
                var kinMembers = await c1.FetchKinMembersAsync(kinCreate.KinIndex);
                Assert(kinMembers.Members.Count == 2, $"Expected 2 kin members after invite, got {kinMembers.Members.Count}");
                var declRes = await c1.SetKinDeclarationAsync(kinCreate.KinIndex, "NewDecl");
                Assert(declRes.Result == 0, $"SetKinDeclaration failed: {declRes.Result}");
                var notifRes = await c1.SetKinNotificationAsync(kinCreate.KinIndex, "WelcomeAll");
                Assert(notifRes.Result == 0, $"SetKinNotification failed: {notifRes.Result}");
                await c1.SendKinChatAsync(kinCreate.KinIndex, "HelloKin");
                var kChat = await WaitForCommandAsync(c2, ExtendedGameCommands.KinChatNotify, p => KinChatNotifyData.Parse(p.Payload));
                Assert(kChat.Message == "HelloKin", $"KinChat peer mismatch: {kChat.Message}");
                var kTop = await c1.FetchKinTopAsync(kinCreate.KinIndex);
                Assert(kTop.Result == 0, $"FetchKinTop failed: {kTop.Result}");
                var kickMember = await c1.KickKinMemberAsync(kinCreate.KinIndex, c2.Uin);
                Assert(kickMember.Result == 0, $"KickKinMember failed: {kickMember.Result}");
                var dismissRes = await c1.DismissKinAsync(kinCreate.KinIndex);
                Assert(dismissRes.Result == 0, $"DismissKin failed: {dismissRes.Result}");
                Pass($"Live Kin Full Flow (Create#{kinCreate.KinIndex}, Invite->Join, 2 Members, Decl, Notif, PeerChat, Top, Kick, Dismiss)");

                // 13. Live Marriage + Wedding Ceremony (ChatRoom Bg=5 + ChangeWeddingMode + StartWedding + AnswerWedding + Divorce)
                var mInit = await c1.GetMarriageInfoAsync();
                Assert(!mInit.IsMarried, "Should not be married initially");
                var sparkRes = await c1.RequestSparkAsync(c2.Uin, "MarryMe");
                Assert(sparkRes.Result == 0, $"RequestSpark failed: {sparkRes.Result}");
                var sparkPush = await WaitForCommandAsync(c2, ExtendedGameCommands.NotifySpark, p => SparkNotifyData.Parse(p.Payload));
                Assert(sparkPush.ProposerUin == c1.Uin && sparkPush.Word == "MarryMe", "NotifySpark mismatch");
                var ansSpark = await c2.AnswerSparkAsync(c1.Uin, true);
                Assert(ansSpark.Result == 0, $"AnswerSpark failed: {ansSpark.Result}");
                var mAfter = await c1.GetMarriageInfoAsync();
                Assert(mAfter.IsMarried && mAfter.MarriageUin == c2.Uin, "MarriageInfo should show C2 as spouse");
                var lw = await c1.ModifyLoveWordAsync(c2.Uin, "Forever");
                Assert(lw.Result == 0, $"ModifyLoveWord failed: {lw.Result}");
                var div = await c1.DivorceAsync(c2.Uin);
                Assert(div.Result == 0, $"Divorce failed: {div.Result}");
                Pass("Live Marriage (Info, Spark, Answer, LoveWord, Divorce)");

                // 14. Live Solo Adventure Mode Start (SinglePlayerAdventureCard 99 -> GameBegin + CreatePVENPCBoss 0x138A) & Clean Logout
                await c1.SetItemStatusAsync(SpecialItemIds.SinglePlayerAdventureCard, 1);
                ushort advRoomId = await c1.CreateRoomAsync("AdvSoloRoom", RoomPropertyFlag.Standard, RoomGameType.Adventure);
                var advEnter = await c1.EnterRoomAsync(advRoomId, roleId: 1);
                Assert(advEnter.ResultId == 0, $"Adventure EnterRoom failed: {advEnter.ResultId}");
                ushort advStart = await c1.StartGameAsync(advRoomId);
                Assert(advStart == 0, $"Solo Adventure StartGame failed: 0x{advStart:X4}");
                var advBegin = await WaitForCommandAsync(c1, GameCommands.GameBeginNotify, p => GameBeginData.ParseLengthPrefixed(p.Payload));
                Assert(advBegin.GameId > 0 && advBegin.Players.Count == 1, "Adventure GameBegin mismatch");
                await c1.LeaveRoomAsync(advRoomId);
                await c1.LogoutAsync();
                await c2.LogoutAsync();
                Pass($"Live Solo Adventure Start (Room={advRoomId}, GameID={advBegin.GameId}, Map={advBegin.MapId}) & Clean Logout");
            }
            finally
            {
                try { if (serverProc != null && !serverProc.HasExited) serverProc.Kill(true); } catch { }
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        private static async Task<T> WaitForCommandAsync<T>(QqtangSessionClient client, ushort command, Func<DecodedLocalPacket, T> parser)
        {
            for (int i = 0; i < 60; i++)
            {
                while (client.TryDequeueTcpPacket(out var pkt))
                {
                    if (pkt.Command == command) return parser(pkt);
                }
                await Task.Delay(50);
            }
            throw new TimeoutException($"Timed out waiting for command 0x{command:X4}.");
        }

        private static async Task<GameOverData> WaitForGameOverAsync(QqtangSessionClient client)
        {
            for (int i = 0; i < 60; i++)
            {
                while (client.TryDequeueTcpPacket(out var pkt))
                {
                    if (pkt.Command == GameCommands.GameEventNotify)
                    {
                        var ev = NotifyGameEventMessage.Parse(pkt.Payload);
                        if (ev.Schema == GameEventSchemas.NotifyGameOver)
                            return GameOverData.Parse(ev.Body);
                    }
                }
                await Task.Delay(50);
            }
            throw new TimeoutException("Timed out waiting for NOTIFY_GAME_OVER (0x0FBB).");
        }

        private static string EscapeJson(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
