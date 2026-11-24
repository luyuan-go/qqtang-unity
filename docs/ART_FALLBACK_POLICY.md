# 美术替补策略与零图片入库规范

本仓库**不包含任何图片或二进制音视频媒体**（`.gitignore` 强制排除 `*.png`、`*.jpg`、`*.img`、`*.pkg`、`*.wav`、`*.ogg`、`*.map` 等），所有画面资产通过本地缓存与三级回退加载。

## 1. 三级美术回退链路（`ArtAssetResolver`）

Unity 运行时按固定优先级解析贴图：

1. **道家原创替补图（首选）**：来自 [luhailong-java/qqtang-daoist-remake](https://github.com/luhailong-java/qqtang-daoist-remake)（`codex/initial-public`）的新绘 PNG，存放于本地忽略目录 `client/Assets/StreamingAssets/ArtCache/daoist/`。
2. **原版客户端回退图（次选）**：对于尚未绘制完成的角色动作、特殊泡泡与长尾物品，由本地工具解码本机 `QQTang-Local/runtime/client-patched` 的 `.img` 第一帧至 `client/Assets/StreamingAssets/ArtCache/original/`。
3. **内置程序化 3D/纹理占位（兜底）**：全新克隆仓库、尚未准备任何本地图包时，客户端依然可直接编译、登录并打完一局。

## 2. 新画替补现状摘要（核对自 `qqtang-daoist-remake`）

| 资产类别 | 新画覆盖情况 | 当前回退策略 |
|---|---|---|
| 地图元素 | 773 / 773 首帧 PNG 已全覆盖（19 主题族） | 优先新画；缺动画的 25 项暂用首帧 |
| 核心主角 | 道童 / 月影 / 山岳 / 莲花 4 向待机与部分动作已齐 | 4 主角按 RoleID 映射新画，缺失动作回退原图或静态帧 |
| 泡泡与特效 | 基础泡泡、火焰 `flame1–10`、护盾已有新画 | 特殊糖泡装扮回退原图或基础新画 |
| 物品/图标 | 583 / 2,945 源物品已有库存图标；对局拾取物核心已齐 | 其余长尾物品回退原图 |

## 3. 本地一键准备图包与地图

在仓库根目录执行（纯 Python 标准库，无需安装 Pillow）：

```powershell
# 方式 A：直接从 GitHub 按需拉取核心道家新画（默认 town/common/box/bomb 四族）并同步本地地图与原图回退
python tools/prepare-art-cache.py --original-client "C:\Users\luyua\Downloads\QQTang-Local\runtime\client-patched"

# 方式 B：如果你本机已克隆 qqtang-daoist-remake，可直接读本地目录并导出全部 19 个地图族
python tools/prepare-art-cache.py --remake-dir "D:\path\to\qqtang-daoist-remake" --families all --original-client "C:\Users\luyua\Downloads\QQTang-Local\runtime\client-patched"
```
