#!/usr/bin/env python3
"""Prepare local Unity StreamingAssets art & map cache (ZERO images committed to Git).

Priority order for every visual asset:
  1. New Daoist-chibi replacement PNG from luhailong-java/qqtang-daoist-remake
     (local clone path or GitHub raw URL).
  2. Original QQTang client .img / data/object.pkg fallback decoded locally from QQTang-Local.
  3. Unity runtime procedural 3D/texture fallback if neither is cached locally.

All outputs go to gitignored directories:
  - client/Assets/StreamingAssets/ArtCache/
  - client/Assets/StreamingAssets/LocalMaps/
"""

from __future__ import annotations

import argparse
from concurrent.futures import ThreadPoolExecutor, as_completed
import json
import re
import shutil
import struct
import sys
import urllib.request
import zlib
from pathlib import Path
from typing import Dict, List, Optional, Tuple

REMAKE_RAW_BASE = (
    "https://raw.githubusercontent.com/"
    "luhailong-java/qqtang-daoist-remake/codex/initial-public"
)

CORE_DAOIST_ASSETS: Dict[str, str] = {
    "characters/role_0_S": "assets/characters/idle/v1/daoist-child-S.png",
    "characters/role_0_N": "assets/characters/idle/v1/daoist-child-N.png",
    "characters/role_0_E": "assets/characters/idle/v1/daoist-child-E.png",
    "characters/role_0_W": "assets/characters/idle/v1/daoist-child-W.png",
    "characters/role_1_S": "assets/characters/idle/v1/moon-shadow-S.png",
    "characters/role_1_N": "assets/characters/idle/v1/moon-shadow-N.png",
    "characters/role_1_E": "assets/characters/idle/v1/moon-shadow-E.png",
    "characters/role_1_W": "assets/characters/idle/v1/moon-shadow-W.png",
    "characters/role_2_S": "assets/characters/idle/v1/mountain-guardian-S.png",
    "characters/role_2_N": "assets/characters/idle/v1/mountain-guardian-N.png",
    "characters/role_2_E": "assets/characters/idle/v1/mountain-guardian-E.png",
    "characters/role_2_W": "assets/characters/idle/v1/mountain-guardian-W.png",
    "characters/role_3_S": "assets/characters/idle/v1/lotus-healer-S.png",
    "characters/role_3_N": "assets/characters/idle/v1/lotus-healer-N.png",
    "characters/role_3_E": "assets/characters/idle/v1/lotus-healer-E.png",
    "characters/role_3_W": "assets/characters/idle/v1/lotus-healer-W.png",
    "effects/flame": "assets/effects/flames/flame10/00.png",
    "effects/bubble": "assets/effects/bubbles/00.png",
    "effects/shield": "assets/effects/protection-bubble-v1.png",
    "pickups/scene_1": "assets/runtime/battle-props-20261112/capacity.png",
    "pickups/scene_2": "assets/runtime/battle-props-20261112/range.png",
    "pickups/scene_3": "assets/runtime/battle-props-20261112/speed.png",
    "pickups/scene_6": "assets/runtime/battle-props-20261112/capacity.png",
    "pickups/scene_7": "assets/runtime/battle-props-20261112/range.png",
    "pickups/scene_8": "assets/runtime/battle-props-20261112/speed.png",
    "ui/logo": "assets/branding/linghu-wordmark-login-1024.png",
}


def encode_rgba_png(width: int, height: int, rgba: bytes) -> bytes:
    """Minimal stdlib PNG encoder (RGBA8) so no Pillow dependency is required."""
    raw = bytearray()
    stride = width * 4
    for y in range(height):
        raw.append(0)
        start = y * stride
        raw.extend(rgba[start : start + stride])

    def chunk(tag: bytes, data: bytes) -> bytes:
        crc = zlib.crc32(tag + data) & 0xFFFFFFFF
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", crc)

    ihdr = struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)
    idat = zlib.compress(bytes(raw), level=6)
    return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr) + chunk(b"IDAT", idat) + chunk(b"IEND", b"")


def decode_dimg_first_frame_to_png(data: bytes) -> Optional[bytes]:
    """Decode the first visible frame of a QQF\\x1ADIMG container to RGBA PNG."""
    if len(data) < 40 or data[:8] != b"QQF\x1aDIMG":
        return None
    version, frame_info_size, frame_count, _directions = struct.unpack_from("<4I", data, 8)
    if version not in (65536, 65537) or frame_info_size != 24 or frame_count < 1:
        return None

    cursor = 40
    for _ in range(frame_count):
        if cursor + 16 > len(data):
            return None
        marker, _x, _y, mode = struct.unpack_from("<IiiI", data, cursor)
        cursor += 16
        if marker != 0:
            return None
        if mode == 0:
            continue
        if cursor + 12 > len(data):
            return None
        width, height, _unk = struct.unpack_from("<3I", data, cursor)
        cursor += 12
        if width == 0 or height == 0 or width > 4096 or height > 4096:
            continue
        count = width * height
        if mode in (3, 0x11000000):
            payload_len = count * 3
            if cursor + payload_len > len(data):
                return None
            payload = data[cursor : cursor + payload_len]
            rgba = bytearray(count * 4)
            for i in range(count):
                val = payload[2 * i] | (payload[2 * i + 1] << 8)
                alpha = min(32, payload[count * 2 + i])
                rgba[4 * i] = ((val >> 11) & 31) * 255 // 31
                rgba[4 * i + 1] = ((val >> 5) & 63) * 255 // 63
                rgba[4 * i + 2] = (val & 31) * 255 // 31
                rgba[4 * i + 3] = (alpha * 255 + 16) // 32
            return encode_rgba_png(width, height, bytes(rgba))
        if mode == 8:
            payload_len = count * 4
            if cursor + payload_len > len(data):
                return None
            payload = data[cursor : cursor + payload_len]
            rgba = bytearray(count * 4)
            for i in range(count):
                b, g, r, a = payload[4 * i : 4 * i + 4]
                rgba[4 * i : 4 * i + 4] = bytes((r, g, b, a))
            return encode_rgba_png(width, height, bytes(rgba))
        if mode == 16:
            payload_len = count * 3
            if cursor + payload_len > len(data):
                return None
            payload = data[cursor : cursor + payload_len]
            rgba = bytearray(count * 4)
            for i in range(count):
                b, g, r = payload[3 * i : 3 * i + 3]
                rgba[4 * i : 4 * i + 4] = bytes((r, g, b, 255))
            return encode_rgba_png(width, height, bytes(rgba))
        return None
    return None


def parse_object_idx(idx_bytes: bytes, pkg_len: int) -> List[Tuple[str, int, int, int]]:
    if len(idx_bytes) < 16:
        return []
    _ver, count, header_size, _total = struct.unpack_from("<4I", idx_bytes, 0)
    cursor = header_size
    entries: List[Tuple[str, int, int, int]] = []
    for _ in range(count):
        if cursor + 2 > len(idx_bytes):
            break
        (name_len,) = struct.unpack_from("<H", idx_bytes, cursor)
        cursor += 2
        if cursor + name_len + 16 > len(idx_bytes):
            break
        name = idx_bytes[cursor : cursor + name_len].decode("ascii", errors="ignore")
        cursor += name_len
        _reserved, offset, raw_size, comp_size = struct.unpack_from("<4I", idx_bytes, cursor)
        cursor += 16
        if comp_size > 0 and offset + comp_size <= pkg_len:
            entries.append((name, offset, raw_size, comp_size))
    return entries


def fetch_remake_bytes(rel_path: str, remake_dir: Optional[Path]) -> Optional[bytes]:
    if remake_dir is not None:
        p = remake_dir / rel_path
        return p.read_bytes() if p.is_file() else None
    url = f"{REMAKE_RAW_BASE}/{rel_path.lstrip('./')}"
    req = urllib.request.Request(url, headers={"User-Agent": "qqtang-unity-cache"})
    try:
        with urllib.request.urlopen(req, timeout=20) as resp:
            return resp.read()
    except Exception:
        return None


def sync_local_maps(original_client: Path, maps_out: Path) -> int:
    maps_out.mkdir(parents=True, exist_ok=True)
    copied = 0
    map_src = original_client / "map"
    if map_src.is_dir():
        for f in map_src.iterdir():
            if f.suffix.lower() in (".map", ".py"):
                shutil.copy2(f, maps_out / f.name)
                copied += 1
    elem_py = original_client / "object" / "mapElem" / "mapElem.py"
    if elem_py.is_file():
        shutil.copy2(elem_py, maps_out / "mapElem.py")
        copied += 1
    return copied


def main() -> int:
    repo_root = Path(__file__).resolve().parent.parent
    parser = argparse.ArgumentParser(description="Prepare local StreamingAssets ArtCache & LocalMaps")
    parser.add_argument("--remake-dir", type=Path, default=None, help="Local clone of qqtang-daoist-remake")
    parser.add_argument(
        "--original-client",
        type=Path,
        default=Path(r"C:\Users\luyua\Downloads\QQTang-Local\runtime\client-patched"),
        help="Local QQTang-Local/runtime/client-patched directory",
    )
    parser.add_argument("--families", type=str, default="town,common,box,bomb", help="Map families or 'all'")
    parser.add_argument("--max-fallback", type=int, default=64, help="Max original .img fallbacks to decode per run (0=all)")
    parser.add_argument("--skip-remote", action="store_true", help="Skip fetching remote daoist-remake PNGs")
    args = parser.parse_args()

    art_out = repo_root / "client" / "Assets" / "StreamingAssets" / "ArtCache"
    maps_out = repo_root / "client" / "Assets" / "StreamingAssets" / "LocalMaps"
    daoist_dir = art_out / "daoist"
    orig_dir = art_out / "original"
    daoist_dir.mkdir(parents=True, exist_ok=True)
    orig_dir.mkdir(parents=True, exist_ok=True)

    manifest: Dict[str, Dict[str, str]] = {"entries": {}}

    def record(key: str, source: str, rel_out: str) -> None:
        manifest["entries"][key] = {"source": source, "path": rel_out}

    # 1. Sync local maps
    if args.original_client.is_dir():
        map_count = sync_local_maps(args.original_client, maps_out)
        print(f"[maps] synced {map_count} local map/catalog files", flush=True)

    # 2. Collect desired Daoist PNGs and fetch concurrently
    jobs: List[Tuple[str, str]] = []
    if args.remake_dir is not None or not args.skip_remote:
        for key, rel_src in CORE_DAOIST_ASSETS.items():
            jobs.append((key, rel_src))

        rm_bytes = fetch_remake_bytes("assets/maps/runtime-manifests.json", args.remake_dir)
        if rm_bytes:
            rm = json.loads(rm_bytes.decode("utf-8"))
            wanted = {f.strip().lower() for f in args.families.split(",") if f.strip()}
            all_fams = "all" in wanted
            for m_path in rm.get("manifests", []):
                parts = m_path.split("/")
                fam = parts[2].lower() if len(parts) >= 3 else ""
                if not all_fams and fam not in wanted:
                    continue
                fam_bytes = fetch_remake_bytes(m_path, args.remake_dir)
                if not fam_bytes:
                    continue
                fam_json = json.loads(fam_bytes.decode("utf-8"))
                for piece in fam_json.get("pieces", []):
                    eid = piece.get("elementId")
                    pfile = piece.get("file")
                    if eid is not None and pfile:
                        jobs.append((f"maps/elem_{eid}", pfile))

        def fetch_one(item: Tuple[str, str]) -> Tuple[str, bool, bool]:
            key, rel_src = item
            dst = daoist_dir / f"{key}.png"
            if dst.is_file():
                return key, True, False
            blob = fetch_remake_bytes(rel_src, args.remake_dir)
            if not blob:
                return key, False, False
            dst.parent.mkdir(parents=True, exist_ok=True)
            dst.write_bytes(blob)
            return key, True, True

        new_count = 0
        cached_count = 0
        with ThreadPoolExecutor(max_workers=10) as pool:
            futs = [pool.submit(fetch_one, j) for j in jobs]
            for fut in as_completed(futs):
                key, ok, downloaded = fut.result()
                if ok:
                    record(key, "daoist", f"daoist/{key}.png")
                    if downloaded:
                        new_count += 1
                    else:
                        cached_count += 1
        print(f"[daoist] ready={new_count + cached_count} (downloaded={new_count}, cached={cached_count})", flush=True)

    # 3. Original DIMG fallback from local effect/*.img and data/object.pkg
    if args.original_client.is_dir():
        decoded_count = 0
        direct_fallbacks = [
            ("effects/bubble", args.original_client / "effect" / "0.img"),
            ("effects/flame", args.original_client / "effect" / "1.img"),
        ]
        for key, img_path in direct_fallbacks:
            if key in manifest["entries"] or not img_path.is_file():
                continue
            dst = orig_dir / f"{key}.png"
            if not dst.is_file():
                png = decode_dimg_first_frame_to_png(img_path.read_bytes())
                if not png:
                    continue
                dst.parent.mkdir(parents=True, exist_ok=True)
                dst.write_bytes(png)
                decoded_count += 1
            record(key, "original", f"original/{key}.png")

        idx_path = args.original_client / "data" / "object.idx"
        pkg_path = args.original_client / "data" / "object.pkg"
        if idx_path.is_file() and pkg_path.is_file():
            pkg_bytes = pkg_path.read_bytes()
            entries = parse_object_idx(idx_path.read_bytes(), len(pkg_bytes))
            elem_pat = re.compile(r"(?:^|\\)mapelem\\.*?([0-9]+)\.img$", re.IGNORECASE)
            for name, offset, raw_size, comp_size in entries:
                if args.max_fallback > 0 and decoded_count >= args.max_fallback:
                    break
                m = elem_pat.search(name)
                if not m:
                    continue
                key = f"maps/elem_{int(m.group(1))}"
                if key in manifest["entries"]:
                    continue
                dst = orig_dir / f"{key}.png"
                if not dst.is_file():
                    try:
                        raw_img = zlib.decompress(pkg_bytes[offset : offset + comp_size])
                        png = decode_dimg_first_frame_to_png(raw_img)
                        if not png:
                            continue
                        dst.parent.mkdir(parents=True, exist_ok=True)
                        dst.write_bytes(png)
                        decoded_count += 1
                    except Exception:
                        continue
                record(key, "original", f"original/{key}.png")
        print(f"[fallback] decoded {decoded_count} original DIMG .img files -> {orig_dir}", flush=True)

    manifest_path = art_out / "manifest.json"
    manifest_path.write_text(json.dumps(manifest, indent=2, ensure_ascii=False), encoding="utf-8")
    print(f"[done] manifest entries={len(manifest['entries'])} -> {manifest_path}", flush=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())
