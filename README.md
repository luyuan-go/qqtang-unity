# qqtang-unity

Unity 3D + 2D 纸片精灵客户端与原版协议兼容 Go 服务端（Monorepo）。

> **零图片入库声明**：本仓库仅保存服务端源码、Unity 客户端 C# 代码、配置与工具脚本，**不包含任何图片、音视频媒体或原始客户端二进制文件**（见 [.gitignore](.gitignore) 与 [docs/ART_FALLBACK_POLICY.md](docs/ART_FALLBACK_POLICY.md)）。

## 仓库结构

```text
qqtang-unity/
├── server/                       Go 原版协议兼容服务端（基于 kuuhaku1314/qqtang，Apache-2.0）
├── client/                       Unity 6000.6.0f1 3D + 2D Billboard 客户端工程
│   ├── Assets/Scripts/Core/      纯 C# 协议、状态存储、静态目录与规则 1 战斗核心（无 UnityEngine 依赖）
│   │   ├── Protocol/             QQTAUTH1 / QQ-TEA / LocalPacket / GameMessages / ExtendedGameMessages / QQTPPP UDP
│   │   ├── Network/              QqtangSessionClient（主连接 + 独立商城辅助连接 + UDP 实时同步）
│   │   ├── State/                ClientStateStore（全子系统统一状态源）& StaticCatalogs（地图/道具/配方/灵宠/商品目录）
│   │   └── Battle/               MapDataLoader（真实 .map v3/v4 解析）& Rule1BattleSimulation
│   ├── Assets/Scripts/Unity/     Unity 3D 渲染、道风 UGUI 全模块、AudioPlayer 与主控
│   │   ├── UI/                   UiFactory / LoginPanelView / LobbyAndNavView / RoomPanelView / InventoryShopCraftView / PetAndSocialView / BattleHudAndSettlementView
│   │   ├── BattleArena3DView.cs  3D 棋盘 + 2D Billboard 角色/泡泡/火焰/道具/护盾光环/表情气泡
│   │   ├── ArtAssetResolver.cs   三级美术回退（道风 PNG -> 原版 .img 解码 -> 程序化兜底）
│   │   └── QqtangGameApp.cs      全流程总控（自动引导加载）
│   └── CoreTests/                .NET 10 单元测试 + 真实 Go 服务端全子系统联机端到端验证（16 项）
├── tools/
│   ├── prepare-art-cache.py      本地拉取道家原创替补图 + 缺口解码原版 .img 回退
│   └── start-local-server.ps1    一键构建并启动本地 Go 服务端（含主游戏端口 18000 与商城端口 18001）
└── docs/
    └── ART_FALLBACK_POLICY.md    美术替补盘点与三级回退说明
```

## 已实现的全部逻辑与 UI 模块

- **零改服务端直连原生二进制协议**：
  - 认证与传输：`QQTAUTH1` PBKDF2-HMAC-SHA256、16 轮 `QQ-TEA`、`local_packet` / `RoomFastPacket`、`QQTPPP` UDP Type-1/2/3 与 `0x20`/`0x21` 握手。
  - 大厅与房间：登录 (`0x0064`) / 注销 (`0x0065`)、房间列表 (`0x0066`/`0x008C`)、在线玩家列表 (`0x006A`)、建房 (`0x006B`)、进房 (`0x0068`/`0x008D`)、快速加入 (`0x008A`)、离房 (`0x0069`/`0x008E`)、选图 (`0x0078`/`0x0079`)、改房间设置 (`0x0098`/`0x0099`)、切房间模式 (`0x00D0`/`0x00D1`)、锁座 (`0x007F`/`0x0080`)、踢人 (`0x006F`/`0x0074`)、换队 (`0x0070`/`0x007A`)、换角色 (`0x0071`/`0x007B`)、准备 (`0x0072`/`0x0073`/`0x007C`)、聊天室背景 (`0x00FF`/`0x0100`/`0x0101`)、开局 (`0x0082`/`0x0089`)。
  - 聊天系统：大厅聊天含小喇叭 (`0x006E`/`0x0092`)、房间聊天含系统消息 (`0x006D`/`0x0093`)、跨区聊天 (`0x006C`/`0x0095`)、家族聊天 (`0x00BE`/`0x00BF`)。
  - 经济与养成：糖币查询与推送 (`0x008B`/`0x0094`)、背包装备与道具同步 (`0x0085`/`0x00D9`)、独立商城端口目录与购买 (`0x035E`/`0x0259`)、配方合成 (`0x00CF`)、形象锻造 (`0x0353`)、砸彩蛋 (`0x00FE`)、灵宠列表与全 7 项操作 (`0x00D4`/`0x00D5`)。
  - 社交系统：好友列表/申请/审批/查找/删除 (`0x00A2`–`0x00A6`/`0x0067`)、家族创建/信息/成员/邀请/公告/聊天/排行/踢人/退出/解散 (`0x00B5`–`0x00CB`/`0x0140`/`0x0141`)、伴侣信息/求婚/应答/爱情宣言/离婚 (`0x0104`–`0x010C`)。
  - 规则 1 实时战斗：确定性 `nativeMapRNG` 出生点与隐藏道具、40px 网格与 `±19px` 碰撞/转角滑动、3 秒引信链式引爆、踢泡 (`0x0FB4`/`0x139C`)、推箱 (`0x0FB2`/`0x0FB3`)、扎针自救/护盾/遥控 (`0x0FAF`/`0x0FB0`)、复活 (`0x0FBA`)、表情气泡 (`0x1194`) 与真实服务端结算 (`0x0FBB`)。
- **完整道风 UGUI 界面**：登录与角色卡、顶部导航栏与大厅、8 座位房间与 326 地图搜索、背包/商城/锻造砸蛋、灵宠与好友/家族/伴侣社交面板、对局 HUD/暂停菜单/结算榜单，采用系统中文动态字体，零图片与零字体二进制入库。

## 快速开始

### 1. 运行全量自动化测试（16 项，含真实 Go 服务端双端联机 E2E）

```powershell
dotnet run --project client/CoreTests/Qqtang.Core.Tests.csproj
```

### 2. 准备本地美术缓存与地图（不入 Git）

```powershell
python tools/prepare-art-cache.py --families town,common,box,bomb --original-client "C:\Users\luyua\Downloads\QQTang-Local\runtime\client-patched"
```

### 3. 启动本地服务端并用 Unity 6000.6.0f1 运行客户端

```powershell
powershell -ExecutionPolicy Bypass -File tools/start-local-server.ps1
```

在 Unity Hub 中打开 `client/`，打开 `Assets/Scenes/Main.unity` 点击 Play，使用默认账号 `1000001` / `123456`（第二个客户端用 `1000002` / `123456`）连接 `127.0.0.1:18000` 即可体验全部大厅、社交、商城与 3D 联机对战流程。

### 4. 推送至 GitHub (`luyuan-go/qqtang-unity`)

在 GitHub `luyuan-go` 下新建空仓库 `qqtang-unity` 后执行：

```powershell
git push -u origin main
```
