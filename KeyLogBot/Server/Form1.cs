using System.Data;

namespace Server
{
    public partial class Form1 : Form
    {
        // Định nghĩa các màu sắc để dễ dàng thay đổi và quản lý
        private readonly Color _menuNormalColor = Color.FromArgb(30, 30, 30);
        private readonly Color _menuHoverColor = Color.FromArgb(45, 45, 45);
        private readonly Color _menuSelectedColor = Color.FromArgb(0, 120, 215);
        private readonly int _defaultWidth = 200;// Màu xanh dương giống Windows

        // Dùng một biến để lưu lại mục đang được chọn
        private Panel _selectedMenuItem;
        public Form1()
        {
            InitializeComponent();
            LoadData();
        }

        private void LoadData()
        {
            this.Text = @"Server";
            splitContainer1.SplitterWidth = 1;
            splitContainer1.Panel1.BackColor = _menuNormalColor;

            //
            AddMenuItem("Home", Properties.Resources.main_2);
            AddMenuItem("System", Properties.Resources.main_2);
            AddMenuItem("Bluetooth & devices", Properties.Resources.main_2);
            AddMenuItem("Network & internet", Properties.Resources.main_2);
            AddMenuItem("Personalization", Properties.Resources.main_2);
            AddMenuItem("Apps", Properties.Resources.main_2);
            AddMenuItem("Accounts", Properties.Resources.main_2);
            AddMenuItem("Time & language", Properties.Resources.main_2);
            AddMenuItem("Gaming", Properties.Resources.main_2);
            AddMenuItem("Accessibility", Properties.Resources.main_2);
            AddMenuItem("Privacy & security", Properties.Resources.main_2);
            AddMenuItem("Windows Update", Properties.Resources.main_2);
            // Tự động chọn mục đầu tiên khi form tải lên
            if (flowLayoutPanel1.Controls.Count > 0)
            {
                SelectItem(flowLayoutPanel1.Controls[0] as Panel);
            }
        }

        private void AddMenuItem(string text, Image icon)
        {
            // --- Tạo panel chứa icon + text ---
            Panel item = new Panel
            {
                Height = 40,
                //Dock = DockStyle.Top,
                Padding = new Padding(5),
                Margin = new Padding(0),
                BackColor = _menuNormalColor,
                Cursor = Cursors.Hand, // Thay đổi con trỏ chuột khi di vào
                Tag = text // Lưu lại tên của mục để dễ dàng xác định khi click
            };

            // --- Icon ---
            PictureBox pic = new PictureBox
            {
                Image = icon,
                SizeMode = PictureBoxSizeMode.StretchImage,
                Size = new Size(24, 24),
                Dock = DockStyle.Left
            };

            // --- Text ---
            Label lbl = new Label
            {
                Text = text,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Regular),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0)
            };

            // --- Gán sự kiện ---
            // Gán cùng một sự kiện cho cả Panel, PictureBox và Label
            // để đảm bảo người dùng click vào đâu cũng được
            item.Click += MenuItem_Click;
            lbl.Click += MenuItem_Click;
            pic.Click += MenuItem_Click;

            item.MouseEnter += MenuItem_MouseEnter;
            item.MouseLeave += MenuItem_MouseLeave;

            // --- Thêm control vào Panel ---
            // QUAN TRỌNG: Thêm control dock Left/Right trước, rồi mới thêm control dock Fill
            item.Controls.Add(pic);
            item.Controls.Add(lbl);

            // Thêm mục menu hoàn chỉnh vào FlowLayoutPanel
            flowLayoutPanel1.Controls.Add(item);
        }

        // Sự kiện khi click vào một mục menu
        private void MenuItem_Click(object sender, EventArgs e)
        {
            // Xác định Panel được click (dù click vào Label hay PictureBox)
            Panel clickedItem = (sender is Panel panel) ? panel : (sender as Control)?.Parent as Panel;

            if (clickedItem != null)
            {
                SelectItem(clickedItem);
            }
        }

        // Hàm xử lý logic chọn một mục
        private void SelectItem(Panel itemToSelect)
        {
            // 1. Bỏ chọn mục hiện tại (nếu có)
            if (_selectedMenuItem != null)
            {
                _selectedMenuItem.BackColor = _menuNormalColor;
            }

            // 2. Chọn mục mới
            itemToSelect.BackColor = _menuSelectedColor;
            _selectedMenuItem = itemToSelect;

            // 3. Thực hiện hành động tương ứng
            string selectedText = itemToSelect.Tag.ToString();
            this.Text = $"Server - {selectedText}";

            // TODO: Tại đây bạn có thể gọi các hàm để hiển thị nội dung
            // tương ứng với mục đã chọn. Ví dụ:
            // switch(selectedText)
            // {
            //     case "Home":
            //          ShowHomePage();
            //          break;
            //     case "System":
            //          ShowSystemPage();
            //          break;
            // }
        }

        // Sự kiện khi di chuột vào
        private void MenuItem_MouseEnter(object sender, EventArgs e)
        {
            Panel item = sender is Panel panel ? panel : (sender as Control)?.Parent as Panel;
            if (item != null && item != _selectedMenuItem) // Chỉ đổi màu nếu không phải mục đang được chọn
            {
                item.BackColor = _menuHoverColor;
            }
        }

        // Sự kiện khi di chuột ra
        private void MenuItem_MouseLeave(object sender, EventArgs e)
        {
            Panel item = sender as Panel;
            if (item != null && item != _selectedMenuItem) // Chỉ đổi màu nếu không phải mục đang được chọn
            {
                item.BackColor = _menuNormalColor;
            }
        }

        private void flowLayoutPanel1_Resize(object sender, EventArgs e)
        {
            // Lặp qua tất cả các control con (là các Panel menu item)
            foreach (Control control in flowLayoutPanel1.Controls)
            {
                // Set chiều rộng của item bằng chiều rộng của vùng ClientSize của FlowLayoutPanel.
                // ClientSize không tính thanh cuộn, giúp item không bị che.
                control.Width = flowLayoutPanel1.ClientSize.Width;
            }
        }
    }
}
