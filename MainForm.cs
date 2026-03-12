using MediaDevices;
using System.Text.Json;

namespace SwitchFileSync
{
    public partial class MainForm : Form
    {
        private bool LocalMode = false; // set to true to enable test mode with local folders instead of MTP
        private MediaDevice? switchDevice;
        private AppConfig config;
        private double? pcPlaytime = null;
        private double? switchPlaytime = null;

        public MainForm()
        {
            InitializeComponent();

            this.Load += (s, e) =>
            {
                float scale = this.DeviceDpi / 96f;

                if (scale > 1.4f) // ~150% or more
                {
                    this.MinimumSize = new System.Drawing.Size(800, 600);
                }
                else if (scale > 1.2f) // ~125%
                {
                    this.MinimumSize = new System.Drawing.Size(600, 500);
                }
                else // 100%
                {
                    this.MinimumSize = new System.Drawing.Size(600, 450);
                }
            };

            // load configuration
            config = AppConfig.Load();
            txtPcPath.Text = config.PcPath;
            txtSwitchPath.Text = config.SwitchPath;
            progressBar.Visible = false;

            DetectSwitch();

            if ((switchDevice == null || !switchDevice.IsConnected) && !LocalMode)
            {
                this.Close();
                return;
            }

            LoadSwitchExplorer();
            treeSwitchExplorer.BeforeExpand += treeSwitchExplorer_BeforeExpand;

            if (!string.IsNullOrWhiteSpace(txtPcPath.Text))
                LoadPlaytimeFromPc(txtPcPath.Text);

            if (!string.IsNullOrWhiteSpace(txtSwitchPath.Text))
                LoadPlaytimeFromSwitch(txtSwitchPath.Text);
        }

        private void DetectSwitch()
        {
            var devices = MediaDevice.GetDevices();
            switchDevice = devices.FirstOrDefault();

            if (switchDevice != null)
            {
                switchDevice.Connect();
                MessageBox.Show("Switch detected via MTP.");
            }
            else
            {
                this.LocalMode = true;
                MessageBox.Show("Local mode enabled: no Switch detected, using local folders instead.");
            }
        }

        private void btnBrowsePc_Click(object sender, EventArgs e)
        {
            using var fbd = new FolderBrowserDialog();
            if (fbd.ShowDialog() == DialogResult.OK)
            {
                txtPcPath.Text = fbd.SelectedPath;
                LoadPlaytimeFromPc(fbd.SelectedPath);
            }
        }

        private void btnBrowseSwitch_Click(object sender, EventArgs e)
        {
            using var fbd = new FolderBrowserDialog();
            if (fbd.ShowDialog() == DialogResult.OK)
            {
                txtSwitchPath.Text = fbd.SelectedPath;
                LoadPlaytimeFromSwitch(fbd.SelectedPath);
            }
        }

        private async void btnSendToSwitch_Click(object sender, EventArgs e)
        {
            try
            {
                if ((switchDevice == null || !switchDevice.IsConnected) && !LocalMode)
                {
                    MessageBox.Show("Switch is not connected. Please connect it via USB.",
                                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                string pcPath = txtPcPath.Text;
                string switchPath = txtSwitchPath.Text;
                if (!Directory.Exists(pcPath))
                {
                    MessageBox.Show("Invalid PC path.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (switchPlaytime.HasValue && pcPlaytime.HasValue && switchPlaytime.Value == pcPlaytime.Value)
                {
                    MessageBox.Show("The saves are already synchronized.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (pcPlaytime.HasValue && switchPlaytime.HasValue && pcPlaytime.Value < switchPlaytime.Value)
                {
                    var result = MessageBox.Show(
                        "Warning: You are about to overwrite the Switch save with an older PC save.\nDo you want to continue?",
                        "Older Save Detected",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);

                    if (result == DialogResult.No)
                        return;
                }

                progressBar.Visible = true;
                progressBar.Value = 0;

                // Backup
                // TODO fix recursion
                string backupRoot = CreateBackupRoot(pcPath);
                BackupFromPc(pcPath, backupRoot);
                BackupFromSwitch(switchPath, backupRoot);

                // Upload

                int total = Directory.GetFiles(pcPath, "*.dat", SearchOption.AllDirectories).Length;
                int count = 0;

                // Copy in background with progress
                await Task.Run(() =>
                {
                    CopyFromPcRecursive(pcPath, switchPath,
                        progress => this.Invoke(new Action(() => progressBar.Value = progress)),
                        ref count, total);
                });

                LoadPlaytimeFromPc(txtPcPath.Text);
                LoadPlaytimeFromSwitch(txtSwitchPath.Text);

                MessageBox.Show("Files uploaded to Switch (backup created).", "Success");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during upload: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                progressBar.Visible = false;
            }
        }

        private async void btnSendToPc_Click(object sender, EventArgs e)
        {
            try
            {
                if ((switchDevice == null || !switchDevice.IsConnected) && !LocalMode)
                {
                    MessageBox.Show("Switch is not connected. Please connect it via USB.",
                                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                string pcPath = txtPcPath.Text;
                string switchPath = txtSwitchPath.Text;

                if (!Directory.Exists(pcPath))
                {
                    MessageBox.Show("Invalid PC path.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (switchPlaytime.HasValue && pcPlaytime.HasValue && switchPlaytime.Value == pcPlaytime.Value)
                {
                    MessageBox.Show("The saves are already synchronized.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // Count files before
                int total = CountSwitchFiles(switchPath);
                if (total == 0)
                {
                    MessageBox.Show("No save files found on Switch.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                int count = 0;

                progressBar.Visible = true;
                progressBar.Value = 0;

                // Backup
                // TODO fix recursion
                string backupRoot = CreateBackupRoot(pcPath);
                BackupFromPc(pcPath, backupRoot);
                BackupFromSwitch(switchPath, backupRoot);

                // Copy in background with progress
                await Task.Run(() =>
                {
                    CopyFromSwitchRecursive(switchPath, pcPath,
                        progress => this.Invoke(new Action(() => progressBar.Value = progress)),
                        ref count, total);
                });

                LoadPlaytimeFromPc(txtPcPath.Text);
                LoadPlaytimeFromSwitch(txtSwitchPath.Text);

                MessageBox.Show("Files downloaded from Switch (backup created).", "Success");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during download: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                progressBar.Visible = false;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            // save configuration
            config.PcPath = txtPcPath.Text;
            config.SwitchPath = txtSwitchPath.Text;
            config.Save();
        }

        private void LoadSwitchExplorer()
        {
            if (switchDevice == null) // Test mode
            {
                treeSwitchExplorer.Nodes.Clear();

                var rootNode = new TreeNode("C:\\") { Tag = @"C:\" };
                treeSwitchExplorer.Nodes.Add(rootNode);

                LoadDirectories(rootNode);
            }
            else
            {
                treeSwitchExplorer.Nodes.Clear();

                var rootNode = new TreeNode("Switch (MTP)") { Tag = "\\" };
                treeSwitchExplorer.Nodes.Add(rootNode);

                LoadDirectories(rootNode);
            }
        }

        private void LoadDirectories(TreeNode node)
        {
            var path = node.Tag.ToString();

            try
            {
                string[] dirs = Array.Empty<string>();

                if (switchDevice != null)
                    dirs = switchDevice.GetDirectories(path);
                else if (!string.IsNullOrEmpty(path))
                    dirs = Directory.GetDirectories(path);

                foreach (var dir in dirs)
                {
                    var child = new TreeNode(Path.GetFileName(dir)) { Tag = dir };
                    // Add an empty node to show the expandable "+" sign
                    child.Nodes.Add(new TreeNode("Loading..."));
                    node.Nodes.Add(child);
                }
            }
            catch
            {
                // If the folder cannot be accessed, we ignore it
            }
        }

        private void treeSwitchExplorer_BeforeExpand(object? sender, TreeViewCancelEventArgs e)
        {
            if (e.Node?.Nodes.Count == 1 && e.Node.Nodes[0].Text == "Loading...")
            {
                e.Node.Nodes.Clear();
                LoadDirectories(e.Node);
            }
        }

        private void treeSwitchExplorer_AfterSelect(object sender, TreeViewEventArgs e)
        {
            txtSwitchPath.Text = e.Node?.Tag.ToString();
            LoadPlaytimeFromSwitch(txtSwitchPath.Text);
        }

        // Recursively copy from Switch → PC
        private void CopyFromSwitchRecursive(string switchPath, string pcPath, Action<int> reportProgress, ref int count, int total)
        {
            if (!Directory.Exists(pcPath) && !File.Exists(pcPath))
                Directory.CreateDirectory(pcPath);

            string[] files;
            // Copy files from the current folder
            if (switchDevice != null)
            {
                files = switchDevice.GetFiles(switchPath);
            }
            else
            {
                if (!File.Exists(switchPath))
                    files = Directory.GetFiles(switchPath);
                else
                    files = Array.Empty<string>();
            }

            foreach (var file in files.Where(f => f.EndsWith(".dat")))
            {
                string localFile = Path.Combine(pcPath, Path.GetFileName(file));

                if (switchDevice != null)
                {
                    using (var fs = File.Create(localFile))
                    {
                        switchDevice.DownloadFile(file, fs);
                    }
                }
                else
                {
                    using (var sourceStream = File.OpenRead(file))
                    using (var destStream = File.Create(localFile))
                    {
                        sourceStream.CopyTo(destStream);
                    }
                }

                // (optional) encrypt here if applicable
                string json = File.ReadAllText(localFile);
                SaveFileEncoder.EncodeDatFile(json, localFile);

                // Report progress
                count++;
                int progress = (int)((count / (float)total) * 100);
                reportProgress(progress);
            }

            // Process subfolders
            string[] dirs;
            if (switchDevice != null)
            {
                dirs = switchDevice.GetDirectories(switchPath);
            }
            else
            {
                if (!File.Exists(switchPath))
                    dirs = Directory.GetFiles(switchPath);
                else
                    dirs = Array.Empty<string>();
            }

            /*foreach (var dir in dirs)
            {
                string localSubDir = Path.Combine(pcPath, Path.GetFileName(dir));
                CopyFromSwitchRecursive(dir, localSubDir, reportProgress, ref count, total);
            }*/
        }

        // Recursively copy from PC → Switch
        private void CopyFromPcRecursive(string pcPath, string switchPath, Action<int> reportProgress, ref int count, int total)
        {
            // Copy local files
            foreach (string file in Directory.GetFiles(pcPath, "*.dat"))
            {
                string json = SaveFileEncoder.DecodeDatFile(file);
                string tempFile = Path.Combine(Path.GetTempPath(), Path.GetFileName(file));
                File.WriteAllText(tempFile, json);

                string targetFile = Path.Combine(switchPath, Path.GetFileName(file)).Replace("\\", "/");

                if (switchDevice != null)
                {
                    if (switchDevice.FileExists(targetFile))
                    {
                        switchDevice.DeleteFile(targetFile);
                    }

                    using (FileStream fs = File.OpenRead(tempFile))
                    {
                        switchDevice.UploadFile(fs, targetFile);
                    }
                }
                else
                {
                    if (File.Exists(targetFile))
                    {
                        File.Delete(targetFile);
                    }

                    File.Copy(tempFile, targetFile);

                }

                // Update progress
                count++;
                int progress = (int)((count / (float)total) * 100);
                reportProgress(progress);
            }

            // Process subfolders
            /*foreach (string dir in Directory.GetDirectories(pcPath))
            {
                string subDirName = Path.GetFileName(dir);
                string switchSubDir = Path.Combine(switchPath, subDirName).Replace("\\", "/");

                if (switchDevice != null)
                {
                    if (!switchDevice.DirectoryExists(switchSubDir))
                    {
                        switchDevice.CreateDirectory(switchSubDir);
                    }
                }
                else
                {
                    if (!Directory.Exists(switchSubDir))
                    {
                        Directory.CreateDirectory(switchSubDir);
                    }
                }

                CopyFromPcRecursive(dir, switchSubDir, reportProgress, ref count, total);
            }*/
        }

        private int CountSwitchFiles(string switchPath)
        {
            int count;
            if (switchDevice != null)
            {
                count = switchDevice.GetFiles(switchPath).Count(f => f.EndsWith(".dat"));
                foreach (var dir in switchDevice.GetDirectories(switchPath))
                {
                    count += CountSwitchFiles(dir);
                }
            }
            else
            {
                count = Directory.GetFiles(switchPath).Count(f => f.EndsWith(".dat"));
                foreach (var dir in Directory.GetDirectories(switchPath))
                {
                    count += CountSwitchFiles(dir);
                }
            }

            return count;
        }

        private void LoadPlaytimeFromPc(string pcPath)
        {
            string filePath = Path.Combine(pcPath, "user1.dat");
            if (!File.Exists(filePath))
            {
                pcPlaytime = null;
                lblPlaytimePc.Text = "Playtime PC: N/A";
                ComparePlaytimes();
                return;
            }

            try
            {
                string json = SaveFileEncoder.DecodeDatFile(filePath);
                var pt = TryGetPlaytime(json);

                if (pt.HasValue)
                {
                    pcPlaytime = pt.Value;
                    lblPlaytimePc.Text = $"Playtime PC: {FormatSecondsAsHMS(pcPlaytime.Value)}";
                }
                else
                {
                    pcPlaytime = null;
                    lblPlaytimePc.Text = "Playtime PC: not found";
                }
            }
            catch
            {
                pcPlaytime = null;
                lblPlaytimePc.Text = "Playtime PC: error";
            }

            ComparePlaytimes();
        }

        // Read playTime from Switch
        private void LoadPlaytimeFromSwitch(string? switchPath)
        {
            string filePath = "";
            if (string.IsNullOrWhiteSpace(switchPath))
            {
                SwitchPlaytimeInvalid();
                return;
            }
            else
                filePath = Path.Combine(switchPath, "user1.dat").Replace("\\", "/");

            if ((switchDevice == null || !switchDevice.IsConnected || !switchDevice.FileExists(filePath)) && !File.Exists(filePath))
            {
                SwitchPlaytimeInvalid();
                return;
            }

            try
            {
                string json;
                Stream stream;
                if (switchDevice != null)
                {
                    stream = new MemoryStream();
                    switchDevice.DownloadFile(filePath, stream);
                    stream.Position = 0;
                }
                else
                {
                    stream = File.OpenRead(filePath);
                }

                using (stream)
                using (var reader = new StreamReader(stream))
                {
                    json = reader.ReadToEnd();
                }

                var pt = TryGetPlaytime(json);

                if (pt.HasValue)
                {
                    switchPlaytime = pt.Value;
                    lblPlaytimeSwitch.Text = $"Playtime Switch: {FormatSecondsAsHMS(switchPlaytime.Value)}";
                }
                else
                {
                    switchPlaytime = null;
                    lblPlaytimeSwitch.Text = "Playtime Switch: not found";
                }
            }
            catch
            {
                switchPlaytime = null;
                lblPlaytimeSwitch.Text = "Playtime Switch: error";
            }

            ComparePlaytimes();
        }

        private void SwitchPlaytimeInvalid()
        {
            switchPlaytime = null;
            lblPlaytimeSwitch.Text = "Playtime Switch: N/A";
            ComparePlaytimes();
        }

        private static string FormatSecondsAsHMS(double seconds)
        {
            var ts = TimeSpan.FromSeconds(seconds);
            return $"{(int)ts.TotalHours}h {ts.Minutes}m {ts.Seconds}s";
        }

        private double? TryGetPlaytime(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Current known path: playerData.playTime
            if (root.TryGetProperty("playerData", out var playerData) &&
                playerData.TryGetProperty("playTime", out var ptKnown) &&
                ptKnown.TryGetDouble(out double dKnown))
            {
                return dKnown;
            }

            // Fallback: recursive search in case the structure changes
            return FindDoublePropertyRecursive(root, "playTime");
        }

        private double? FindDoublePropertyRecursive(JsonElement el, string name)
        {
            switch (el.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var prop in el.EnumerateObject())
                    {
                        if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase) &&
                            prop.Value.ValueKind == JsonValueKind.Number &&
                            prop.Value.TryGetDouble(out double d))
                        {
                            return d;
                        }
                        var nested = FindDoublePropertyRecursive(prop.Value, name);
                        if (nested.HasValue) return nested;
                    }
                    break;

                case JsonValueKind.Array:
                    foreach (var item in el.EnumerateArray())
                    {
                        var nested = FindDoublePropertyRecursive(item, name);
                        if (nested.HasValue) return nested;
                    }
                    break;
            }
            return null;
        }
        private void ComparePlaytimes()
        {
            if (!pcPlaytime.HasValue || !switchPlaytime.HasValue)
            {
                lblPlaytimePc.ForeColor = System.Drawing.Color.Black;
                lblPlaytimeSwitch.ForeColor = System.Drawing.Color.Black;
                return;
            }

            if (Math.Abs(pcPlaytime.Value - switchPlaytime.Value) < 0.001) // almost equal
            {
                lblPlaytimePc.ForeColor = System.Drawing.Color.Blue;
                lblPlaytimeSwitch.ForeColor = System.Drawing.Color.Blue;

                lblPlaytimePc.Text += " (Synchronized)";
                lblPlaytimeSwitch.Text += " (Synchronized)";
            }
            else if (pcPlaytime > switchPlaytime)
            {
                lblPlaytimePc.ForeColor = System.Drawing.Color.Green;
                lblPlaytimeSwitch.ForeColor = System.Drawing.Color.Red;
            }
            else
            {
                lblPlaytimePc.ForeColor = System.Drawing.Color.Red;
                lblPlaytimeSwitch.ForeColor = System.Drawing.Color.Green;
            }
        }

        // backup
        private string CreateBackupRoot(string path)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string root = Path.Combine(path, "backups", timestamp);
            Directory.CreateDirectory(root);
            return root;
        }

        private void BackupFromPc(string pcPath, string backupRoot)
        {
            string dest = Path.Combine(backupRoot, "pc");
            CopyDirectory(pcPath, dest);
        }

        private void CopyDirectory(string sourceDir, string destDir)
        {
            // Copy files
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string target = Path.Combine(destDir, Path.GetFileName(file));
                if (!Directory.Exists(destDir))
                    Directory.CreateDirectory(destDir);
                File.Copy(file, target, true);
            }

            // Recursion in subfolders
            /*foreach (string dir in Directory.GetDirectories(sourceDir))
            {
                string subDest = Path.Combine(destDir, Path.GetFileName(dir));
                CopyDirectory(dir, subDest);
            }*/
        }

        private void BackupFromSwitch(string switchPath, string backupRoot)
        {
            string dest = Path.Combine(backupRoot, "switch");
            CopyFromSwitchRecursiveBack(switchPath, dest);
        }

        private void CopyFromSwitchRecursiveBack(string switchPath, string pcPath)
        {
            string[] files;
            // Copy files from the current folder
            if (switchDevice != null)
            {
                files = switchDevice.GetFiles(switchPath);
                foreach (var file in files.Where(f => f.EndsWith(".dat")))
                {
                    string localFile = Path.Combine(pcPath, Path.GetFileName(file));

                    using (var fs = File.Create(localFile))
                    {
                        switchDevice.DownloadFile(file, fs);
                    }

                    // (optional) encrypt here if applicable
                    string json = File.ReadAllText(localFile);
                    SaveFileEncoder.EncodeDatFile(json, localFile);
                }

                // Process subfolders
                /*var dirs = switchDevice.GetDirectories(switchPath);
                foreach (var dir in dirs)
                {
                    string localSubDir = Path.Combine(pcPath, Path.GetFileName(dir));
                    CopyFromSwitchRecursiveBack(dir, localSubDir);
                }*/
            }
            else
            {
                files = Directory.GetFiles(switchPath);
                foreach (var file in files.Where(f => f.EndsWith(".dat")))
                {
                    string localFile = Path.Combine(pcPath, Path.GetFileName(file));

                    if (!Directory.Exists(pcPath))
                        Directory.CreateDirectory(pcPath);

                    using (var sourceStream = File.OpenRead(file))
                    using (var destStream = File.Create(localFile))
                    {
                        sourceStream.CopyTo(destStream);
                    }

                    // (optional) encrypt here if applicable
                    string json = File.ReadAllText(localFile);
                    SaveFileEncoder.EncodeDatFile(json, localFile);
                }

                // Process subfolders
                /*var dirs = Directory.GetDirectories(switchPath);
                foreach (var dir in dirs)
                {
                    string localSubDir = Path.Combine(pcPath, Path.GetFileName(dir));
                    CopyFromSwitchRecursiveBack(dir, localSubDir);
                }*/
            }

        }
    }
}
