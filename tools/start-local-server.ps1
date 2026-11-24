param(
    [string]$ClientRoot = "C:\Users\luyua\Downloads\QQTang-Local\runtime\client-patched",
    [int]$GamePort = 18000,
    [int]$GmPort = 18100
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$serverDir = Join-Path $repoRoot "server"
$runtimeDir = Join-Path $repoRoot "local-runtime"
New-Item -ItemType Directory -Force -Path $runtimeDir, (Join-Path $runtimeDir "captures") | Out-Null

if (!(Test-Path $ClientRoot)) {
    throw "Original client static data directory not found: $ClientRoot"
}

$serverExe = Join-Path $runtimeDir "qqt-server.exe"
Write-Host "[build] Building Go server -> $serverExe"
Push-Location $serverDir
go build -o $serverExe ./cmd/qqt-server
Pop-Location

$cfgPath = Join-Path $runtimeDir "server-unity-local.json"
$esc = { param($p) $p.Replace('\', '\\') }
$cfg = @"
{
  "capture_root": "$(& $esc (Join-Path $runtimeDir 'captures'))",
  "log_file": "$(& $esc (Join-Path $runtimeDir 'server.jsonl'))",
  "client_root": "$(& $esc $ClientRoot)",
  "competitive_map_rules_path": "$(& $esc (Join-Path $serverDir 'data\qqt_competitive_map_rules.json'))",
  "database_path": "$(& $esc (Join-Path $runtimeDir 'qqtang.sqlite'))",
  "seed_uin": 1000001,
  "initial_accounts": [
    { "uin": 1000001, "nickname": "DaoistOne", "gender": 1 },
    { "uin": 1000002, "nickname": "DaoistTwo", "gender": 0 }
  ],
  "adventure_rules_path": "$(& $esc (Join-Path $serverDir 'configs\adventure-rules.json'))",
  "role_rules_path": "$(& $esc (Join-Path $serverDir 'configs\role-rules.json'))",
  "equipment_seeds_path": "$(& $esc (Join-Path $serverDir 'configs\starter-loadouts.json'))",
  "combine_recipes_path": "$(& $esc (Join-Path $serverDir 'data\qqt_combine_recipes.json'))",
  "break_egg_rewards_path": "$(& $esc (Join-Path $serverDir 'configs\break-egg-rewards.json'))",
  "forge_rules_path": "$(& $esc (Join-Path $serverDir 'data\qqt_forge_rules.json'))",
  "item_image_aliases_path": "$(& $esc (Join-Path $serverDir 'configs\item-image-aliases.json'))",
  "gm_http_address": "127.0.0.1:$GmPort",
  "directory_hall": {
    "server_ip": "127.0.0.1",
    "server_port": $GamePort,
    "server_udp_port": $GamePort,
    "shop_server_id": 2,
    "shop_server_port": $($GamePort + 1),
    "shop_server_udp_port": $($GamePort + 1)
  },
  "max_packet_bytes": 1048576,
  "idle_timeout_ms": 300000,
  "player_profile": {
    "nickname": "DaoistOne",
    "gender": 1,
    "icon_id": 0,
    "player_id": 1,
    "identity": 8192,
    "section_id": 1,
    "room_count": 100,
    "minimum_room_id": 1,
    "tutorial_completed": true,
    "game_info": { "wins": 10, "losses": 2, "draws": 1, "points": 5000, "money": 99999, "degree": 5, "role_id": 1 },
    "inventory": [
      { "id": 99, "quantity": 1 },
      { "id": 30098, "quantity": 1 },
      { "id": 30099, "quantity": 1 },
      { "id": 9001, "quantity": 3 },
      { "id": 9011, "quantity": 3 },
      { "id": 2235, "quantity": 1 },
      { "id": 9020, "quantity": 1 },
      { "id": 9021, "quantity": 1 }
    ]
  },
  "listeners": [
    {
      "name": "game-tcp",
      "network": "tcp",
      "address": "127.0.0.1:$GamePort",
      "response": {
        "qqt_directory_response": true,
        "qqt_login_success": true,
        "qqt_room_list": true,
        "qqt_player_list": true,
        "qqt_create_room_success": true,
        "qqt_start_game_success": true,
        "qqt_game_begin": true,
        "qqt_game_event_relay": true
      }
    },
    {
      "name": "shop-service",
      "network": "tcp",
      "address": "127.0.0.1:$($GamePort + 1)",
      "response": {
        "qqt_login_success": true,
        "qqt_shop_catalog": true,
        "qqt_shop_type2_session": true
      }
    },
    {
      "name": "game-udp",
      "network": "udp",
      "address": "127.0.0.1:$GamePort",
      "response": {}
    }
  ]
}
"@
[System.IO.File]::WriteAllText($cfgPath, $cfg, (New-Object System.Text.UTF8Encoding($false)))

Write-Host "[run] Starting qqt-server on 127.0.0.1:$GamePort (TCP+UDP), Shop 127.0.0.1:$($GamePort + 1), GM http://127.0.0.1:$GmPort/gm/"
Write-Host "[run] Default accounts: 1000001 / 123456 and 1000002 / 123456"
& $serverExe -config $cfgPath
