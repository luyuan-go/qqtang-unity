# qqtang-unity

Unity 3D + 2D 纸片精灵客户端与原版协议兼容 Go 服务端（Monorepo）。

> **零图片入库声明**：本仓库仅保存服务端源码、Unity 客户端 C# 代码、配置与工具脚本，**不包含任何图片、音视频媒体或原始客户端二进制文件**（见 [.gitignore](.gitignore) 与 [docs/ART_FALLBACK_POLICY.md](docs/ART_FALLBACK_POLICY.md)）。

## 仓库结构

```text
qqtang-unity/
├── server/                       Go 原版协议兼容服务端（基于 kuuhaku1314/qqtang，Apache-2.0）
├── client/                       Unity 6000.6.0f1 3D + 2D Billboard 客户端工程
│   ├── Assets/Scripts/Core/      纯 C# 协议、状态存储、静态目录与规则 1 战斗核心（无 UnityEngine 依赖）
│   │   ├── Protocol/             QQTAUTH1 / QQ-TEA / LocalPacket / GameMessages / ExtendedGameMessages / ModeAndBossMessages / QQTPPP UDP
│   │   ├── Network/              QqtangSessionClient（主连接 + 独立商城辅助连接 + UDP 实时同步）
│   │   ├── State/                ClientStateStore（全子系统统一状态源）& StaticCatalogs（地图/道具/配方/灵宠/商品目录）
│   │   └── Battle/               MapDataLoader（真实 .map v3/v4 解析）、Rule1BattleSimulation & LocalBattleAiBot（离线 AI）
│   ├── Assets/Scripts/Unity/     Unity 3D 渲染、道风 UGUI 全模块、AudioPlayer（BGM+SFX）与主控
│   │   ├── UI/                   UiFactory / LoginPanelView / LobbyAndNavView / RoomPanelView / InventoryShopCraftView / PetAndSocialView / BattleHudAndSettlementView
│   │   ├── BattleArena3DView.cs  3D 棋盘 + 2D Billboard 角色/树冠覆盖层/变身光效/装扮糖泡皮肤/陷阱香蕉胶水/飞镖斧头弹道/Boss血条/基地血柱/灵宠精灵伴飞
│   │   ├── ArtAssetResolver.cs   三级美术回退（道风 19 图集+图标+Wordmark -> 原版 object.pkg/pet/bomb/chatBg .img 解码 -> 程序化兜底）
│   │   └── QqtangGameApp.cs      全流程总控（自动引导加载、联机 + 离线单机多模式多地图演武）
│   ├── Assets/Editor/            HeadlessBuilder.cs（命令行无弹窗构建独立 Windows .exe）
│   └── CoreTests/                .NET 10 单元测试 + 真实 Go 服务端全子系统联机端到端验证（21 项）
├── tools/
│   ├── prepare-art-cache.py      本地拉取全部 19 套道家替补图/32 图标/21 首音频 + 959 张原版 .img 解码兜底 + 379 地图与目录同步
│   ├── build-unity-client.ps1    一键无界面构建独立 Windows 客户端 (local-runtime/build/QqtangDaoistClient.exe)
│   └── start-local-server.ps1    一键构建并启动本地 Go 服务端（含主游戏端口 18000 与商城端口 18001）
└── docs/
    └── ART_FALLBACK_POLICY.md    美术替补盘点与三级回退说明
```

## 已实现的全部逻辑与 UI 模块

- **零改服务端直连原生二进制协议**：
  - 认证与传输：`QQTAUTH1` PBKDF2-HMAC-SHA256、16 轮 `QQ-TEA`、`local_packet` / `RoomFastPacket`、`QQTPPP` UDP Type-1/2/3 与 `0x20`/`0x21` 握手。
  - 大厅与房间：登录 (`0x0064`) / 注销 (`0x0065`)、房间列表 (`0x0066`/`0x008C`)、在线玩家列表 (`0x006A`)、建房 (`0x006B`)、进房 (`0x0068`/`0x008D`)、快速加入 (`0x008A`)、离房 (`0x0069`/`0x008E`)、选图 (`0x0078`/`0x0079`)、改房间设置 (`0x0098`/`0x0099`)、切房间模式 (`0x00D0`/`0x00D1`)、锁座 (`0x007F`/`0x0080`)、踢人 (`0x006F`/`0x0074`)、换队 (`0x0070`/`0x007A`)、换角色 (`0x0071`/`0x007B`)、准备 (`0x0072`/`0x0073`/`0x007C`)、聊天室背景 (`0x00FF`/`0x0100`/`0x0101`)、房间道具使用 (`0x0083`)、开局 (`0x0082`/`0x0089`)。
  - 聊天系统：大厅聊天含小喇叭 (`0x006E`/`0x0092`)、房间与局内迷你聊天 (`0x006D`/`0x0093`)、跨区私聊 (`0x006C`/`0x0095`)、家族聊天 (`0x00BE`/`0x00BF`)。
  - 经济与养成：糖币查询与推送 (`0x008B`/`0x0094`)、配置版本校验 (`0x0266`)、背包装备/收藏柜开关 (`Status=2` 停用被动卡) 与道具同步 (`0x0085`/`0x00D9`)、独立商城端口目录与购买 (`0x035E`/`0x0259`)、配方合成 (`0x00CF`)、形象锻造 (`0x0353`)、砸彩蛋 (`0x00FE`)、隐藏藏宝室进出与拾取 (`0x00AB`–`0x00AE`)、灵宠列表（含原生精灵头像）与全 7 项操作 (`0x00D4`/`0x00D5`)。
  - 社交系统：好友列表/申请/审批/查找/删除 (`0x00A2`–`0x00A6`/`0x0067`)、家族创建/信息/成员/邀请/宣言/公告/5 级职级称号 (`0x00B8`/`0x00C6`)/族徽/职级单步升降/聊天/全服排行/踢人/退出/解散 (`0x00B5`–`0x00CB`/`0x0140`/`0x0141`)、伴侣信息/求婚/应答/爱情宣言/离婚与中式/西式婚礼仪式 (`0x0104`–`0x010E`)。
  - 全部 9 大原版战斗规则与实时推演：覆盖全部 330 张竞技地图与 50 关探险地图（直读 `mapDesc.py` 与 379 份原版 `.map` 二进制）、方向退出碰撞属性 (`NativePlayerExitAttr`)、定时补给空投 (`ScheduledPickups` + `0x0FAE`)、全 9 种原生规则生命周期与时长（Rule 1 普通、Rule 2 踢泡 `0x1159` + 被动特效 `FE Q F`、Rule 3 抢包山 `0x0FB6`–`0x0FB9`、Rule 4 比武摔跤 `0x115B`/`0x115C` 180s、Rule 5 夺宝 180s、Rule 6 雕像、Rule 7 机械世界 4 血耐久 + `0x10EF`–`0x10F3`/`0x115D`、Rule 8 推箱子 4 血耐久 + `0x115E`–`0x1168`/`0x0FBE`、Rule 13 坦克基地 `0x15B3`）、规则 3/4/5/6/13 自动定时复活 (`0x0FBA`)、原版 7 槽局内道具（扎针 63 / 捕兽陷阱 41 / 香蕉皮强滑 42 / 胶水减速 43 / 飞镖与斧头远程引爆 44·46 / 遥控 64）、战备消耗品触发与取消 (`0x1174`/`0x1175`/`0x10F5`–`0x10F7`)、全部 8 种变身及 3 秒护盾与恢复 (`0x10E0`)、阵亡道具散落、仲裁者切换与中途离场 (`0x0FB1`/`0x10EC`)、竞技/PVE BOSS (`0x1389`/`0x138A`/`0x10F4`/`0x1169`–`0x116B`)、探险多关切图 (`0x1177`/`0x1178`) 与明细结算 (`0x0FBB`)。
- **零服务端离线单机演武**：登录页可直接选择地图（包子01/水面01/英雄01/抢包山01）与玩法（**离线 1v1 竞技**、**离线 1v3 混战**、**单人 BOSS 挑战**、**抢包山**、**雕塑攻防**），由 `LocalBattleAiBot` 执行避险/吃道具/追击救援与安全放泡，无需启动 Go 服务端即可试玩全部局内道具与变身。
- **完整道风 UGUI + 音频系统**：道风 Wordmark、角色卡、道具缩略图、灵宠精灵头像、聊天室背景横幅、顶部导航栏（含 **「战绩」精确 180 级竞技段位 + 30 级探险等级双进度条档案弹窗** 与 **「设置」音画/镜头缩放/资源统计/键位弹窗**）、大厅（含跨区私聊）、8 座位房间与 330 竞技 + 50 探险双模式地图搜索、背包（含收藏柜开关）/商城/锻造砸蛋、灵宠与好友/家族治理排行/伴侣婚礼面板、7 键战斗道具与模式绝技 HUD（`1`–`6` + `F` 绝技/战备 + `C` 取消 + `E` 表情，含规则标签、耐久生命、被动特效、变身倒计时、Boss 台词/血量、探险切关入口与 `Enter` 局内迷你聊天）、隐藏藏宝室弹窗、暂停菜单、明细结算榜单，以及大厅/战斗双循环 BGM 与完整 21 首原版道风音效（本地缓存，零媒体入库）。

## 快速开始

### 1. 运行全量自动化测试（21 项，含全 9 规则/道具变身单测与真实 Go 服务端双端联机 E2E）

```powershell
dotnet run --project client/CoreTests/Qqtang.Core.Tests.csproj
```

### 2. 准备本地美术、21 首音频、图标、地图与目录缓存（均不入 Git）

```powershell
python tools/prepare-art-cache.py
```

### 3. 启动本地服务端并用 Unity 6000.6.0f1 运行客户端（或免服务端直接离线演武）

```powershell
powershell -ExecutionPolicy Bypass -File tools/start-local-server.ps1
```

在 Unity Hub 中打开 `client/`，打开 `Assets/Scenes/Main.unity` 点击 Play：
- 联机：使用默认账号 `1000001` / `123456`（第二个客户端用 `1000002` / `123456`）连接 `127.0.0.1:18000`；
- 单机：直接点击登录卡片上的 **「离线 1v1 演武」** 或 **「离线 1v3 混战」**。

### 4. 一键无界面构建独立 Windows 64 位客户端 `.exe`

```powershell
powershell -ExecutionPolicy Bypass -File tools/build-unity-client.ps1
```

产物输出至 gitignored 的 `local-runtime/build/QqtangDaoistClient.exe`。

### 5. 推送至 GitHub (`luyuan-go/qqtang-unity`)

在 GitHub `luyuan-go` 下新建空仓库 `qqtang-unity` 后执行：

```powershell
git push -u origin main
```
