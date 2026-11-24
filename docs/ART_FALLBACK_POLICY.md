# 美术替补策略与零图片入库规范

本仓库**不包含任何图片或二进制音视频媒体**（`.gitignore` 强制排除 `*.png`、`*.jpg`、`*.img`、`*.pkg`、`*.wav`、`*.ogg`、`*.map` 等），所有画面资产通过本地缓存与三级回退加载。

## 1. 三级美术回退链路（`ArtAssetResolver`）

Unity 运行时按固定优先级解析贴图：

1. **道家原创替补图（首选）**：来自 [luhailong-java/qqtang-daoist-remake](https://github.com/luhailong-java/qqtang-daoist-remake)（`codex/initial-public`）的新绘 PNG，存放于本地忽略目录 `client/Assets/StreamingAssets/ArtCache/daoist/`。
2. **原版客户端回退图（次选）**：对于尚未绘制完成的角色动作、特殊泡泡与长尾物品，由本地工具解码本机 `QQTang-Local/runtime/client-patched` 的 `.img` 第一帧至 `client/Assets/StreamingAssets/ArtCache/original/`。
3. **内置程序化 3D/纹理占位（兜底）**：全新克隆仓库、尚未准备任何本地图包时，客户端依然可直接编译、登录并打完一局。

## 2. 新画替补与原图解码现状（当前本地缓存共计 2,226 项零入库资产）

| 资产类别 | 道风新画覆盖（869 PNG + 21 WAV） | 原版 `.img` 解码兜底（1,336 PNG） |
|---|---|---|
| 地图元素 | 全部 19 个地图主题族（773 首帧 PNG 全覆盖） | `object.pkg` 缺失元素按 `elem<id>_stand.img` 自动解码 |
| 角色立绘/动作/被困 | 16 角色 256px 精绘立绘 + 4 主角 4 向/行走/胜利/失败 + 4 张被困气泡立绘 | 未覆盖角色动作平滑回退原图或四主角道风帧 |
| 变身与泡焰特效 | 8 种原版变身道风立绘 (`avatar-41..55`) + 多帧糖泡 (`bomb-1`/`bomb-4`) + 多帧火焰 (`flame10`) + 灵包 (`bun`) + 护盾/针/陷阱/香蕉/胶水 | 特殊糖泡装扮按 `bomb<id>_stand.img` 自动解码 |
| 结算徽章与 UI | 胜/负/平道风徽章 (`victory`/`defeat`/`draw`) + Wordmark + 32 道具图标 + 21 首 BGM/SFX | 聊天室背景 (`chat<id>_stand.img`)、灵宠精灵 (`pet<id>_stand.img`)、380 张地图缩略图 (`map/*.img`) 与长尾物品图标 (`item<id>_stand.img`) |

## 3. 本地一键准备图包与地图

在仓库根目录执行（纯 Python 标准库，无需安装 Pillow，默认已拉取全部 19 套道家图族 + 21 首音频 + 1,336 张原版 `.img` 解码与 377 份 `.map`）：

```powershell
# 方式 A：默认从 GitHub 拉取全部 19 套道家新画与音频，并解码本机原版客户端 .img 与地图
python tools/prepare-art-cache.py --original-client "C:\Users\luyua\Downloads\QQTang-Local\runtime\client-patched"

# 方式 B：如果你本机已克隆 qqtang-daoist-remake，可直接读本地目录零网络导出
python tools/prepare-art-cache.py --remake-dir "D:\path\to\qqtang-daoist-remake" --families all --original-client "C:\Users\luyua\Downloads\QQTang-Local\runtime\client-patched"
```
