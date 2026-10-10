using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace EnhancedSwitcher;

// GTA V Enhanced Platform Switcher (Steam / Epic / RGL on ONE installation).
// Concept adapted from TechnoStone's PlatformSwitcherLegacy (MIT), rewritten for Enhanced.
//
// DISTRIBUTION MODEL (like the Legacy switcher):
//  - The <base>\Switcher\<Platform>\ folders ship PRE-FILLED by the distributor
//    (Reva) with each platform's special files, taken from genuine installs.
//  - End users just drop the exe + Switcher folder into their game folder,
//    click a platform, and play. There is no capture step in this app.
//  - The app reads the folders by scanning them; no metadata files are needed.
//    A platform counts as ready when its folder holds at least
//    GTA5_Enhanced.exe + title.rgl.
//  - Switch: copy the platform's stored files over the base; managed files the
//    target platform does not have (steam_api64.dll, EOS SDK, ...) are deleted
//    from the base. PlayGTAV.exe is byte-identical everywhere, never touched.
//
// The managed set was found by diffing + SHA256-hashing three real installs
// (Steam / Epic / RGL, game 1.0.1158.16, 2026-10-10).
public class MainForm : Form
{
    private static readonly string[] Platforms = { "Steam", "Epic", "RGL" };

    // Files whose content or presence differs per platform (verified by hash).
    private static readonly string[] ManagedFiles =
    {
        "GTA5_Enhanced.exe",                       // Steam == Epic, RGL different build
        "GTA5_Enhanced_BE.exe",                    // Steam == Epic, RGL different bytes (same size!)
        "title.rgl",                               // all three different
        "steam_api64.dll",                         // Steam only
        "EOSSDK-Win64-Shipping-1.17.1.3.dll",      // Epic only
        "index.bin",                               // RGL only
        @"update\update2.rpf",                     // Steam == Epic, RGL different
        @"x64\audio\sfx\RADIO_02_POP.rpf",         // Steam == Epic, RGL different
    };

    // Folders handled the same way (.egstore = Epic only; BattlEye listed identical
    // across platforms, carried along just in case a fill includes it).
    private static readonly string[] ManagedDirs = { ".egstore", "BattlEye" };

    private const string SteamAppId = "3240220";
    private const string EpicAppId = "2869a6774d90387a2af259a7a89abe37"; // from .egstore manifest name

    // ---- theme (matches the gold-on-charcoal logo) ----
    private static readonly Color BgColor = Color.FromArgb(24, 24, 24);      // == logo background
    private static readonly Color PanelColor = Color.FromArgb(38, 38, 38);
    private static readonly Color InputColor = Color.FromArgb(16, 16, 16);
    private static readonly Color BorderColor = Color.FromArgb(64, 64, 64);
    private static readonly Color TextColor = Color.FromArgb(237, 237, 237);
    private static readonly Color DimColor = Color.FromArgb(158, 158, 158);
    private static readonly Color GoldColor = Color.FromArgb(238, 188, 47);

    private string _baseDir = "";
    private bool _busy;
    private string _lang = "id";
    private bool En => _lang == "en";
    private string Tr(string id, string en) => En ? en : id;

    private readonly TextBox _txtBase = new() { ReadOnly = true, Dock = DockStyle.Fill };
    private readonly Label _lblSubtitle = new() { AutoSize = true };
    private readonly Label _lblBase = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly Label _lblCurrent = new() { AutoSize = true, Font = new Font("Segoe UI", 11, FontStyle.Bold) };
    private readonly Label _lblVersion = new() { AutoSize = true };
    private readonly Button _btnBase = new() { AutoSize = true };
    private readonly Button _btnId = new() { Text = "ID", Width = 40, Height = 26 };
    private readonly Button _btnEn = new() { Text = "EN", Width = 40, Height = 26 };
    private readonly Dictionary<string, Button> _switchButtons = new();
    private readonly Button _btnLaunch = new() { Text = "Launch", Dock = DockStyle.Fill, Height = 38 };
    private readonly TextBox _log = new()
    {
        Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
        Dock = DockStyle.Fill, Font = new Font("Consolas", 9),
    };

    public MainForm()
    {
        Text = "GTA V Enhanced — Platform Switcher";
        Width = 780;
        Height = 600;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = BgColor;
        ForeColor = TextColor;

        var logo = LoadLogo();
        if (logo != null)
        {
            try { Icon = Icon.FromHandle(new Bitmap(logo, 64, 64).GetHicon()); } catch { }
        }

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6,
            Padding = new Padding(12), BackColor = BgColor,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // header
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // base row
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // current label
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // switch buttons
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // launch
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // log

        // --- header: logo + title + language toggle ---
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, AutoSize = true, BackColor = BgColor };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var pic = new PictureBox
        {
            Width = 92, Height = 92, SizeMode = PictureBoxSizeMode.Zoom,
            Image = logo, BackColor = BgColor, Margin = new Padding(0, 0, 10, 6),
        };
        header.Controls.Add(pic, 0, 0);
        var titles = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown, Dock = DockStyle.Fill,
            AutoSize = true, BackColor = BgColor, Padding = new Padding(0, 14, 0, 0),
        };
        titles.Controls.Add(new Label
        {
            Text = "GTA V ENHANCED", AutoSize = true,
            Font = new Font("Segoe UI", 17, FontStyle.Bold), ForeColor = GoldColor,
        });
        _lblSubtitle.Font = new Font("Segoe UI", 9.5f);
        _lblSubtitle.ForeColor = DimColor;
        titles.Controls.Add(_lblSubtitle);
        header.Controls.Add(titles, 1, 0);
        var langRow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight, AutoSize = true,
            BackColor = BgColor, Padding = new Padding(0, 4, 0, 0),
        };
        StyleButton(_btnId);
        StyleButton(_btnEn);
        _btnId.Click += (_, _) => SetLanguage("id");
        _btnEn.Click += (_, _) => SetLanguage("en");
        langRow.Controls.Add(_btnId);
        langRow.Controls.Add(_btnEn);
        header.Controls.Add(langRow, 2, 0);
        root.Controls.Add(header, 0, 0);

        // --- base folder row ---
        _txtBase.BackColor = InputColor;
        _txtBase.ForeColor = TextColor;
        _txtBase.BorderStyle = BorderStyle.FixedSingle;
        _lblBase.ForeColor = TextColor;
        var baseRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, AutoSize = true, BackColor = BgColor };
        baseRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        baseRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        baseRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        baseRow.Controls.Add(_lblBase, 0, 0);
        baseRow.Controls.Add(_txtBase, 1, 0);
        StyleButton(_btnBase);
        _btnBase.Click += (_, _) => PickBaseFolder();
        baseRow.Controls.Add(_btnBase, 2, 0);
        root.Controls.Add(baseRow, 0, 1);

        // --- current platform ---
        _lblCurrent.ForeColor = GoldColor;
        _lblVersion.ForeColor = DimColor;
        var curRow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, BackColor = BgColor };
        curRow.Controls.Add(_lblCurrent);
        curRow.Controls.Add(_lblVersion);
        root.Controls.Add(curRow, 0, 2);

        // --- switch buttons ---
        var switchRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, AutoSize = true, BackColor = BgColor };
        for (int i = 0; i < 3; i++) switchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        foreach (var p in Platforms)
        {
            var b = new Button { Dock = DockStyle.Fill, Height = 58, Font = new Font("Segoe UI", 11, FontStyle.Bold) };
            StyleButton(b);
            string platform = p;
            b.Click += async (_, _) => await SwitchToAsync(platform);
            _switchButtons[p] = b;
            switchRow.Controls.Add(b);
        }
        root.Controls.Add(switchRow, 0, 3);

        StyleButton(_btnLaunch, accent: true);
        _btnLaunch.Font = new Font("Segoe UI", 11, FontStyle.Bold);
        _btnLaunch.Click += (_, _) => Launch();
        root.Controls.Add(_btnLaunch, 0, 4);

        _log.BackColor = InputColor;
        _log.ForeColor = Color.FromArgb(205, 205, 205);
        _log.BorderStyle = BorderStyle.FixedSingle;
        root.Controls.Add(_log, 0, 5);

        Controls.Add(root);

        // --- watermark, bottom-right over the log corner ---
        var watermark = new Label
        {
            Text = "© REVA",
            AutoSize = true,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = Color.FromArgb(150, 124, 52), // dimmed gold
            BackColor = InputColor,
            Padding = new Padding(4, 1, 4, 1),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
        };
        Controls.Add(watermark);
        watermark.Location = new Point(
            ClientSize.Width - watermark.Width - 16,
            ClientSize.Height - watermark.Height - 14);
        watermark.BringToFront();

        ApplyLanguage();
        Load += (_, _) => InitBase();
    }

    private static void StyleButton(Button b, bool accent = false)
    {
        b.FlatStyle = FlatStyle.Flat;
        b.UseVisualStyleBackColor = false;
        b.FlatAppearance.BorderSize = 1;
        b.FlatAppearance.BorderColor = accent ? GoldColor : BorderColor;
        b.FlatAppearance.MouseOverBackColor = Color.FromArgb(52, 52, 52);
        b.BackColor = accent ? GoldColor : PanelColor;
        b.ForeColor = accent ? Color.FromArgb(24, 24, 24) : TextColor;
        b.Cursor = Cursors.Hand;
    }

    private static Image? LoadLogo()
    {
        try
        {
            using var stream = typeof(MainForm).Assembly
                .GetManifestResourceStream("EnhancedSwitcher.logo.png");
            if (stream == null) return null;
            using var img = Image.FromStream(stream);
            return new Bitmap(img); // copy so the stream can close
        }
        catch { return null; }
    }

    // ---------------------------------------------------------------
    // Language
    // ---------------------------------------------------------------
    private void SetLanguage(string lang)
    {
        _lang = lang;
        var s = LoadSettings();
        s.Lang = lang;
        SaveSettings(s);
        ApplyLanguage();
        RefreshUi();
    }

    private void ApplyLanguage()
    {
        _lblSubtitle.Text = Tr(
            "Platform Switcher  •  Steam  •  Epic  •  RGL  —  satu folder buat semua launcher",
            "Platform Switcher  •  Steam  •  Epic  •  RGL  —  one folder for every launcher");
        _lblBase.Text = Tr("Folder game (base):", "Game folder (base):");
        _btnBase.Text = Tr("Pilih...", "Browse...");

        foreach (var (btn, code) in new[] { (_btnId, "id"), (_btnEn, "en") })
        {
            bool active = _lang == code;
            btn.BackColor = active ? GoldColor : PanelColor;
            btn.ForeColor = active ? Color.FromArgb(24, 24, 24) : TextColor;
            btn.FlatAppearance.BorderColor = active ? GoldColor : BorderColor;
        }
    }

    // ---------------------------------------------------------------
    // Paths / state
    // ---------------------------------------------------------------
    private string SettingsPath => Path.Combine(AppContext.BaseDirectory, "EnhancedSwitcher.settings.json");
    private string SwitcherDir => Path.Combine(_baseDir, "Switcher");
    private string PlatformDir(string platform) => Path.Combine(SwitcherDir, platform);
    private string StatePath => Path.Combine(SwitcherDir, "switcher.json");

    private sealed class Settings
    {
        public string Base { get; set; } = "";
        public string Current { get; set; } = "";
        public string Lang { get; set; } = "id";
    }

    // What one pre-filled platform folder holds, found by scanning it.
    private sealed class PlatformSet
    {
        public HashSet<string> Files = new();
        public HashSet<string> Dirs = new();
        public string ExeVersion = "";
        public string TitleHash = "";
        public bool Ready => Files.Contains("GTA5_Enhanced.exe") && Files.Contains("title.rgl");
    }

    private PlatformSet? LoadSet(string platform)
    {
        var dir = PlatformDir(platform);
        if (!Directory.Exists(dir)) return null;
        var set = new PlatformSet();
        foreach (var rel in ManagedFiles)
            if (File.Exists(Path.Combine(dir, rel))) set.Files.Add(rel);
        foreach (var rel in ManagedDirs)
            if (Directory.Exists(Path.Combine(dir, rel))) set.Dirs.Add(rel);
        set.ExeVersion = GetExeVersion(Path.Combine(dir, "GTA5_Enhanced.exe"));
        var title = Path.Combine(dir, "title.rgl");
        if (File.Exists(title)) set.TitleHash = Sha256Hex(title);
        return set;
    }

    private Settings LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsPath))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsPath)) ?? new Settings();
        }
        catch { /* corrupted settings -> start fresh */ }
        return new Settings();
    }

    private void SaveSettings(Settings s)
    {
        try { File.WriteAllText(SettingsPath, JsonSerializer.Serialize(s)); } catch { }
    }

    private void InitBase()
    {
        var s = LoadSettings();
        if (s.Lang is "id" or "en" && s.Lang != _lang)
        {
            _lang = s.Lang;
            ApplyLanguage();
        }
        if (!string.IsNullOrEmpty(s.Base) && File.Exists(Path.Combine(s.Base, "GTA5_Enhanced.exe")))
            _baseDir = s.Base;
        else if (File.Exists(Path.Combine(AppContext.BaseDirectory, "GTA5_Enhanced.exe")))
            _baseDir = AppContext.BaseDirectory;

        _txtBase.Text = _baseDir;
        RefreshUi();
        if (string.IsNullOrEmpty(_baseDir))
        {
            Log(Tr("Pilih dulu folder game yang mau dijadiin base (folder GTA V Enhanced kamu).",
                   "First, pick your GTA V Enhanced game folder (the base)."));
        }
        else
        {
            Log($"Base: {_baseDir}");
            foreach (var p in Platforms)
            {
                var set = LoadSet(p);
                Log(set == null
                    ? Tr($"  Folder Switcher\\{p} nggak ketemu — platform ini belum bisa dipakai.",
                         $"  Switcher\\{p} folder not found — this platform is not usable yet.")
                    : set.Ready
                        ? $"  {p}: siap (versi file {set.ExeVersion})"
                        : Tr($"  {p}: foldernya ada tapi isinya belum lengkap.",
                             $"  {p}: folder exists but its contents are incomplete."));
            }
        }
    }

    private void PickBaseFolder()
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = Tr("Pilih folder GTA V Enhanced kamu",
                             "Pick your GTA V Enhanced folder"),
            UseDescriptionForTitle = true,
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        if (!File.Exists(Path.Combine(dlg.SelectedPath, "GTA5_Enhanced.exe")))
        {
            MessageBox.Show(this,
                Tr("Folder itu nggak ada GTA5_Enhanced.exe-nya.", "That folder has no GTA5_Enhanced.exe in it."),
                Tr("Bukan folder Enhanced", "Not an Enhanced folder"),
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        _baseDir = dlg.SelectedPath;
        _txtBase.Text = _baseDir;
        var s = LoadSettings();
        s.Base = _baseDir;
        SaveSettings(s);
        Log(Tr($"Base sekarang: {_baseDir}", $"Base is now: {_baseDir}"));
        RefreshUi();
    }

    // ---------------------------------------------------------------
    // Switch
    // ---------------------------------------------------------------
    private async Task SwitchToAsync(string platform)
    {
        if (!EnsureBase()) return;
        if (_busy) return;

        var set = LoadSet(platform);
        if (set == null || !set.Ready)
        {
            MessageBox.Show(this,
                Tr($"File {platform} belum lengkap di folder Switcher\\{platform} — platform ini belum bisa dipakai.",
                   $"The {platform} files in Switcher\\{platform} are missing or incomplete — this platform is not usable yet."),
                Tr("Belum siap", "Not ready"),
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (GameRunning())
        {
            MessageBox.Show(this,
                Tr("GTA V / PlayGTAV masih jalan. Tutup dulu game sama launcher-nya.",
                   "GTA V / PlayGTAV is still running. Close the game and the launchers first."),
                Tr("Game masih jalan", "Game is running"),
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var baseExeVer = GetExeVersion(Path.Combine(_baseDir, "GTA5_Enhanced.exe"));
        if (!string.IsNullOrEmpty(set.ExeVersion) && set.ExeVersion != baseExeVer)
        {
            var r = MessageBox.Show(this,
                Tr($"Versi file {platform} ({set.ExeVersion}) beda sama versi game di folder ini ({baseExeVer}).\n" +
                   "Kemungkinan game-nya udah update — file beda versi bisa bikin launcher minta verify/update ulang.\n\nTetap ganti?",
                   $"The {platform} files are version {set.ExeVersion}, but this game folder is {baseExeVer}.\n" +
                   "The game has probably updated — mismatched files can make the launcher demand a verify/re-update.\n\nSwitch anyway?"),
                Tr("Versi beda", "Version mismatch"),
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (r != DialogResult.Yes) return;
        }

        if (MessageBox.Show(this,
                Tr($"Folder ini diganti jadi mode {platform}?", $"Switch this folder to {platform} mode?"),
                Tr("Konfirmasi", "Confirm"),
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

        SetBusy(true);
        try
        {
            await Task.Run(() => ApplySet(platform, set));

            var s = LoadSettings();
            s.Current = platform;
            SaveSettings(s);
            WriteState(platform);

            var titleNow = Path.Combine(_baseDir, "title.rgl");
            if (File.Exists(titleNow) && set.TitleHash.Length > 0 && Sha256Hex(titleNow) == set.TitleHash)
                Log(Tr($"BERHASIL ganti ke {platform} (title.rgl cocok).",
                       $"Switched to {platform} (title.rgl verified)."));
            else
                Log(Tr($"BERHASIL ganti ke {platform} (title.rgl nggak bisa dicek — kalau launcher protes, cek manual ya).",
                       $"Switched to {platform} (title.rgl could not be verified — if a launcher complains, check manually)."));
        }
        catch (Exception ex) { Log("ERROR: " + ex.Message); }
        finally { SetBusy(false); RefreshUi(); }
    }

    private void ApplySet(string platform, PlatformSet set)
    {
        var srcDir = PlatformDir(platform);
        foreach (var rel in ManagedFiles)
        {
            var dest = Path.Combine(_baseDir, rel);
            if (set.Files.Contains(rel))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(Path.Combine(srcDir, rel), dest, true);
                Log($"  -> {rel}");
            }
            else if (File.Exists(dest))
            {
                File.Delete(dest);
                Log(Tr($"  x  {rel} (nggak ada di mode {platform}, dihapus)",
                       $"  x  {rel} (not part of {platform} mode, deleted)"));
            }
        }
        foreach (var rel in ManagedDirs)
        {
            var dest = Path.Combine(_baseDir, rel);
            if (set.Dirs.Contains(rel))
            {
                MirrorDir(Path.Combine(srcDir, rel), dest);
                Log($"  -> folder {rel}");
            }
            else if (Directory.Exists(dest))
            {
                Directory.Delete(dest, true);
                Log(Tr($"  x  folder {rel} (nggak ada di mode {platform}, dihapus)",
                       $"  x  folder {rel} (not part of {platform} mode, deleted)"));
            }
        }
    }

    private void WriteState(string platform)
    {
        try
        {
            Directory.CreateDirectory(SwitcherDir);
            File.WriteAllText(StatePath, JsonSerializer.Serialize(new Settings { Current = platform }));
        }
        catch { }
    }

    // ---------------------------------------------------------------
    // Launch / detect / misc
    // ---------------------------------------------------------------
    private void Launch()
    {
        if (!EnsureBase()) return;
        var current = DetectCurrent() ?? LoadSettings().Current;
        try
        {
            switch (current)
            {
                case "Steam":
                    Process.Start(new ProcessStartInfo($"steam://rungameid/{SteamAppId}") { UseShellExecute = true });
                    Log("Launch via Steam.");
                    break;
                case "Epic":
                    Process.Start(new ProcessStartInfo(
                        $"com.epicgames.launcher://apps/{EpicAppId}?action=launch&silent=true") { UseShellExecute = true });
                    Log("Launch via Epic.");
                    break;
                case "RGL":
                    Process.Start(new ProcessStartInfo(Path.Combine(_baseDir, "PlayGTAV.exe"))
                    { UseShellExecute = true, WorkingDirectory = _baseDir });
                    Log("Launch via PlayGTAV (RGL).");
                    break;
                default:
                    MessageBox.Show(this,
                        Tr("Belum ketahuan lagi mode apa — ganti ke salah satu platform dulu.",
                           "The current mode is unknown — switch to a platform first."),
                        Tr("Belum ada mode", "No mode yet"),
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    break;
            }
        }
        catch (Exception ex) { Log("ERROR launch: " + ex.Message); }
    }

    private string? DetectCurrent()
    {
        var title = Path.Combine(_baseDir, "title.rgl");
        if (!File.Exists(title)) return null;
        var h = Sha256Hex(title);
        foreach (var p in Platforms)
        {
            var set = LoadSet(p);
            if (set != null && set.TitleHash.Length > 0 && set.TitleHash == h) return p;
        }
        return null;
    }

    private static bool GameRunning()
    {
        foreach (var name in new[] { "GTA5_Enhanced", "GTA5_Enhanced_BE", "PlayGTAV" })
            if (Process.GetProcessesByName(name).Length > 0) return true;
        return false;
    }

    private static string GetExeVersion(string exePath)
    {
        try
        {
            if (File.Exists(exePath))
                return FileVersionInfo.GetVersionInfo(exePath).FileVersion ?? "";
        }
        catch { }
        return "";
    }

    private static string Sha256Hex(string path)
    {
        using var sha = SHA256.Create();
        using var fs = File.OpenRead(path);
        return Convert.ToHexString(sha.ComputeHash(fs));
    }

    private static void MirrorDir(string src, string dest)
    {
        if (Directory.Exists(dest)) Directory.Delete(dest, true);
        Directory.CreateDirectory(dest);
        foreach (var dir in Directory.GetDirectories(src, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(dir.Replace(src, dest));
        foreach (var file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
        {
            var to = file.Replace(src, dest);
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            File.Copy(file, to, true);
        }
    }

    // ---------------------------------------------------------------
    // UI helpers
    // ---------------------------------------------------------------
    private bool EnsureBase()
    {
        if (!string.IsNullOrEmpty(_baseDir) && Directory.Exists(_baseDir)) return true;
        MessageBox.Show(this,
            Tr("Pilih folder game (base) dulu ya.", "Pick the base game folder first."),
            Tr("Base belum dipilih", "Base not selected"),
            MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return false;
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        foreach (var b in _switchButtons.Values) b.Enabled = !busy;
        _btnLaunch.Enabled = !busy;
        UseWaitCursor = busy;
    }

    private void RefreshUi()
    {
        if (string.IsNullOrEmpty(_baseDir))
        {
            _lblCurrent.Text = Tr("Platform aktif: -", "Active platform: -");
            return;
        }

        var current = DetectCurrent() ?? LoadSettings().Current;
        _lblCurrent.Text = Tr("Platform aktif: ", "Active platform: ") +
                           (string.IsNullOrEmpty(current) ? "?" : current);

        var ver = GetExeVersion(Path.Combine(_baseDir, "GTA5_Enhanced.exe"));
        _lblVersion.Text = string.IsNullOrEmpty(ver) ? "" : Tr($"   (versi game {ver})", $"   (game version {ver})");

        foreach (var p in Platforms)
        {
            var set = LoadSet(p);
            var b = _switchButtons[p];
            string line2 = set == null
                ? Tr("(foldernya nggak ada)", "(folder missing)")
                : !set.Ready
                    ? Tr("(belum lengkap)", "(incomplete)")
                    : string.IsNullOrEmpty(set.ExeVersion) ? "" : $"v{set.ExeVersion}";
            b.Text = $"{p}{(p == current ? Tr("  ● AKTIF", "  ● ACTIVE") : "")}\n{line2}";
            if (p == current)
            {
                b.BackColor = GoldColor;
                b.ForeColor = Color.FromArgb(24, 24, 24);
                b.FlatAppearance.BorderColor = GoldColor;
            }
            else
            {
                b.BackColor = PanelColor;
                b.ForeColor = TextColor;
                b.FlatAppearance.BorderColor = BorderColor;
            }
        }
        _btnLaunch.Text = string.IsNullOrEmpty(current) ? "Launch" : $"▶ Launch ({current})";
    }

    private void Log(string msg)
    {
        if (InvokeRequired) { BeginInvoke(() => Log(msg)); return; }
        var line = $"[{DateTime.Now:HH:mm:ss}] {msg}";
        _log.AppendText(line + Environment.NewLine);
        try
        {
            if (!string.IsNullOrEmpty(_baseDir) && Directory.Exists(SwitcherDir) || Directory.Exists(_baseDir))
            {
                Directory.CreateDirectory(SwitcherDir);
                File.AppendAllText(Path.Combine(SwitcherDir, "Switcher.log"), line + Environment.NewLine);
            }
        }
        catch { }
    }
}
