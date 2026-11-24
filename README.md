# qqtang-unity

Unity 3D + 2D 纸片精灵客户端与原版协议兼容 Go 服务端（Monorepo）。

> **零图片入库声明**：本仓库仅保存服务端源码、Unity 客户端 C# 代码、配置与工具脚本，**不包含任何图片、音视频媒体或原始客户端二进制文件**（见 [.gitignore](.gitignore) 与 [docs/ART_FALLBACK_POLICY.md](docs/ART_FALLBACK_POLICY.md)）。

## 仓库结构

```text
qqtang-unity/
├── server/                 Go 原版协议兼容服务端（基于 kuuhaku1314/qqtang，Apache-2.0）
├── client/                 Unity 6000.6.0f1 3D + 2D Billboard 客户端工程
│   ├── Assets/Scripts/Core/    纯 C# 协议与规则 1 战斗核心（无 UnityEngine 依赖）
│   ├── Assets/Scripts/Unity/   Unity 3D 渲染、ArtAssetResolver 与联机界面
│   └── CoreTests/              .NET 10 单元测试 + 真实 Go 服务端联机端到端验证
├── tools/
│   ├── prepare-art-cache.py    本地拉取道家原创替补图 + 缺口解码原版 .img 回退
│   └── start-local-server.ps1  一键构建并启动本地 Go 服务端
└── docs/
    └── ART_FALLBACK_POLICY.md  美术替补盘点与三级回退说明
```

## 已实现的核心闭环（一期）

- **零改服务端直连原生二进制协议**：
  - `QQTAUTH1` PBKDF2-HMAC-SHA256 挑战响应登录鉴权
  - 16 轮 `QQ-TEA` 加解密与 `local_packet` / `RoomFastPacket` 包络
  - `0x0064` 登录、`0x0066` 房间列表、`0x006B` 建房、`0x0068` 进房、`0x0078` 选图、`0x0070` 换队、`0x0072` 准备、`0x0082` 开局、`0x0089` `GameBegin`
  - `QQTPPP` UDP Type-1 登记与 Type-2 加密批次实时同步（`0x0FBD` / `0x0FA2` 移动 / `0x0FA3` 放泡 / `0x0FA4` 爆炸 / `0x0FA5` 困泡 / `0x0FA7` 死亡 / `0x0FBB` 结算）
- **确定性规则 1 战斗逻辑**：移植 `nativeMapRNG`、原生出生点采样、隐藏道具洗牌、40px 网格与 `±19px` 碰撞/转角滑动、3 秒引信链式引爆与破墙掉落。
- **三级美术回退**：优先加载 [qqtang-daoist-remake](https://github.com/luhailong-java/qqtang-daoist-remake) 道家原创 PNG → 缺失项本地解码原版 `.img` 回退 → 程序化占位兜底。

## 快速开始

### 1. 运行自动化测试（含真实 Go 服务端双端联机 E2E）

```powershell
dotnet run --project client/CoreTests/Qqtang.Core.Tests.csproj
```

### 2. 准备本地美术缓存与地图（不入 Git）

```powershell
python tools/prepare-art-cache.py --families town,common,box,bomb --original-client "C:\Users\luyua\Downloads\QQTang-Local\runtime\client-patched"
```

### 3. 启动本地服务端并用 Unity 6000.6.0f1 打开客户端

```powershell
powershell -ExecutionPolicy Bypass -File tools/start-local-server.ps1
```

在 Unity Hub 中打开 `client/`，打开 `Assets/Scenes/Main.unity` 点击 Play，使用默认账号 `1000001` / `123456`（第二个客户端用 `1000002` / `123456`）连接 `127.0.0.1:18000` 即可开局对战。
