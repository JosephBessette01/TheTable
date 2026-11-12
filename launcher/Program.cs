using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace TheTableLauncher;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var mutex = new Mutex(true, @"Local\TheTableLauncher", out bool firstInstance);
        if (!firstInstance)
        {
            MessageBox.Show("The Table launcher is already running.", "The Table");
            return;
        }
        Application.Run(new LauncherWindow());
    }
}

internal sealed class LauncherWindow : Form
{
    private const string ReleaseApi = "https://api.github.com/repos/JosephBessette01/TheTable/releases/latest";
    private const string ZipAsset = "TheTable-Windows.zip";
    private const string HashAsset = "TheTable-Windows.zip.sha256";
    private const string GameExe = "TheTableGame.exe";
    private readonly string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TheTable");
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromMinutes(10) };
    private readonly Label status = new() { AutoSize = false, TextAlign = System.Drawing.ContentAlignment.MiddleCenter };
    private readonly Label detail = new() { AutoSize = false, TextAlign = System.Drawing.ContentAlignment.MiddleCenter };
    private readonly Button launch = new() { Text = "CHECKING FOR UPDATES..." };
    private readonly Button retry = new() { Text = "RETRY", Visible = false };
    private readonly ProgressBar progress = new() { Style = ProgressBarStyle.Marquee, Visible = false };
    private bool busy;

    private string GameDir => Path.Combine(root, "game");
    private string BackupDir => Path.Combine(root, "game.previous");
    private string GamePath => Path.Combine(GameDir, GameExe);
    private string VersionPath => Path.Combine(GameDir, "installed-version.txt");
    private bool HasGame => File.Exists(GamePath);

    internal LauncherWindow()
    {
        Text = "THE TABLE // LAUNCHER";
        ClientSize = new System.Drawing.Size(510, 315);
        MinimumSize = MaximumSize = Size;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = System.Drawing.Color.FromArgb(15, 25, 17);
        ForeColor = System.Drawing.Color.FromArgb(185, 255, 194);
        Font = new System.Drawing.Font("Segoe UI", 10);

        var heading = new Label
        {
            Text = "THE TABLE_",
            Font = new System.Drawing.Font("Segoe UI", 32, System.Drawing.FontStyle.Bold),
            TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
            ForeColor = ForeColor,
            Bounds = new System.Drawing.Rectangle(20, 22, 470, 72)
        };
        status.Text = "Starting launcher...";
        status.Bounds = new System.Drawing.Rectangle(25, 108, 460, 30);
        detail.ForeColor = System.Drawing.Color.FromArgb(164, 183, 165);
        detail.Bounds = new System.Drawing.Rectangle(25, 142, 460, 43);
        launch.Bounds = new System.Drawing.Rectangle(110, 201, 290, 42);
        launch.BackColor = System.Drawing.Color.FromArgb(39, 74, 44);
        launch.ForeColor = System.Drawing.Color.White;
        launch.FlatStyle = FlatStyle.Flat;
        launch.Enabled = false;
        launch.Click += (_, _) => StartGame();
        retry.Bounds = new System.Drawing.Rectangle(215, 261, 80, 30);
        retry.Click += async (_, _) => await CheckUpdatesAsync();
        progress.Bounds = new System.Drawing.Rectangle(110, 250, 290, 7);
        Controls.AddRange(new Control[] { heading, status, detail, launch, retry, progress });
        http.DefaultRequestHeaders.UserAgent.ParseAdd("TheTableLauncher/0.1");
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        Shown += async (_, _) => await CheckUpdatesAsync();
    }

    private void SetStatus(string headline, string description)
    {
        if (IsDisposed) return;
        status.Text = headline;
        detail.Text = description;
    }

    private async Task CheckUpdatesAsync()
    {
        if (busy) return;
        busy = true;
        retry.Visible = false;
        progress.Visible = true;
        launch.Enabled = HasGame;
        launch.Text = HasGame ? "PLAY INSTALLED VERSION" : "PREPARING...";
        SetStatus("Checking for updates...", "Connecting to official GitHub Releases");
        try
        {
            Directory.CreateDirectory(root);
            // An unpublished repository is a valid state before our first public build.
            using var response = await http.GetAsync(ReleaseApi, HttpCompletionOption.ResponseHeadersRead);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                throw new InvalidOperationException("No public game release has been published yet.");
            response.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var release = doc.RootElement;
            if (release.GetProperty("draft").GetBoolean() || release.GetProperty("prerelease").GetBoolean())
                throw new InvalidOperationException("The latest release is not approved for public distribution.");
            string tag = release.GetProperty("tag_name").GetString() ?? "";
            if (!Regex.IsMatch(tag, @"^game-v[0-9]+\.[0-9]+\.[0-9]+$"))
                throw new InvalidOperationException("Latest release isn't a game release; expected game-v1.0.0.");
            string installedTag = File.Exists(VersionPath) ? File.ReadAllText(VersionPath).Trim() : "";
            if (HasGame && installedTag == tag)
            {
                SetStatus("READY TO PLAY", $"Installed version: {tag}");
                return;
            }

            var assets = release.GetProperty("assets").EnumerateArray().ToArray();
            string FindAsset(string name)
            {
                foreach (var asset in assets)
                    if (asset.GetProperty("name").GetString() == name)
                    {
                        var url = asset.GetProperty("browser_download_url").GetString() ?? "";
                        if (Uri.TryCreate(url, UriKind.Absolute, out var uri)
                            && uri.Scheme == "https"
                            && uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
                            && uri.AbsolutePath.StartsWith("/JosephBessette01/TheTable/releases/download/", StringComparison.Ordinal))
                            return url;
                    }
                throw new InvalidOperationException($"Release is missing a valid {name} asset.");
            }
            var zipUrl = FindAsset(ZipAsset);
            var hashUrl = FindAsset(HashAsset);
            SetStatus($"Installing {tag}", "Downloading verified game files...");
            string shaText = await http.GetStringAsync(hashUrl);
            var match = Regex.Match(shaText.Trim(), @"\A([a-fA-F0-9]{64})(?:\s+\*?TheTable-Windows\.zip)?\s*\z");
            if (!match.Success) throw new InvalidDataException("Invalid SHA-256 checksum format.");
            string expectedHash = match.Groups[1].Value;

            string tempZip = Path.Combine(root, "download-" + Guid.NewGuid().ToString("N") + ".zip");
            string stage = Path.Combine(root, "staging-" + Guid.NewGuid().ToString("N"));
            try
            {
                using (var download = await http.GetAsync(zipUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    download.EnsureSuccessStatusCode();
                    if (download.Content.Headers.ContentLength is > 3_000_000_000L)
                        throw new InvalidDataException("Game download exceeds size limit.");
                    await using var source = await download.Content.ReadAsStreamAsync();
                    await using var output = File.Create(tempZip);
                    byte[] buffer = new byte[128 * 1024];
                    long downloaded = 0;
                    int count;
                    while ((count = await source.ReadAsync(buffer)) > 0)
                    {
                        downloaded += count;
                        if (downloaded > 3_000_000_000L) throw new InvalidDataException("Game download exceeds size limit.");
                        await output.WriteAsync(buffer.AsMemory(0, count));
                    }
                }
                SetStatus($"Installing {tag}", "Verifying SHA-256 checksum...");
                await using (var stream = File.OpenRead(tempZip))
                {
                    var digest = Convert.ToHexString(await SHA256.HashDataAsync(stream));
                    if (!digest.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("The downloaded ZIP failed SHA-256 verification.");
                }

                SetStatus($"Installing {tag}", "Extracting into a safe staging folder...");
                Directory.CreateDirectory(stage);
                using (var archive = ZipFile.OpenRead(tempZip))
                {
                    long total = 0;
                    foreach (var entry in archive.Entries)
                    {
                        total += entry.Length;
                        if (total > 5_000_000_000L) throw new InvalidDataException("Uncompressed game is too large.");
                        var relative = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
                        string dest = Path.GetFullPath(Path.Combine(stage, relative));
                        if (!dest.StartsWith(Path.GetFullPath(stage) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException("Unsafe ZIP entry path.");
                        if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000)
                            throw new InvalidDataException("ZIP symlinks are not allowed.");
                    }
                    archive.ExtractToDirectory(stage);
                }
                if (!File.Exists(Path.Combine(stage, GameExe)))
                    throw new InvalidDataException($"Game archive must contain {GameExe} at its root.");
                File.WriteAllText(Path.Combine(stage, "installed-version.txt"), tag);

                // No running game files are modified until download, hash and extraction have passed.
                if (Directory.Exists(BackupDir)) Directory.Delete(BackupDir, recursive: true);
                bool movedOld = false;
                try
                {
                    if (Directory.Exists(GameDir))
                    {
                        Directory.Move(GameDir, BackupDir);
                        movedOld = true;
                    }
                    Directory.Move(stage, GameDir);
                }
                catch
                {
                    if (movedOld && !Directory.Exists(GameDir) && Directory.Exists(BackupDir))
                        Directory.Move(BackupDir, GameDir);
                    throw;
                }
                SetStatus("UPDATE COMPLETE", $"Installed version: {tag}");
            }
            finally
            {
                try { if (File.Exists(tempZip)) File.Delete(tempZip); } catch { }
                try { if (Directory.Exists(stage)) Directory.Delete(stage, true); } catch { }
            }
        }
        catch (Exception ex)
        {
            SetStatus(HasGame ? "OFFLINE / UPDATE UNAVAILABLE" : "GAME NOT AVAILABLE YET",
                ex.Message.Length > 150 ? ex.Message[..150] : ex.Message);
            retry.Visible = true;
        }
        finally
        {
            busy = false;
            progress.Visible = false;
            launch.Enabled = HasGame;
            launch.Text = HasGame ? "PLAY THE TABLE" : "NO GAME INSTALLED";
        }
    }

    private void StartGame()
    {
        if (!HasGame) return;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = GamePath,
                WorkingDirectory = GameDir,
                UseShellExecute = true
            });
            Close();
        }
        catch (Exception ex)
        {
            SetStatus("COULDN'T LAUNCH", ex.Message);
            retry.Visible = true;
        }
    }
}
