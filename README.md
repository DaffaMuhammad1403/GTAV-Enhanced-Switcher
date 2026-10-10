# GTA V Enhanced — Platform Switcher

One **GTA V Enhanced** install that works with **Steam, Epic Games, and the Rockstar Games Launcher (RGL)** — no need to keep three separate ~103 GB installs.

The three editions of the game are ~99% identical. Only a small set of platform files differs, so this tool keeps one shared game folder ("base") plus three small per-platform file sets (~1 GB each) and swaps them in seconds.

Inspired by TechnoStone's Platform Switcher for the Legacy edition — this is the Enhanced equivalent.

## Download

**[Download the full package (Google Drive)](https://drive.google.com/file/d/1WffZy0mv1CUHnkTA8SU5SMTOhMp-emuJ/view?usp=sharing)** — one zip containing `EnhancedSwitcher.exe` and the pre-filled `Switcher` folders. Extract it into your GTA V Enhanced folder, run the exe, click a platform, play.

- For **game version 1.0.1158.16** only — don't use it on other versions.
- You must own the game on every platform you switch to.
- Full bilingual tutorial (EN + ID): [TUTORIAL.txt](TUTORIAL.txt) in this repo.

## How it works

Each platform's file set lives in `Switcher\<Platform>\` inside the game folder:

```
Grand Theft Auto V Enhanced\
  GTA5_Enhanced.exe
  EnhancedSwitcher.exe
  Switcher\Steam|Epic|RGL\
```

The managed (swapped) files — everything else in the install is shared:

| File | Steam | Epic | RGL |
|---|:---:|:---:|:---:|
| `GTA5_Enhanced.exe` | ● | ● | ● |
| `GTA5_Enhanced_BE.exe` | ● | ● | ● |
| `title.rgl` | ● | ● | ● |
| `update\update2.rpf` | ● | ● | ● |
| `x64\audio\sfx\RADIO_02_POP.rpf` | ● | ● | ● |
| `steam_api64.dll` | ● | | |
| `EOSSDK-Win64-Shipping-1.17.1.3.dll` + `.egstore\` | | ● | |
| `index.bin` | | | ● |
| `BattlEye\` | ● | ● | ● |

`PlayGTAV.exe` is byte-identical across all three editions, so it is *not* swapped. The active platform is detected by hashing `title.rgl`; each platform button shows the version of its stored files and the app warns if they don't match the current game version.

## Features

- One-click platform switching with presence semantics (files a platform doesn't have are removed from the base)
- Active-platform detection + per-platform version readout
- Launch button per platform (Steam protocol / Epic protocol / PlayGTAV.exe)
- Game-running guard, full action log (`Switcher\Switcher.log`)
- Dark + gold UI, Indonesian / English language toggle

## One-time setup

Each launcher only knows its own install folder at first, so point it at the shared base **once** (per launcher):

- **Steam** — nothing to do if the base is your Steam install folder.
- **RGL** — switch the base to RGL, run `PlayGTAV.exe` from the base, and when the Rockstar Launcher offers Install, browse to the base's **parent** folder. It verifies the existing files and downloads only a little (~100 MB in testing).
- **Epic** — switch the base to Epic, then Install from the Epic Launcher pointed at the same parent folder; it discovers and verifies the existing files.

After that, switching is just: close the game/launchers → click a platform → Launch. Full bilingual walkthrough in [TUTORIAL.txt](TUTORIAL.txt).

## Build

.NET 8 WinForms, no external dependencies.

```powershell
# single-file, self-contained exe (runs without .NET installed)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

## Notes

- You must own the game on each platform you switch to — this tool swaps files, not ownership.
- The per-platform file sets are **not** included in this repo; they come from your own installs.
- Game updates make stored sets stale until they're refreshed — the version warning exists for that.
- Not affiliated with Rockstar Games / Take-Two Interactive.

---

*Switcher untuk GTA V Enhanced: satu folder game untuk Steam / Epic / RGL. Panduan lengkap dwibahasa ada di [TUTORIAL.txt](TUTORIAL.txt).*

**by Reva**
