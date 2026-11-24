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
    # Trapped-in-bubble Daoist sprites for core 4 roles
    "characters/trapped_0": "assets/characters/trapped/v2/daoist-child.png",
    "characters/trapped_1": "assets/characters/trapped/v2/moon-shadow.png",
    "characters/trapped_2": "assets/characters/trapped/v2/mountain-guardian.png",
    "characters/trapped_3": "assets/characters/trapped/v2/lotus-healer.png",
    # Dedicated Daoist 256px portraits for all 16 playable roles
    "portraits/role_1": "assets/characters/portraits-candidates/daoist-child-256.png",
    "portraits/role_2": "assets/characters/portraits-candidates/moon-shadow-256.png",
    "portraits/role_3": "assets/characters/portraits-candidates/mountain-guardian-256.png",
    "portraits/role_4": "assets/characters/portraits-candidates/lotus-healer-256.png",
    "portraits/role_5": "assets/characters/portraits-candidates/02_fire_talisman_boy-256.png",
    "portraits/role_6": "assets/characters/portraits-candidates/08_alchemy_prodigy_boy-256.png",
    "portraits/role_7": "assets/characters/portraits-candidates/01_ice_sword_girl-256.png",
    "portraits/role_8": "assets/characters/portraits-candidates/05_celestial_musician_girl-256.png",
    "portraits/role_9": "assets/characters/portraits-candidates/09_bamboo_archer_girl-256.png",
    "portraits/role_13": "assets/characters/portraits-candidates/20_star_formation_master_girl-256.png",
    "portraits/role_14": "assets/characters/portraits-candidates/06_thunder_caster_boy-256.png",
    "portraits/role_18": "assets/characters/portraits-candidates/14_short_hair_snow_summoner_girl-256.png",
    "portraits/role_19": "assets/characters/portraits-candidates/paper-kite-wanderer-256.png",
    "portraits/role_20": "assets/characters/portraits-candidates/10_crimson_spear_girl-256.png",
    "portraits/role_21": "assets/characters/portraits-candidates/15_water_dragon_scholar_boy-256.png",
    "portraits/role_22": "assets/characters/portraits-candidates/jade-craft-artisan-256.png",
    # All 8 native transformation Daoist avatars (AvatarRoleId 41..46, 54, 55)
    "transforms/avatar_41": "assets/characters/motion-generated/transformations/avatar-41-qingyu-cloud-duck/master.png",
    "transforms/avatar_42": "assets/characters/motion-generated/transformations/avatar-42-ink-horn-fox/master.png",
    "transforms/avatar_43": "assets/characters/motion-generated/transformations/avatar-43-ruyi-lingzhi-spirit/master.png",
    "transforms/avatar_44": "assets/characters/motion-generated/transformations/avatar-44-jade-palm-bamboo-bear/master.png",
    "transforms/avatar_45": "assets/characters/motion-generated/transformations/avatar-45-cloud-lantern-spirit/master.png",
    "transforms/avatar_46": "assets/characters/motion-generated/transformations/avatar-46-golden-gourd-sage/master.png",
    "transforms/avatar_54": "assets/characters/motion-generated/transformations/avatar-54-jade-shell-crab/master.png",
    "transforms/avatar_55": "assets/characters/motion-generated/transformations/avatar-55-jade-stone-axe-child/master.png",
    # Multi-frame Daoist bubble & flame animations + protection shield
    "effects/bubble": "assets/effects/bubbles/bomb-1/00.png",
    "effects/bubble_f0": "assets/effects/bubbles/bomb-1/00.png",
    "effects/bubble_f1": "assets/effects/bubbles/bomb-1/01.png",
    "effects/bubble_f2": "assets/effects/bubbles/bomb-1/03.png",
    "effects/bubble_alt_f0": "assets/effects/bubbles/bomb-4/00.png",
    "effects/bubble_alt_f1": "assets/effects/bubbles/bomb-4/01.png",
    "effects/bubble_alt_f2": "assets/effects/bubbles/bomb-4/02.png",
    "effects/bubble_alt_f3": "assets/effects/bubbles/bomb-4/03.png",
    "effects/flame": "assets/effects/flames/flame10/00.png",
    "effects/flame_f0": "assets/effects/flames/flame10/00.png",
    "effects/flame_f1": "assets/effects/flames/flame10/01.png",
    "effects/flame_f2": "assets/effects/flames/flame10/02.png",
    "effects/flame_f3": "assets/effects/flames/flame10/03.png",
    "effects/shield": "assets/effects/protection-bubble-v1.png",
    "effects/bun": "assets/runtime/battle-props-20261112/bun.png",
    "pickups/scene_1": "assets/runtime/battle-props-20261112/capacity.png",
    "pickups/scene_2": "assets/runtime/battle-props-20261112/range.png",
    "pickups/scene_3": "assets/runtime/battle-props-20261112/speed.png",
    "pickups/scene_6": "assets/runtime/battle-props-20261112/capacity.png",
    "pickups/scene_7": "assets/runtime/battle-props-20261112/range.png",
    "pickups/scene_8": "assets/runtime/battle-props-20261112/speed.png",
    "pickups/scene_24": "assets/runtime/battle-props-20261112/needle.png",
    "pickups/scene_63": "assets/runtime/battle-props-20261112/needle.png",
    "ui/logo": "assets/branding/linghu-wordmark-login-1024.png",
    "ui/victory": "assets/ui/results/victory.png",
    "ui/defeat": "assets/ui/results/defeat.png",
    "ui/draw": "assets/ui/results/draw.png",
}

DAOIST_AUDIO_CLIPS: List[str] = [
    "bubble-burst",
    "bubble-kick",
    "bubble-place",
    "bun-delivery",
    "damage",
    "defeat",
    "draw",
    "eliminated",
    "music-battle",
    "music-lobby",
    "needle",
    "pickup",
    "rescued",
    "respawn",
    "shield",
    "trapped",
    "ui-click",
    "victory",
    "wave",
    "wood-break",
    "wood-push",
]


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


def sync_catalogs(repo_root: Path, original_client: Path, catalogs_out: Path) -> int:
    """Copy server JSON catalogs and optional Commodity.ini into gitignored StreamingAssets/Catalogs for standalone builds."""
    catalogs_out.mkdir(parents=True, exist_ok=True)
    server_dir = repo_root / "server"
    candidates = [
        server_dir / "data" / "qqt_competitive_map_rules.json",
        server_dir / "data" / "qqt_combine_recipes.json",
        server_dir / "data" / "qqt_pet_catalog.json",
        server_dir / "docs" / "item-id-catalog.json",
        original_client / "Commodity.ini",
        original_client / "config" / "Commodity.ini",
        server_dir / "data" / "Commodity.ini",
    ]
    copied = 0
    seen = set()
    for src in candidates:
        if src.is_file() and src.name not in seen:
            shutil.copy2(src, catalogs_out / src.name)
            seen.add(src.name)
            copied += 1
    return copied


def main() -> int:
    repo_root = Path(__file__).resolve().parent.parent
    parser = argparse.ArgumentParser(description="Prepare local StreamingAssets ArtCache, Audio, Catalogs & LocalMaps")
    parser.add_argument("--remake-dir", type=Path, default=None, help="Local clone of qqtang-daoist-remake")
    parser.add_argument(
        "--original-client",
        type=Path,
        default=Path(r"C:\Users\luyua\Downloads\QQTang-Local\runtime\client-patched"),
        help="Local QQTang-Local/runtime/client-patched directory",
    )
    parser.add_argument("--families", type=str, default="all", help="Map families or 'all'")
    parser.add_argument("--max-fallback", type=int, default=0, help="Max original .img fallbacks to decode per run (0=all)")
    parser.add_argument("--max-icons", type=int, default=0, help="Max Daoist item icons to download from assets/items/art.json (0=all)")
    parser.add_argument("--no-audio", action="store_true", help="Skip downloading Daoist WAV audio clips")
    parser.add_argument("--no-icons", action="store_true", help="Skip downloading Daoist item icons")
    parser.add_argument("--skip-remote", action="store_true", help="Skip fetching remote daoist-remake assets")
    args = parser.parse_args()

    art_out = repo_root / "client" / "Assets" / "StreamingAssets" / "ArtCache"
    maps_out = repo_root / "client" / "Assets" / "StreamingAssets" / "LocalMaps"
    catalogs_out = repo_root / "client" / "Assets" / "StreamingAssets" / "Catalogs"
    daoist_dir = art_out / "daoist"
    orig_dir = art_out / "original"
    audio_dir = art_out / "audio"
    daoist_dir.mkdir(parents=True, exist_ok=True)
    orig_dir.mkdir(parents=True, exist_ok=True)
    audio_dir.mkdir(parents=True, exist_ok=True)

    manifest: Dict[str, Dict[str, str]] = {"entries": {}}

    def record(key: str, source: str, rel_out: str) -> None:
        manifest["entries"][key] = {"source": source, "path": rel_out}

    # 1. Sync local maps & standalone catalogs
    if args.original_client.is_dir():
        map_count = sync_local_maps(args.original_client, maps_out)
        print(f"[maps] synced {map_count} local map/catalog files", flush=True)
    cat_count = sync_catalogs(repo_root, args.original_client, catalogs_out)
    print(f"[catalogs] synced {cat_count} catalog files -> {catalogs_out}", flush=True)

    # 2. Collect desired Daoist PNGs (core + maps + item icons) and fetch concurrently
    jobs: List[Tuple[str, str]] = []
    if args.remake_dir is not None or not args.skip_remote:
        for key, rel_src in CORE_DAOIST_ASSETS.items():
            jobs.append((key, rel_src))

        rm_bytes = fetch_remake_bytes("assets/maps/runtime-manifests.json", args.remake_dir)
        if rm_bytes:
            rm = json.loads(rm_bytes.decode("utf-8"))
            wanted = {f.strip().lower() for f in args.families.split(",") if f.strip()}
            all_fams = "all" in wanted
            selected_manifests = [
                m_path for m_path in rm.get("manifests", [])
                if all_fams or ((m_path.split("/")[2].lower() if len(m_path.split("/")) >= 3 else "") in wanted)
            ]
            with ThreadPoolExecutor(max_workers=10) as mpool:
                for fam_bytes in mpool.map(lambda p: fetch_remake_bytes(p, args.remake_dir), selected_manifests):
                    if not fam_bytes:
                        continue
                    fam_json = json.loads(fam_bytes.decode("utf-8"))
                    for piece in fam_json.get("pieces", []):
                        eid = piece.get("elementId")
                        pfile = piece.get("file")
                        if eid is not None and pfile:
                            jobs.append((f"maps/elem_{eid}", pfile))

        if not args.no_icons:
            art_json_bytes = fetch_remake_bytes("assets/items/art.json", args.remake_dir)
            if art_json_bytes:
                try:
                    art_doc = json.loads(art_json_bytes.decode("utf-8"))
                    icon_added = 0
                    for it in art_doc.get("items", []):
                        iid = it.get("itemId")
                        ifile = it.get("file")
                        if iid is None or not ifile:
                            continue
                        jobs.append((f"items/item_{iid}", ifile))
                        icon_added += 1
                        if args.max_icons > 0 and icon_added >= args.max_icons:
                            break
                except Exception:
                    pass

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

        # 2b. Fetch Daoist WAV audio clips (BGM + SFX)
        if not args.no_audio:
            def fetch_audio(clip_name: str) -> Tuple[str, bool, bool]:
                dst = audio_dir / f"{clip_name}.wav"
                if dst.is_file():
                    return clip_name, True, False
                blob = fetch_remake_bytes(f"assets/audio/original-v1/{clip_name}.wav", args.remake_dir)
                if not blob:
                    return clip_name, False, False
                dst.write_bytes(blob)
                return clip_name, True, True

            a_new = 0
            a_cached = 0
            with ThreadPoolExecutor(max_workers=8) as pool:
                futs = [pool.submit(fetch_audio, c) for c in DAOIST_AUDIO_CLIPS]
                for fut in as_completed(futs):
                    cname, ok, downloaded = fut.result()
                    if ok:
                        record(f"audio/{cname}", "daoist", f"audio/{cname}.wav")
                        if downloaded:
                            a_new += 1
                        else:
                            a_cached += 1
            print(f"[audio] ready={a_new + a_cached}/{len(DAOIST_AUDIO_CLIPS)} (downloaded={a_new}, cached={a_cached})", flush=True)

    # 3. Original DIMG fallback from local effect/*.img, loose object/*/*.img, and data/object.pkg
    #    ONLY decoded when no Daoist replacement exists for that key (exact Daoist-first, Original-fallback rule).
    if args.original_client.is_dir():
        fallback_jobs: List[Tuple[str, Optional[Path], Optional[Tuple[int, int]]]] = []
        seen_fb = set()

        def enqueue_fb(key: str, file_path: Optional[Path] = None, pkg_slice: Optional[Tuple[int, int]] = None) -> None:
            if key in manifest["entries"] or key in seen_fb:
                return
            if args.max_fallback > 0 and len(fallback_jobs) >= args.max_fallback:
                return
            seen_fb.add(key)
            fallback_jobs.append((key, file_path, pkg_slice))

        direct_fallbacks = [
            ("effects/bubble", args.original_client / "effect" / "0.img"),
            ("effects/flame", args.original_client / "effect" / "1.img"),
        ]
        for key, img_path in direct_fallbacks:
            if img_path.is_file():
                enqueue_fb(key, file_path=img_path)

        idx_path = args.original_client / "data" / "object.idx"
        pkg_path = args.original_client / "data" / "object.pkg"
        pkg_bytes: Optional[bytes] = None
        if idx_path.is_file() and pkg_path.is_file():
            pkg_bytes = pkg_path.read_bytes()
            entries = parse_object_idx(idx_path.read_bytes(), len(pkg_bytes))
            pkg_patterns = [
                (re.compile(r"(?:^|\\)mapelem\\.*?elem([0-9]+)_stand\.img$", re.IGNORECASE), "maps/elem_{}"),
                (re.compile(r"(?:^|\\)mapelem\\.*?([0-9]+)\.img$", re.IGNORECASE), "maps/elem_{}"),
                (re.compile(r"(?:^|\\)item\\item([0-9]+)_stand\.img$", re.IGNORECASE), "pickups/scene_{}"),
                (re.compile(r"(?:^|\\)bomb\\bomb([0-9]+)_stand\.img$", re.IGNORECASE), "effects/bubble_{}"),
                (re.compile(r"(?:^|\\)flame\\flame([0-9]+)_stand\.img$", re.IGNORECASE), "effects/flame_{}"),
                (re.compile(r"(?:^|\\)pet\\pet([0-9]+)_stand\.img$", re.IGNORECASE), "pets/pet_{}"),
                (re.compile(r"(?:^|\\)chatbg\\chat([0-9]+)_stand\.img$", re.IGNORECASE), "chatbg/chat_{}"),
            ]
            for name, offset, raw_size, comp_size in entries:
                for pat, fmt in pkg_patterns:
                    m = pat.search(name)
                    if m:
                        enqueue_fb(fmt.format(int(m.group(1))), pkg_slice=(offset, comp_size))
                        break

        # Also decode original map/*.img thumbnails mapped by mapId from mapDesc.py
        map_desc_py = args.original_client / "map" / "mapDesc.py"
        if map_desc_py.is_file():
            try:
                raw_desc = map_desc_py.read_bytes().decode("gb18030", errors="ignore")
                thumb_pat = re.compile(
                    r"^\(\s*([0-9]+)\s*,\s*'[^']+'\s*,\s*'[^']*'\s*,\s*[0-9]+\s*,\s*[0-9]+\s*,\s*[0-9]+\s*,\s*'[^']+\.map'\s*,\s*'([^']+\.img)'",
                    re.MULTILINE,
                )
                for m in thumb_pat.finditer(raw_desc):
                    mid = int(m.group(1))
                    img_file = args.original_client / "map" / m.group(2)
                    if mid > 0 and img_file.is_file():
                        enqueue_fb(f"maps/thumb_{mid}", file_path=img_file)
            except Exception:
                pass

        # Also scan loose object/pet/*.img and object/bomb/*.img for extra skins/pets not inside object.pkg
        loose_dirs = [
            (args.original_client / "object" / "pet", re.compile(r"^pet([0-9]+)_stand\.img$", re.IGNORECASE), "pets/pet_{}"),
            (args.original_client / "object" / "bomb", re.compile(r"^bomb([0-9]+)_stand\.img$", re.IGNORECASE), "effects/bubble_{}"),
        ]
        for ldir, pat, fmt in loose_dirs:
            if not ldir.is_dir():
                continue
            for f in sorted(ldir.iterdir()):
                m = pat.match(f.name)
                if m:
                    enqueue_fb(fmt.format(int(m.group(1))), file_path=f)

        def decode_one(job: Tuple[str, Optional[Path], Optional[Tuple[int, int]]]) -> Tuple[str, bool, bool]:
            key, fpath, pslice = job
            dst = orig_dir / f"{key}.png"
            if dst.is_file():
                return key, True, False
            try:
                if fpath is not None:
                    raw_img = fpath.read_bytes()
                elif pslice is not None and pkg_bytes is not None:
                    raw_img = zlib.decompress(pkg_bytes[pslice[0] : pslice[0] + pslice[1]])
                else:
                    return key, False, False
                png = decode_dimg_first_frame_to_png(raw_img)
                if not png:
                    return key, False, False
                dst.parent.mkdir(parents=True, exist_ok=True)
                dst.write_bytes(png)
                return key, True, True
            except Exception:
                return key, False, False

        fb_new = 0
        fb_cached = 0
        with ThreadPoolExecutor(max_workers=8) as pool:
            futs = [pool.submit(decode_one, j) for j in fallback_jobs]
            for fut in as_completed(futs):
                key, ok, decoded = fut.result()
                if ok:
                    record(key, "original", f"original/{key}.png")
                    if decoded:
                        fb_new += 1
                    else:
                        fb_cached += 1
        print(f"[fallback] ready={fb_new + fb_cached} original DIMG PNGs (decoded={fb_new}, cached={fb_cached}) -> {orig_dir}", flush=True)

    manifest_path = art_out / "manifest.json"
    manifest_path.write_text(json.dumps(manifest, indent=2, ensure_ascii=False), encoding="utf-8")
    print(f"[done] manifest entries={len(manifest['entries'])} -> {manifest_path}", flush=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())
