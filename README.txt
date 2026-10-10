GTA V Enhanced — Platform Switcher (Steam / Epic / RGL, satu instalasi)
========================================================================
Konsep diadaptasi dari PlatformSwitcherLegacy-nya TechnoStone (MIT),
ditulis ulang untuk GTA V Enhanced berdasarkan diff + hash SHA256 dari
tiga instalasi asli (Steam, Epic, RGL), game versi 1.0.1158.16.

MODEL DISTRIBUSI (sama kayak Legacy):
  - Folder Switcher\Steam, Switcher\Epic, Switcher\RGL di dalam folder
    game SUDAH TERISI dari sananya oleh distributor (Reva), isinya file
    khusus tiap platform dari instalasi asli.
  - Pemakai tinggal taruh exe + folder Switcher di folder game-nya,
    klik platform, main. Tidak ada langkah ambil file di aplikasi.
  - Aplikasi membaca isi folder Switcher\<Platform> langsung (scan),
    tanpa file metadata. Platform dianggap siap bila foldernya berisi
    minimal GTA5_Enhanced.exe + title.rgl.

FILE YANG DITUKAR (hasil verifikasi hash):
  - GTA5_Enhanced.exe            (Steam == Epic; RGL build berbeda)
  - GTA5_Enhanced_BE.exe         (Steam == Epic; RGL beda bytes, ukuran sama)
  - title.rgl                    (tiga-tiganya beda)
  - steam_api64.dll              (khusus Steam)
  - EOSSDK-Win64-Shipping-*.dll  (khusus Epic) + folder .egstore
  - index.bin                    (khusus RGL)
  - update\update2.rpf           (Steam == Epic; RGL beda)
  - x64\audio\sfx\RADIO_02_POP.rpf (Steam == Epic; RGL beda)
  - folder BattlEye              (ikut ditukar bila ada di folder platform)
PlayGTAV.exe dan semua DLL lain byte-identik di tiga platform -> tidak diutak-atik.

BUILD
  Buka EnhancedSwitcher.csproj di Visual Studio 2022, Build Release,
  atau:  dotnet build -c Release
  Copy 4 file dari bin\Release\net8.0-windows\ ke folder game:
  EnhancedSwitcher.exe, EnhancedSwitcher.dll,
  EnhancedSwitcher.runtimeconfig.json, EnhancedSwitcher.deps.json
  (exe minta Administrator sendiri lewat manifest.)

ISI FOLDER (distributor)
  Lihat CARA_ISI_FOLDER.txt — ada perintah PowerShell per platform
  untuk menyalin file yang dibutuhkan dari instalasi asli ke folder
  Switcher\<Platform> di base.

CATATAN
  - Platform aktif dideteksi dari hash title.rgl di folder game vs
    title.rgl tersimpan di tiap folder platform.
  - Tombol platform menampilkan versi file tersimpan (mis. v1.0.1158.16);
    kalau game update, isi folder perlu diperbarui ke versi yang sama,
    kalau tidak aplikasi memberi peringatan beda versi saat switch.
  - Log tersimpan di <base>\Switcher\Switcher.log.
