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
                TestPhase4CodecsAndProgression();
                TestRule1SimulationMechanics();
                TestAuthenticBattleItemsAndTransformations();
                TestOfflineSoloAiAndBossMechanics();
                TestPhase5NativeModesAndCodecs();
                TestPhase6ArbitratorObjectivesAndBossAi();
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

            // Test extended mechanics: Needle self-rescue (63), Field Trap (41), Remote Relive (0x0FBA), Emotion (0x1194)
            sim.LocalActor.State = ActorLifeState.Trapped;
            sim.LocalActor.NeedleCount = 1;
            Assert(sim.TryUseWorldItem(63), "Needle self-rescue should succeed");
            Assert(sim.LocalActor.State == ActorLifeState.Active && sim.LocalActor.NeedleCount == 0, "Actor should be active after needle");
            sim.LocalActor.TrapCount = 1;
            Assert(sim.TryUseWorldItem(41), "Capture Trap placement should succeed");
            Assert(sim.FieldObjects.Count == 1 && sim.FieldObjects[0].ActionId == 41, "Field trap 41 should be placed on grid");
            sim.TriggerLocalEmotion(1000001, 5);
            Assert(sim.LocalActor.ActiveEmotionId == 5, "Emotion ID should be set");
            var reliveBody = new PlayerReliveEventData { PlayerId = 2, ClientTime = sim.ElapsedMs, Row = 1, Col = 1 }.Marshal();
            sim.GetActor(2).State = ActorLifeState.Dead;
            sim.ApplyRemoteMessage(2, ExtendedGameEventSchemas.NotifyPlayerRelive, reliveBody);
            Assert(sim.GetActor(2).State == ActorLifeState.Active, "Remote relive should revive actor");

            Pass("Rule-1 BattleSimulation (move, bomb, fuse, blast, needle, trap, relive, emote)");
        }

        private static void TestPhase4CodecsAndProgression()
        {
            var rec = AvatarRecoveryEventData.Parse(new AvatarRecoveryEventData { PlayerId = 2, Time = 9999, PosX = 140, PosY = 220 }.Marshal());
            Assert(rec.PlayerId == 2 && rec.Time == 9999 && rec.PosX == 140 && rec.PosY == 220, "AvatarRecovery roundtrip mismatch");

            var dbIn = new DispatchBombEventData { PlayerId = 1, Time = 5000 };
            dbIn.Bombs.Add((101u, 2, 3, 4));
            dbIn.Items.Add((24u, 5, 6));
            var dbOut = DispatchBombEventData.Parse(dbIn.Marshal());
            Assert(dbOut.Bombs.Count == 1 && dbOut.Bombs[0].row == 3 && dbOut.Items.Count == 1 && dbOut.Items[0].itemId == 24, "DispatchBomb roundtrip mismatch");

            var ieIn = new ItemsExplodedEventData { PlayerId = 1, Time = 5100 };
            ieIn.Items.Add((1u, 2, 3));
            var ieOut = ItemsExplodedEventData.Parse(ieIn.Marshal());
            Assert(ieOut.Items.Count == 1 && ieOut.Items[0].itemId == 1, "ItemsExploded roundtrip mismatch");

            var eat = EatBombActionData.Parse(new EatBombActionData { PlayerId = 1, Time = 123, BombPlayerId = 2, BombRow = 4, BombCol = 5 }.Marshal());
            Assert(eat.PlayerId == 1 && eat.BombRow == 4 && eat.BombCol == 5, "EatBomb roundtrip mismatch");

            var pfpRaw = new PassiveFunctionProjectionData { Rule = 2, PlayerId = 3, Effects = PassiveFunctionEffectMask.SwordArc | PassiveFunctionEffectMask.KickImpact, Reset = true }.Marshal();
            Assert(PassiveFunctionProjectionData.IsProjectionBody(pfpRaw), "FE Q F magic detection failed");
            var pfp = PassiveFunctionProjectionData.Parse(pfpRaw);
            Assert(pfp.Reset && pfp.PlayerId == 3 && (pfp.Effects & PassiveFunctionEffectMask.SwordArc) != 0, "PassiveFunctionProjection roundtrip mismatch");

            var kick = KickBombActionData.Parse(new KickBombActionData { FromRow = 2, FromCol = 3, DestRow = 2, DestCol = 7, Direction = 0, PlayerId = 1, BombPower = 3 }.Marshal());
            Assert(kick.FromRow == 2 && kick.FromCol == 3 && kick.DestCol == 7 && kick.BombPower == 3, "KickBombAction roundtrip mismatch");

            var prod = ProduceItemEventData.Parse(new ProduceItemEventData { Row = 4, Col = 9, ItemId = 85 }.Marshal());
            Assert(prod.Row == 4 && prod.Col == 9 && prod.ItemId == 85, "ProduceItem roundtrip mismatch");

            var wr = WrestleActionEventData.Parse(new WrestleActionEventData { SkillType = 2, FirstTeam = 1, PlayerPos = 3, PlayerId = 2, Time = 8888 }.Marshal());
            Assert(wr.SkillType == 2 && wr.PlayerId == 2, "WrestleAction roundtrip mismatch");

            var npcDrop = new NpcDropItemEventData { ObjectId = 30001, PosX = 100, PosY = 120 };
            npcDrop.Items.Add((83u, 3, 4));
            var npcDropOut = NpcDropItemEventData.Parse(npcDrop.Marshal());
            Assert(npcDropOut.ObjectId == 30001 && npcDropOut.Items.Count == 1 && npcDropOut.Items[0].itemId == 83, "NpcDropItem roundtrip mismatch");

            var tankHp = TankBaseHpEventData.Parse(new TankBaseHpEventData { PlayerId = 1, Time = 7000, TeamId = 2, ChangeHp = -250 }.Marshal());
            Assert(tankHp.TeamId == 2 && tankHp.ChangeHp == -250, "TankBaseHp roundtrip mismatch");

            // Exact 180-tier competitive & 31-tier adventure progression math
            var r0 = StaticCatalogs.GetCompetitiveRankInfo(0);
            var r6 = StaticCatalogs.GetCompetitiveRankInfo(10000);
            var a5 = StaticCatalogs.GetAdventureRankInfo(8000);
            Assert(r0.degree == 1 && r6.degree == 7 && a5.level == 5, $"Progression mismatch: r0={r0.degree} r6={r6.degree} a5={a5.level}");

            Pass("Phase 4 Protocol Codecs (0x0FBF/0x10E0-0x10E3/0x1159-0x116B/0x15B3) & 180-Tier/30-Tier Progression");
        }

        private static void TestAuthenticBattleItemsAndTransformations()
        {
            var map = MapDataLoader.BuildFallbackArena(201);
            var begin = new GameBeginData
            {
                GameId = 77, MapId = 201, SpawnSeed = 111, ItemSeed = 222, ArbitratorPlayerId = 1, GameTimeMs = 180_000,
                Players = new List<PlayerGameInfoData>
                {
                    new PlayerGameInfoData { PlayerId = 1, RoleId = 1, TeamId = 1 },
                    new PlayerGameInfoData { PlayerId = 2, RoleId = 2, TeamId = 2 }
                }
            };
            var sim = new Rule1BattleSimulation { IsOfflineSoloMode = true };
            sim.Initialize(begin, map, 1, false);
            sim.Step(3000, BattleDirection.None, false);

            // 1. Place Banana (42) & Glue (43) and verify contacts on actor 2
            var p1 = sim.LocalActor;
            var p2 = sim.GetActor(2);
            p1.GrantHeldAction(42, 1);
            p1.GrantHeldAction(43, 1);
            GridCell bananaCell = p1.CurrentCell;
            Assert(sim.TryUseWorldItem(42), "Banana placement should succeed");
            p2.PosX = bananaCell.Col * 40 + 20;
            p2.PosY = bananaCell.Row * 40 + 20;
            sim.Step(25, BattleDirection.None, false);
            Assert(p2.MovementStatus == MovementStatusKind.ForcedSlide, "Stepping on banana 42 must trigger ForcedSlide");

            // 2. Dart (44) projectile targeting active bomb 2 cells away in facing ray
            p1.Facing = BattleDirection.Right;
            p1.GrantHeldAction(44, 1);
            for (int dc = 1; dc <= 2; dc++)
            {
                int cc = Math.Min(map.Width - 1, p1.CurrentCell.Col + dc);
                var openTile = sim.Map.GetCell(p1.CurrentCell.Row, cc);
                openTile.Collision = CellCollision.Open;
                openTile.FlamePassable = true;
                sim.Map.Cells[p1.CurrentCell.Row * map.Width + cc] = openTile;
            }
            GridCell bombCell = new GridCell(p1.CurrentCell.Row, Math.Min(map.Width - 1, p1.CurrentCell.Col + 2));
            sim.Bombs.Add(new ActiveBomb { BombId = 500, OwnerId = 2, Cell = bombCell, Power = 1, PlacedAtMs = sim.ElapsedMs, ExplodeAtMs = sim.ElapsedMs + 3000 });
            Assert(sim.TryUseWorldItem(44) && sim.Projectiles.Count == 1, "Dart 44 should launch projectile toward bomb");
            sim.Step(1025, BattleDirection.None, false);
            Assert(sim.Projectiles.Count == 0 && sim.Bombs.Count == 0, "Dart projectile should detonate target bomb after 1000ms");

            // 3. Transformation 115 (Axe Gang -> grants 9 axes) & 110 (Demon -> reversed controls) + 30s expiry + 3s protection
            sim.Pickups.Add(new FieldPickup { SceneId = 115, Cell = p1.CurrentCell, Hidden = false });
            sim.Step(25, BattleDirection.None, false);
            Assert(p1.IsTransformed && p1.TransformationSceneId == 115 && p1.AxeCount == 9, "Transformation 115 must grant 9 axes");
            sim.Step(30_050, BattleDirection.None, false);
            Assert(!p1.IsTransformed && p1.AxeCount == 0 && p1.IsInvulnerable(sim.ElapsedMs), "Transformation expiry must strip axes and grant 3s protection");

            // 4. Arbitrator failover (0x0FB1) + TankBaseHp (0x15B3) + BossRaid / Sculpture offline scenarios
            var arbBuf = new BigEndianBuffer(2); arbBuf.WriteUInt16(2);
            sim.ApplyRemoteMessage(0, ModeEventSchemas.NotifyChangeArbitrator, arbBuf.ToArray());
            Assert(sim.ArbitratorPlayerId == 2, "Arbitrator should update to P2");
            sim.ApplyRemoteMessage(0, ModeEventSchemas.RequestTankBaseHp, new TankBaseHpEventData { PlayerId = 1, Time = sim.ElapsedMs, TeamId = 2, ChangeHp = -250 }.Marshal());
            Assert(sim.TankBaseHpByTeam[2] == 750, $"Expected TankBaseHp 750, got {sim.TankBaseHpByTeam[2]}");

            sim.ConfigureOfflineScenario(OfflinePracticeMode.BossRaid);
            Assert(sim.Bosses.Count == 1 && sim.Bosses[0].Hp == 2500, "BossRaid offline scenario should spawn 2500 HP boss");

            Pass("Authentic Battle Items (Trap/Banana/Glue/Dart/Transformations/Arbitrator/TankBase/OfflineBoss)");
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

        private static void TestPhase5NativeModesAndCodecs()
        {
            // 1. Machine World (0x10EF / 0x10F0..0x10F3 / 0x115D) & Box (0x115E..0x1168 / 0x0FBE) & Prop Cancel/Trigger (0x10F5..0x10F7) & DispatchItem (0x0FAE)
            var ml = new MachineLaunchEventData { FirstTeam = 1, RoleId = 2, PlayerId = 7, Time = 4500 };
            var ml2 = MachineLaunchEventData.Parse(ml.Marshal());
            Assert(ml2.PlayerId == 7 && ml2.RoleId == 2 && ml2.Time == 4500, "MachineLaunch round-trip failed");

            var mb = new MachineBombStateEventData { PlayerId = 7, Time = 5000 };
            mb.Bombs.Add((10u, 1, 4, 6));
            var mb2 = MachineBombStateEventData.Parse(mb.Marshal());
            Assert(mb2.Bombs.Count == 1 && mb2.Bombs[0].row == 4 && mb2.Bombs[0].col == 6, "MachineBombState round-trip failed");

            var push = new PushMapElementNotifyData { PlayerId = 7, Direction = 0, Power = 2, DestinationRow = 3, DestinationCol = 5, Time = 6000, Element = new byte[] { 3, 4, 0, 0, 0, 0, 7, 0xD2 } };
            var push2 = PushMapElementNotifyData.Parse(push.Marshal());
            Assert(push2.DestinationRow == 3 && push2.DestinationCol == 5 && push2.Element[1] == 4, "PushMapElementNotify round-trip failed");

            var cancel = new CancelUsePropEventData { PlayerId = 7, PropId = 20050, Time = 6100 };
            var cancel2 = CancelUsePropEventData.Parse(cancel.Marshal());
            Assert(cancel2.PropId == 20050, "CancelUseProp round-trip failed");

            var trig = new PropTriggerEventData { PropGuid = 99, GameTime = 6200, UserId = 7, PosX = 100, PosY = 100, Dir = 1, TypeId = 20050 };
            trig.AffectedPlayers.Add((8, 140, 140, 2));
            var trig2 = PropTriggerEventData.Parse(trig.Marshal());
            Assert(trig2.AffectedPlayers.Count == 1 && trig2.AffectedPlayers[0].playerId == 8 && trig2.TypeId == 20050, "PropTrigger round-trip failed");

            var disp = new DispatchItemEventData { Time = 6300 };
            disp.Items.Add(new DispatchedGameItem { ItemId = 24, Row = 2, Col = 3 });
            disp.DelayedItems.Add(new DispatchedGameItem { ItemId = 109, DispatchTime = 70_000, Row = 4, Col = 5 });
            var disp2 = DispatchItemEventData.Parse(disp.Marshal());
            Assert(disp2.Items.Count == 1 && disp2.Items[0].ItemId == 24 && disp2.DelayedItems.Count == 1 && disp2.DelayedItems[0].DispatchTime == 70_000, "DispatchItemEventData round-trip failed");

            // 2. Native Rule 7 (Machine World) -> 4-hit NativeDurability + MachineLaunch + MachineBombState
            var mMap = MapDataLoader.BuildFallbackArena(701);
            mMap.NativeRule = 7;
            var begin7 = new GameBeginData { GameId = 507, MapId = 701, SpawnSeed = 11, ItemSeed = 22, ArbitratorPlayerId = 1 };
            begin7.Players.Add(new PlayerGameInfoData { PlayerId = 1, RoleId = 1, TeamId = 1 });
            begin7.Players.Add(new PlayerGameInfoData { PlayerId = 2, RoleId = 2, TeamId = 2 });
            var sim7 = new Rule1BattleSimulation();
            sim7.Initialize(begin7, mMap, 1, false);
            Assert(sim7.Lifecycle == BattleLifecycleKind.NativeDurability && sim7.LocalActor.NativeHitPoints == 4, "Rule 7 should start with 4-hit NativeDurability");
            sim7.Step(3000, BattleDirection.None, false);
            Assert(sim7.TryTriggerModeSkill(), "Rule 7 mode skill should emit RequestLaunchMachine");
            Assert(sim7.OutgoingTcpEvents.Any(e => e.schemaId == ModeEventSchemas.RequestLaunchMachine), "Missing 0x10EF RequestLaunchMachine");
            sim7.ApplyRemoteMessage(2, ModeEventSchemas.NotifyMachineFree, mb.Marshal());
            Assert(sim7.Bombs.Count == 1 && sim7.Bombs[0].Cell.Equals(new GridCell(4, 6)), "NotifyMachineFree did not materialize machine bomb");
            sim7.ApplyRemoteMessage(2, ModeEventSchemas.PlayerBeHarmed, new EntityHarmedEventData { ObjectId = 1, Time = 3100, LossHp = 0 }.Marshal());
            Assert(sim7.LocalActor.NativeHitPoints == 3 && sim7.LocalActor.State == ActorLifeState.Active, "Rule 7 durability hit should reduce HP 4->3 without killing");

            // 3. Native Rule 2 (KickBomb) -> CanKickBomb on start + PlayerThrowBomb (0x1159)
            var kMap = MapDataLoader.BuildFallbackArena(202);
            kMap.NativeRule = 2;
            var sim2 = new Rule1BattleSimulation();
            sim2.Initialize(begin7, kMap, 1, false);
            Assert(sim2.LocalActor.CanKickBomb, "Rule 2 should grant CanKickBomb from start");
            sim2.Step(3000, BattleDirection.None, false);
            sim2.LocalActor.PosX = 20; sim2.LocalActor.PosY = 20; // Place deterministically at (0,0)
            sim2.Map.Cells[0 * sim2.Map.Width + 1].Collision = CellCollision.Open;
            sim2.Map.Cells[0 * sim2.Map.Width + 2].Collision = CellCollision.Open;
            sim2.Bombs.Add(new ActiveBomb { BombId = 99, OwnerId = 2, Cell = new GridCell(0, 1), Power = 2, PlacedAtMs = 3000, ExplodeAtMs = 6000 });
            Assert(sim2.TryKickAdjacentBomb(BattleDirection.Right), "Rule 2 kick failed");
            Assert(sim2.OutgoingTcpEvents.Any(e => e.schemaId == ModeEventSchemas.PlayerThrowBomb), "Rule 2 kick must emit 0x1159 PlayerThrowBomb");

            // 4. Native Rule 4 (Wrestle) -> 180s duration + TimedRespawn + Delayed Scheduled Pickups + Directional Exit Block
            var wMap = MapDataLoader.BuildFallbackArena(401);
            wMap.NativeRule = 4;
            var begin4 = new GameBeginData { GameId = 504, MapId = 401, SpawnSeed = 11, ItemSeed = 22, ArbitratorPlayerId = 1 };
            begin4.Players.Add(new PlayerGameInfoData { PlayerId = 1, RoleId = 1, TeamId = 1 });
            begin4.Players.Add(new PlayerGameInfoData { PlayerId = 2, RoleId = 2, TeamId = 2 });
            begin4.NewItems.Add(new GameItemTypeData { ItemId = 1, Quantity = 1 });
            begin4.Items.Add(new GameItemTypeData { ItemId = 24, Quantity = 1 });
            var sim4 = new Rule1BattleSimulation { IsOfflineSoloMode = true };
            sim4.Initialize(begin4, wMap, 1, false);
            Assert(sim4.RoundDurationMs == 180_000u && sim4.Lifecycle == BattleLifecycleKind.TimedRespawn, "Rule 4 should be 180s TimedRespawn");
            Assert(sim4.ScheduledPickups.Count == 1 && sim4.ScheduledPickups[0].DueMs >= 60_000u, "Delayed scheduled pickup not queued");
            sim4.LocalActor.PosX = 20; sim4.LocalActor.PosY = 20; // (0,0)
            sim4.Map.Cells[0].NativeGridAttr = 15 & ~1u; // Block Right exit from (0,0)
            sim4.Map.Cells[0].NativeGridAttrSet = true;
            sim4.Map.Cells[0].NativePlayerExitAttr = 15 & ~1u;
            sim4.Map.Cells[0].NativePlayerExitAttrSet = true;
            sim4.Step(3000, BattleDirection.None, false);
            int startX = sim4.LocalActor.PosX;
            for (int i = 0; i < 8; i++) sim4.Step(50, BattleDirection.Right, false);
            Assert(sim4.LocalActor.CurrentCell.Col == 0 && sim4.LocalActor.PosX >= startX, "Directional exit attr should block moving right out of (0,0)");
            sim4.LocalActor.State = ActorLifeState.Dead;
            sim4.LocalActor.RespawnAtMs = sim4.ElapsedMs + 100;
            sim4.Step(120, BattleDirection.None, false);
            Assert(sim4.LocalActor.State == ActorLifeState.Active, "TimedRespawn did not revive dead actor");
            Pass("Phase 5 Native Modes (Machine 0x10EF..0x115D, Box 0x115E..0x1168, Prop Cancel/Trigger, Rule 2 KickBomb, Rule 7 Durability, Rule 4 TimedRespawn, Directional Exit, Scheduled Supply)");
        }

        private static void TestPhase6ArbitratorObjectivesAndBossAi()
        {
            // 1. Online Rule 3 (Bun) -> RequestGetBun (0x0FB6) + RequestPutBun (0x0FB8) + Arbitrator NotifyGameOver (0x0FBB) at 3 points
            var bunMap = MapDataLoader.BuildFallbackArena(301);
            bunMap.NativeRule = 3;
            bunMap.ObjectiveCells.Add(new GridCell(0, 2));
            bunMap.Cells[0 * bunMap.Width + 2].Collision = CellCollision.Open;
            var bunBegin = new GameBeginData { GameId = 603, MapId = 301, SpawnSeed = 5, ItemSeed = 6, ArbitratorPlayerId = 1 };
            bunBegin.Players.Add(new PlayerGameInfoData { PlayerId = 1, RoleId = 1, TeamId = 1 });
            bunBegin.Players.Add(new PlayerGameInfoData { PlayerId = 2, RoleId = 2, TeamId = 2 });
            var bunSim = new Rule1BattleSimulation();
            bunSim.Initialize(bunBegin, bunMap, 1, false);
            bunSim.Step(3000, BattleDirection.None, false);
            bunSim.LocalActor.SpawnCell = new GridCell(0, 0);
            bunSim.LocalActor.PosX = 2 * 40 + 20; bunSim.LocalActor.PosY = 20; // walk onto bun at (0,2)
            bunSim.Step(25, BattleDirection.None, false);
            Assert(bunSim.BunCarrierPlayerId == 1 && bunSim.OutgoingTcpEvents.Any(e => e.schemaId == ModeEventSchemas.NotifyPlayerGetBun), "Arbitrator Rule 3 did not emit 0x0FB7 NotifyPlayerGetBun");
            bunSim.TeamBunScores[1] = 2; // 3rd deposit should trigger Arbitrator NotifyGameOver (0x0FBB)
            bunSim.LocalActor.PosX = 20; bunSim.LocalActor.PosY = 20; // return to home base (0,0)
            bunSim.Step(25, BattleDirection.None, false);
            Assert(bunSim.TeamBunScores[1] == 3, "Online Rule 3 deposit failed");
            Assert(bunSim.OutgoingTcpEvents.Any(e => e.schemaId == ModeEventSchemas.NotifyPlayerPutBun), "Arbitrator Rule 3 did not emit 0x0FB9 NotifyPlayerPutBun");
            Assert(bunSim.OutgoingTcpEvents.Any(e => e.schemaId == GameEventSchemas.NotifyGameOver), "Arbitrator did not emit 0x0FBB NotifyGameOver on Bun 3rd point");

            // Non-arbitrator client sends 0x0FB6 RequestGetBun, Arbitrator validates & broadcasts 0x0FB7 NotifyPlayerGetBun
            var peerBunSim = new Rule1BattleSimulation();
            peerBunSim.Initialize(bunBegin, bunMap, 2, false);
            peerBunSim.Step(3000, BattleDirection.None, false);
            peerBunSim.LocalActor.PosX = 2 * 40 + 20; peerBunSim.LocalActor.PosY = 20;
            peerBunSim.Step(25, BattleDirection.None, false);
            var reqGet = peerBunSim.OutgoingTcpEvents.FirstOrDefault(e => e.schemaId == ModeEventSchemas.RequestGetBun);
            Assert(reqGet.body != null, "Non-arbitrator Rule 3 did not emit 0x0FB6 RequestGetBun");
            bunSim.OutgoingTcpEvents.Clear();
            bunSim.ApplyRemoteMessage(2, ModeEventSchemas.RequestGetBun, reqGet.body);
            Assert(bunSim.BunCarrierPlayerId == 2 && bunSim.OutgoingTcpEvents.Any(e => e.schemaId == ModeEventSchemas.NotifyPlayerGetBun), "Arbitrator did not relay 0x0FB6 -> 0x0FB7");

            // 2. Online Rule 13 (Tank) -> flame on enemy base emits RequestTankBaseHp (0x15B3)
            var tankMap = MapDataLoader.BuildFallbackArena(1301);
            tankMap.NativeRule = 13;
            tankMap.ObjectiveCells.Add(new GridCell(1, 1));
            tankMap.ObjectiveCells.Add(new GridCell(1, 3));
            var tankSim = new Rule1BattleSimulation();
            tankSim.Initialize(bunBegin, tankMap, 1, false);
            tankSim.Step(3000, BattleDirection.None, false);
            tankSim.Flames.Add(new ActiveFlame { Cell = new GridCell(1, 3), OwnerId = 1, ExpireAtMs = 5000 });
            tankSim.Step(25, BattleDirection.None, false);
            Assert(tankSim.OutgoingTcpEvents.Any(e => e.schemaId == ModeEventSchemas.RequestTankBaseHp), "Online Rule 13 did not emit 0x15B3 RequestTankBaseHp");

            // 3. Boss Patrol AI & Offline MachineDodge / BoxPush / KickBomb setup
            var mSim = new Rule1BattleSimulation { IsOfflineSoloMode = true };
            mSim.Initialize(bunBegin, MapDataLoader.BuildFallbackArena(201), 1, false);
            mSim.ConfigureOfflineScenario(OfflinePracticeMode.MachineDodge);
            Assert(mSim.NativeRule == 7 && mSim.Lifecycle == BattleLifecycleKind.NativeDurability && mSim.LocalActor.NativeHitPoints == 4, "Offline MachineDodge init failed");
            mSim.Step(6200, BattleDirection.None, false);
            Assert(mSim.Bombs.Count > 0, "Offline MachineDodge did not spawn machine wave bombs");

            var bSim = new Rule1BattleSimulation { IsOfflineSoloMode = true };
            bSim.Initialize(bunBegin, MapDataLoader.BuildFallbackArena(201), 1, false);
            bSim.ConfigureOfflineScenario(OfflinePracticeMode.BossRaid);
            int bossStartX = bSim.Bosses[0].PosX, bossStartY = bSim.Bosses[0].PosY;
            bSim.Step(3000, BattleDirection.None, false);
            for (int i = 0; i < 10; i++) bSim.Step(50, BattleDirection.None, false);
            Assert(bSim.Bosses[0].PosX != bossStartX || bSim.Bosses[0].PosY != bossStartY, "Boss Patrol AI did not move toward player");
            Pass("Phase 6 Arbitrator GameOver + Online Bun(0x0FB6/0x0FB8) + TankBase(0x15B3) + Boss Patrol AI + Offline MachineDodge");
        }

        private static void TestStaticCatalogs()
        {
            string repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
            string serverDir = Path.Combine(repoRoot, "server");
            string localMapsDir = Path.Combine(repoRoot, "client", "Assets", "StreamingAssets", "LocalMaps");
            var catalogs = new StaticCatalogs();
            catalogs.LoadFromDirectories(localMapsDir, serverDir, @"C:\Users\luyua\Downloads\QQTang-Local\runtime\client-patched");
            Assert(catalogs.Maps.Count >= 300, $"Expected >=300 maps, got {catalogs.Maps.Count}");
            Assert(catalogs.ItemsById.Count >= 2000, $"Expected >=2000 items, got {catalogs.ItemsById.Count}");
            Assert(catalogs.CombineRecipes.Count >= 10, $"Expected >=10 combine recipes, got {catalogs.CombineRecipes.Count}");
            Assert(catalogs.PetsByTypeId.Count >= 10, $"Expected >=10 pet types, got {catalogs.PetsByTypeId.Count}");
            Assert(StaticCatalogs.GetRankTitle(1).Contains("QQ堂平民"), "Rank title mismatch");
            string mapDescPath = Path.Combine(localMapsDir, "mapDesc.py");
            if (File.Exists(mapDescPath))
            {
                var fileMap = MapDataLoader.LoadMapFilenamesFromMapDescPy(mapDescPath);
                Assert(fileMap.Count >= 300, $"Expected mapDesc.py filename map >= 300, got {fileMap.Count}");
                Assert(catalogs.AdventureMaps.Count >= 40, $"Expected >=40 PVE adventure maps, got {catalogs.AdventureMaps.Count}");
            }
            Pass($"StaticCatalogs (maps={catalogs.Maps.Count}, pve={catalogs.AdventureMaps.Count}, items={catalogs.ItemsById.Count}, recipes={catalogs.CombineRecipes.Count}, pets={catalogs.PetsByTypeId.Count}, shop={catalogs.ShopCommodities.Count})");
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

                // 9. Live Lobby PlayerList + SectionChat + AcrossSectionChat (0x006C/0x0095) + ConfigFileCheck (0x0266)
                var pList = await c1.RefreshPlayerListAsync();
                Assert(pList.ResultId == 0 && pList.Players.Count >= 2, $"PlayerList expected >=2, got {pList.Players.Count}");
                Assert(await c1.SendSectionChatAsync("HelloLobby") == 0, "SectionChat failed");
                var sChat = await WaitForCommandAsync(c2, ExtendedGameCommands.SectionChatNotify, p => SectionChatNotifyData.Parse(p.Payload));
                Assert(sChat.Content == "HelloLobby", $"SectionChat mismatch: {sChat.Content}");
                var cfgCount = await c1.CheckConfigFilesAsync();
                Assert(cfgCount == 0, $"ConfigFileCheck expected 0 outdated files, got {cfgCount}");
                var acrossRes = await c1.SendAcrossSectionChatAsync("HelloAcross", c2.Uin);
                Assert(acrossRes.ResultId == 0 && acrossRes.DestinationUin == c2.Uin, $"AcrossSectionChat failed: {acrossRes.ResultId}");
                var acrossPush = await WaitForCommandAsync(c2, ExtendedGameCommands.AcrossSectionChatNotify, p => AcrossSectionChatNotifyData.Parse(p.Payload));
                Assert(acrossPush.Content == "HelloAcross" && acrossPush.SourceUin == c1.Uin, $"AcrossSectionChatNotify mismatch: {acrossPush.Content}");
                Pass($"Live Lobby Extended (PlayerList={pList.Players.Count}, SectionChat, CrossWhisper, ConfigCheck)");

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
                // Verify Inventory Cabinet toggle (Status=2 collected off-switch -> Status=0)
                await c1.SetItemStatusAsync(294, 2);
                await c1.SetItemStatusAsync(294, 0);

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
                var titleRes = await c1.UpdateKinTitleAsync(kinCreate.KinIndex, "ImmortalHall");
                Assert(titleRes.Result == 0, $"UpdateKinTitle failed: {titleRes.Result}");
                var notifRes = await c1.SetKinNotificationAsync(kinCreate.KinIndex, "WelcomeAll");
                Assert(notifRes.Result == 0, $"SetKinNotification failed: {notifRes.Result}");
                var badgeRes = await c1.SetKinBadgeAsync(kinCreate.KinIndex, 7);
                Assert(badgeRes.Result == 0 && badgeRes.BadgeId == 7, $"SetKinBadge failed: res={badgeRes.Result}");
                var authUp = await c1.AssignKinAuthorityAsync(kinCreate.KinIndex, c2.Uin, 6);
                Assert(authUp.Result == 0 && authUp.AuthorityId == 6, $"AssignKinAuthority promote 4->6 failed: res={authUp.Result} {authUp.Reason}");
                var authDown = await c1.AssignKinAuthorityAsync(kinCreate.KinIndex, c2.Uin, 4);
                Assert(authDown.Result == 0 && authDown.AuthorityId == 4, $"AssignKinAuthority demote 6->4 failed: res={authDown.Result} {authDown.Reason}");
                await c1.SendKinChatAsync(kinCreate.KinIndex, "HelloKin");
                var kChat = await WaitForCommandAsync(c2, ExtendedGameCommands.KinChatNotify, p => KinChatNotifyData.Parse(p.Payload));
                Assert(kChat.Message == "HelloKin", $"KinChat peer mismatch: {kChat.Message}");
                var kTop = await c1.FetchKinTopAsync(kinCreate.KinIndex);
                Assert(kTop.Result == 0, $"FetchKinTop failed: {kTop.Result}");
                var kickMember = await c1.KickKinMemberAsync(kinCreate.KinIndex, c2.Uin);
                Assert(kickMember.Result == 0, $"KickKinMember failed: {kickMember.Result}");
                var dismissRes = await c1.DismissKinAsync(kinCreate.KinIndex);
                Assert(dismissRes.Result == 0, $"DismissKin failed: {dismissRes.Result}");
                Pass($"Live Kin Full Flow (Create#{kinCreate.KinIndex}, Invite->Join, 2 Members, Decl, Notif, Badge=7, Auth=4->6->4, PeerChat, Top, Kick, Dismiss)");

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

                // 14. Live Solo Adventure Multi-Stage Clear (CreatePVENPCBoss 0x138A -> open-cell spawn -> TCP pickup 0x0FAF -> Doorway auto-trigger -> NextMap 0x1178 -> Stage 2!)
                await c1.SetItemStatusAsync(SpecialItemIds.SinglePlayerAdventureCard, 1);
                ushort advRoomId = await c1.CreateRoomAsync("AdvSoloRoom", RoomPropertyFlag.Standard, RoomGameType.Adventure);
                var advEnter = await c1.EnterRoomAsync(advRoomId, roleId: 1);
                Assert(advEnter.ResultId == 0, $"Adventure EnterRoom failed: {advEnter.ResultId}");
                ushort advStart = await c1.StartGameAsync(advRoomId);
                Assert(advStart == 0, $"Solo Adventure StartGame failed: 0x{advStart:X4}");
                var advBegin = await WaitForCommandAsync(c1, GameCommands.GameBeginNotify, p => GameBeginData.ParseLengthPrefixed(p.Payload));
                Assert(advBegin.GameId > 0 && advBegin.Players.Count == 1, "Adventure GameBegin mismatch");

                byte[] pveBossBody = await WaitForGameEventSchemaAsync(c1, ModeEventSchemas.CreatePveNpcBoss);
                var advSim = new Rule1BattleSimulation();
                advSim.Initialize(advBegin, MapDataLoader.BuildFallbackArena(advBegin.MapId), c1.PlayerId, false);
                advSim.ApplyRemoteMessage(0, ModeEventSchemas.CreatePveNpcBoss, pveBossBody);
                Assert(advSim.Bosses.Count > 0, "CreatePveNpcBoss did not populate stage NPCs");
                Assert(advSim.Bosses.All(b => advSim.Map.GetCell(b.CurrentCell.Row, b.CurrentCell.Col).Collision == CellCollision.Open), "PVE NPC spawned on solid tile");
                advSim.Step(3000, BattleDirection.None, false);
                uint t = 4000;
                uint? sampleDropItem = null;
                foreach (var boss in advSim.Bosses)
                {
                    boss.Alive = false;
                    if (sampleDropItem == null && boss.DropItemIds.Count > 0) sampleDropItem = boss.DropItemIds[0];
                    var die = new PlayerDeathMessage { PlayerId = boss.ReportDeathId, ClientTime = t += 100, PosX = (ushort)boss.PosX, PosY = (ushort)boss.PosY };
                    await c1.SendGameEventTcpAsync(advRoomId, GameEventSchemas.NotifyPlayerDie, die.Marshal());
                }
                // Verify TCP NotifyPlayerGetItem (0x0FAF) pickup crediting & golden doorway auto-trigger
                advSim.Pickups.Add(new FieldPickup { SceneId = sampleDropItem ?? 1u, Cell = advSim.LocalActor.CurrentCell, Hidden = false });
                advSim.Step(25, BattleDirection.None, false);
                var advPickupEv = advSim.OutgoingTcpEvents.FirstOrDefault(e => e.schemaId == GameEventSchemas.NotifyPlayerGetItem);
                Assert(advPickupEv.body != null, "Adventure Arbitrator did not emit TCP 0x0FAF NotifyPlayerGetItem");
                await c1.SendGameEventTcpAsync(advRoomId, advPickupEv.schemaId, advPickupEv.body);
                Assert(advSim.AdventureDoorwayActive, "Adventure doorway did not activate after clearing all NPCs");
                advSim.LocalActor.PosX = advSim.AdventureDoorwayCell.Col * 40 + 20;
                advSim.LocalActor.PosY = advSim.AdventureDoorwayCell.Row * 40 + 20;
                advSim.Step(25, BattleDirection.None, false);
                Assert(advSim.AdventureDoorwayTriggered, "Walking into Adventure doorway did not auto-trigger NextMap");

                await Task.Delay(120);
                await c1.SendAdventureNextMapAsync(advRoomId, unchecked((uint)advBegin.ContinueId), 0, t + 500);
                byte[] nextMapBody = await WaitForGameEventSchemaAsync(c1, ModeEventSchemas.NotifyGameNextMap);
                var stage2Begin = GameBeginData.Parse(nextMapBody);
                Assert(stage2Begin.MapId != advBegin.MapId && stage2Begin.GameId != advBegin.GameId,
                    $"Adventure NextMap did not advance stage: stage1={advBegin.MapId} stage2={stage2Begin.MapId}");
                await c1.LeaveRoomAsync(advRoomId);
                Pass($"Live Solo Adventure Multi-Stage + Loot TCP + Doorway (Stage1={advBegin.MapId} NPCs={advSim.Bosses.Count} -> Stage2={stage2Begin.MapId} GameID={stage2Begin.GameId})");

                // 15. Live Competitive Boss Dungeon E2E on Map 11 (水手 sailorBoss: CreateNpcBoss 0x1389 -> Skill 0x1169 -> 5x Harm 0x10F4 + Drop 0x116B -> Kill 0x0FA7 -> Loot 0x0FAF)
                ushort bossRoomId = await c1.CreateRoomAsync("BossMap11", RoomPropertyFlag.Standard, RoomGameType.CompetitiveNoItem);
                Assert((await c1.EnterRoomAsync(bossRoomId, roleId: 1)).ResultId == 0, "C1 Enter Boss room failed");
                await c1.SelectMapAsync(bossRoomId, mapId: 11, gameType: RoomGameType.CompetitiveNoItem);
                Assert((await c2.EnterRoomAsync(bossRoomId, roleId: 2)).ResultId == 0, "C2 Enter Boss room failed");
                await c2.SetReadyAsync(bossRoomId, true);
                Assert(await c1.StartGameAsync(bossRoomId) == 0, "Boss Map 11 StartGame failed");
                var bossBegin = await WaitForCommandAsync(c1, GameCommands.GameBeginNotify, p => GameBeginData.ParseLengthPrefixed(p.Payload));
                byte[] createBossBody = await WaitForGameEventSchemaAsync(c1, ModeEventSchemas.CreateNpcBoss);
                var bossSim = new Rule1BattleSimulation();
                bossSim.Initialize(bossBegin, MapDataLoader.BuildFallbackArena(bossBegin.MapId), bossBegin.ArbitratorPlayerId, false);
                bossSim.ApplyRemoteMessage(0, ModeEventSchemas.CreateNpcBoss, createBossBody);
                Assert(bossSim.Bosses.Count == 1 && bossSim.Bosses[0].Hp == 5 && bossSim.Bosses[0].Skills.Count > 0,
                    $"Competitive Boss Map 11 did not spawn Sailor(HP=5, Skills>0): count={bossSim.Bosses.Count}");
                var arbClient = bossBegin.ArbitratorPlayerId == c1.PlayerId ? c1 : c2;
                bossSim.Step(6000, BattleDirection.None, false); // triggers Boss Skill 0x1169
                var skillEv = bossSim.OutgoingTcpEvents.FirstOrDefault(e => e.schemaId == ModeEventSchemas.NotifyNpcUseSkill);
                Assert(skillEv.body != null, "Competitive Boss did not cast server skill 0x1169");
                await arbClient.SendGameEventTcpAsync(bossRoomId, skillEv.schemaId, skillEv.body);
                // 5 flame hits -> 4x non-lethal PlayerBeHarmed (0x10F4) + 5th lethal NotifyPlayerDie (0x0FA7)
                int harmCount = 0;
                bool bossDiedSent = false;
                for (int hit = 0; hit < 5; hit++)
                {
                    bossSim.Step(650, BattleDirection.None, false);
                    var bCell = bossSim.Bosses[0].CurrentCell;
                    bossSim.Flames.Add(new ActiveFlame { Cell = bCell, OwnerId = bossBegin.ArbitratorPlayerId, ExpireAtMs = bossSim.ElapsedMs + 500 });
                    bossSim.Step(25, BattleDirection.None, false);
                    foreach (var ev in bossSim.OutgoingTcpEvents)
                    {
                        if (ev.schemaId == ModeEventSchemas.PlayerBeHarmed) harmCount++;
                        if (ev.schemaId == GameEventSchemas.NotifyPlayerDie) bossDiedSent = true;
                        await arbClient.SendGameEventTcpAsync(bossRoomId, ev.schemaId, ev.body);
                    }
                }
                Assert(harmCount == 5 && bossDiedSent && !bossSim.Bosses[0].Alive, $"Sailor Boss should take exactly 5 hits (got harm={harmCount}, died={bossDiedSent})");
                var bossOver = await WaitForGameOverAsync(c1, maxPolls: 280);
                Assert(bossOver.Results.Count == 2 && bossOver.Results.All(r => r.Result == GameResultCode.Win), "Boss Victory GameOver should mark both players Win");
                await c1.LeaveRoomAsync(bossRoomId);
                await c2.LeaveRoomAsync(bossRoomId);
                await c1.LogoutAsync();
                await c2.LogoutAsync();
                Pass($"Live Competitive Boss Dungeon Map 11 (Sailor HP=5, Skill=0x1169, 5x Harm=0x10F4, Kill=0x0FA7 -> Co-op Win GameOver) & Clean Logout");
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

        private static async Task<byte[]> WaitForGameEventSchemaAsync(QqtangSessionClient client, ushort schema)
        {
            for (int i = 0; i < 60; i++)
            {
                while (client.TryDequeueTcpPacket(out var pkt))
                {
                    if (pkt.Command == GameCommands.GameEventNotify)
                    {
                        var ev = NotifyGameEventMessage.Parse(pkt.Payload);
                        if (ev.Schema == schema) return ev.Body;
                    }
                }
                await Task.Delay(50);
            }
            throw new TimeoutException($"Timed out waiting for GameEvent schema 0x{schema:X4}.");
        }

        private static async Task<GameOverData> WaitForGameOverAsync(QqtangSessionClient client, int maxPolls = 60)
        {
            for (int i = 0; i < maxPolls; i++)
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
