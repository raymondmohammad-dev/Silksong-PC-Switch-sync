using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using MediaDevices;
using System.Text.Json;

namespace SwitchFileSync
{
    public partial class MainForm : Form
    {
        private bool TestMode = true; // set to true to enable test mode with local folders instead of MTP
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

            if ((switchDevice == null || !switchDevice.IsConnected) && !TestMode)
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
            else if (TestMode)
            {
                MessageBox.Show("Test mode enabled: no Switch detected, using local folders instead.");
            }
            else
            {
                MessageBox.Show("Switch is not connected. Please connect it via USB in MTP mode.");
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
                if (switchDevice == null || !switchDevice.IsConnected)
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

                if (switchPlaytime.Value == pcPlaytime.Value)
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
                string backupRoot = CreateBackupRoot();
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
                if (switchDevice == null || !switchDevice.IsConnected)
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
                string backupRoot = CreateBackupRoot();
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
            string path = node.Tag.ToString();

            try
            {
                string[] dirs;
                dirs = Directory.GetDirectories(path);
                if (switchDevice != null)
                    dirs = switchDevice.GetDirectories(path);

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

        private void treeSwitchExplorer_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            if (e.Node.Nodes.Count == 1 && e.Node.Nodes[0].Text == "Loading...")
            {
                e.Node.Nodes.Clear();
                LoadDirectories(e.Node);
            }
        }

        private void treeSwitchExplorer_AfterSelect(object sender, TreeViewEventArgs e)
        {
            txtSwitchPath.Text = e.Node.Tag.ToString();
            LoadPlaytimeFromSwitch(txtSwitchPath.Text);
        }

        // Recursively copy from Switch → PC
        private void CopyFromSwitchRecursive(string switchPath, string pcPath, Action<int> reportProgress, ref int count, int total)
        {
            Directory.CreateDirectory(pcPath);

            // Copy files from the current folder
            var files = switchDevice.GetFiles(switchPath);
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

                // Report progress
                count++;
                int progress = (int)((count / (float)total) * 100);
                reportProgress(progress);
            }

            // Process subfolders
            var dirs = switchDevice.GetDirectories(switchPath);
            foreach (var dir in dirs)
            {
                string localSubDir = Path.Combine(pcPath, Path.GetFileName(dir));
                CopyFromSwitchRecursive(dir, localSubDir, reportProgress, ref count, total);
            }
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

                if (switchDevice.FileExists(targetFile))
                {
                    switchDevice.DeleteFile(targetFile);
                }

                using (FileStream fs = File.OpenRead(tempFile))
                {
                    switchDevice.UploadFile(fs, targetFile);
                }

                // Update progress
                count++;
                int progress = (int)((count / (float)total) * 100);
                reportProgress(progress);
            }

            // Process subfolders
            foreach (string dir in Directory.GetDirectories(pcPath))
            {
                string subDirName = Path.GetFileName(dir);
                string switchSubDir = Path.Combine(switchPath, subDirName).Replace("\\", "/");

                if (!switchDevice.DirectoryExists(switchSubDir))
                {
                    switchDevice.CreateDirectory(switchSubDir);
                }

                CopyFromPcRecursive(dir, switchSubDir, reportProgress, ref count, total);
            }
        }

        private int CountSwitchFiles(string switchPath)
        {
            int count = switchDevice.GetFiles(switchPath).Count(f => f.EndsWith(".dat"));

            foreach (var dir in switchDevice.GetDirectories(switchPath))
            {
                count += CountSwitchFiles(dir);
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
        private void LoadPlaytimeFromSwitch(string switchPath)
        {
            string filePath = Path.Combine(switchPath, "user1.dat").Replace("\\", "/");
            if ((switchDevice == null || !switchDevice.IsConnected || !switchDevice.FileExists(filePath)) && !TestMode)
            {
                switchPlaytime = null;
                lblPlaytimeSwitch.Text = "Playtime Switch: N/A";
                ComparePlaytimes();
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
        private string CreateBackupRoot()
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backups", timestamp);
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
            Directory.CreateDirectory(destDir);

            // Copy files
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string target = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, target, true);
            }

            // Recursion in subfolders
            foreach (string dir in Directory.GetDirectories(sourceDir))
            {
                string subDest = Path.Combine(destDir, Path.GetFileName(dir));
                CopyDirectory(dir, subDest);
            }
        }

        private void BackupFromSwitch(string switchPath, string backupRoot)
        {
            string dest = Path.Combine(backupRoot, "switch");
            CopyFromSwitchRecursiveBack(switchPath, dest);
        }

        private void CopyFromSwitchRecursiveBack(string switchPath, string pcPath)
        {
            Directory.CreateDirectory(pcPath);

            // Copy files from the current folder
            var files = switchDevice.GetFiles(switchPath);
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
            var dirs = switchDevice.GetDirectories(switchPath);
            foreach (var dir in dirs)
            {
                string localSubDir = Path.Combine(pcPath, Path.GetFileName(dir));
                CopyFromSwitchRecursiveBack(dir, localSubDir);
            }
        }
    }
}
