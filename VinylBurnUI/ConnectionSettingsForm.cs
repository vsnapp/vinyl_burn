// This file is part of Vinyl Burn.
//
// Vinyl Burn is free software: you can redistribute it and/or modify it under the
// terms of the GNU General Public License as published by the Free Software Foundation,
// either version 3 of the License, or (at your option) any later version.
//
// Vinyl Burn is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY,
// without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License along with Vinyl Burn.
// If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Drawing;
using System.IO.Ports;
using System.Windows.Forms;

namespace VinylBurnUI
{
    /// <summary>
    /// Form for manually selecting COM port and baud rate for G-code communication.
    /// </summary>
    public class ConnectionSettingsForm : Form
    {
        private ComboBox cmbComPort;
        private ComboBox cmbBaudRate;
        private Button btnConnect;
        private Button btnCancel;
        private Button btnRefresh;
        private Label lblComPort;
        private Label lblBaudRate;
        private Label lblStatus;
        private CheckBox chkAutoDetect;

        /// <summary>
        /// Selected COM port name (e.g., "COM3").
        /// </summary>
        public string SelectedComPort { get; private set; }

        /// <summary>
        /// Selected baud rate.
        /// </summary>
        public int SelectedBaudRate { get; private set; }

        /// <summary>
        /// Whether to use auto-detection instead of manual settings.
        /// </summary>
        public bool UseAutoDetect { get; private set; }

        /// <summary>
        /// Standard baud rates supported by Marlin.
        /// </summary>
        private static readonly int[] StandardBaudRates = new int[]
        {
            9600,
            19200,
            38400,
            57600,
            115200,
            250000
        };

        public ConnectionSettingsForm()
        {
            InitializeComponent();
            PopulateComPorts();
            PopulateBaudRates();
        }

        private void InitializeComponent()
        {
            this.Text = "Connection Settings";
            this.Size = new Size(350, 280);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = GenMethods.Constants.FormBackColour;
            this.ForeColor = GenMethods.Constants.FormForeColour;

            // Auto-detect checkbox
            chkAutoDetect = new CheckBox();
            chkAutoDetect.Text = "Auto-detect Arduino/Marlin device";
            chkAutoDetect.Location = new Point(20, 20);
            chkAutoDetect.Size = new Size(300, 25);
            chkAutoDetect.Checked = true;
            chkAutoDetect.ForeColor = GenMethods.Constants.FormForeColour;
            chkAutoDetect.CheckedChanged += ChkAutoDetect_CheckedChanged;
            this.Controls.Add(chkAutoDetect);

            // COM Port label
            lblComPort = new Label();
            lblComPort.Text = "COM Port:";
            lblComPort.Location = new Point(20, 60);
            lblComPort.Size = new Size(80, 25);
            lblComPort.ForeColor = GenMethods.Constants.FormForeColour;
            this.Controls.Add(lblComPort);

            // COM Port combo box
            cmbComPort = new ComboBox();
            cmbComPort.Location = new Point(120, 57);
            cmbComPort.Size = new Size(120, 25);
            cmbComPort.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbComPort.Enabled = false;
            this.Controls.Add(cmbComPort);

            // Refresh button
            btnRefresh = new Button();
            btnRefresh.Text = "Refresh";
            btnRefresh.Location = new Point(250, 55);
            btnRefresh.Size = new Size(70, 27);
            btnRefresh.Enabled = false;
            btnRefresh.Click += BtnRefresh_Click;
            this.Controls.Add(btnRefresh);

            // Baud Rate label
            lblBaudRate = new Label();
            lblBaudRate.Text = "Baud Rate:";
            lblBaudRate.Location = new Point(20, 100);
            lblBaudRate.Size = new Size(80, 25);
            lblBaudRate.ForeColor = GenMethods.Constants.FormForeColour;
            this.Controls.Add(lblBaudRate);

            // Baud Rate combo box
            cmbBaudRate = new ComboBox();
            cmbBaudRate.Location = new Point(120, 97);
            cmbBaudRate.Size = new Size(120, 25);
            cmbBaudRate.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbBaudRate.Enabled = false;
            this.Controls.Add(cmbBaudRate);

            // Status label
            lblStatus = new Label();
            lblStatus.Text = "Select connection settings for Marlin/G-code communication.";
            lblStatus.Location = new Point(20, 140);
            lblStatus.Size = new Size(300, 40);
            lblStatus.ForeColor = GenMethods.Constants.FormForeColour;
            this.Controls.Add(lblStatus);

            // Connect button
            btnConnect = new Button();
            btnConnect.Text = "Connect";
            btnConnect.Location = new Point(140, 195);
            btnConnect.Size = new Size(90, 30);
            btnConnect.DialogResult = DialogResult.OK;
            btnConnect.Click += BtnConnect_Click;
            this.Controls.Add(btnConnect);

            // Cancel button
            btnCancel = new Button();
            btnCancel.Text = "Cancel";
            btnCancel.Location = new Point(240, 195);
            btnCancel.Size = new Size(80, 30);
            btnCancel.DialogResult = DialogResult.Cancel;
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnConnect;
            this.CancelButton = btnCancel;
        }

        private void ChkAutoDetect_CheckedChanged(object sender, EventArgs e)
        {
            bool manualMode = !chkAutoDetect.Checked;
            cmbComPort.Enabled = manualMode;
            cmbBaudRate.Enabled = manualMode;
            btnRefresh.Enabled = manualMode;

            if (manualMode)
            {
                lblStatus.Text = "Select COM port and baud rate manually.";
            }
            else
            {
                lblStatus.Text = "Will auto-detect Arduino/Marlin device.";
            }
        }

        private void PopulateComPorts()
        {
            cmbComPort.Items.Clear();
            string[] ports = SerialPort.GetPortNames();

            if (ports.Length == 0)
            {
                cmbComPort.Items.Add("No ports found");
                cmbComPort.SelectedIndex = 0;
                cmbComPort.Enabled = false;
                lblStatus.Text = "No COM ports detected. Check USB connection.";
                lblStatus.ForeColor = GenMethods.Constants.FormErrorColour;
            }
            else
            {
                foreach (string port in ports)
                {
                    cmbComPort.Items.Add(port);
                }
                cmbComPort.SelectedIndex = 0;
                lblStatus.ForeColor = GenMethods.Constants.FormForeColour;
            }
        }

        private void PopulateBaudRates()
        {
            cmbBaudRate.Items.Clear();
            foreach (int baudRate in StandardBaudRates)
            {
                cmbBaudRate.Items.Add(baudRate.ToString());
            }
            // Default to 115200 (common Marlin default)
            cmbBaudRate.SelectedIndex = Array.IndexOf(StandardBaudRates, 115200);
            if (cmbBaudRate.SelectedIndex < 0)
                cmbBaudRate.SelectedIndex = 0;
        }

        private void BtnRefresh_Click(object sender, EventArgs e)
        {
            PopulateComPorts();
        }

        private void BtnConnect_Click(object sender, EventArgs e)
        {
            UseAutoDetect = chkAutoDetect.Checked;

            if (!UseAutoDetect)
            {
                if (cmbComPort.SelectedItem == null || cmbComPort.SelectedItem.ToString() == "No ports found")
                {
                    MessageBox.Show("Please select a valid COM port.", "Connection Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    this.DialogResult = DialogResult.None;
                    return;
                }

                SelectedComPort = cmbComPort.SelectedItem.ToString();

                if (cmbBaudRate.SelectedItem != null &&
                    int.TryParse(cmbBaudRate.SelectedItem.ToString(), out int baudRate))
                {
                    SelectedBaudRate = baudRate;
                }
                else
                {
                    SelectedBaudRate = 115200; // Default
                }
            }
        }

        /// <summary>
        /// Static method to show the connection settings dialog and get user selection.
        /// </summary>
        /// <param name="owner">Parent form.</param>
        /// <param name="comPort">Output: selected COM port.</param>
        /// <param name="baudRate">Output: selected baud rate.</param>
        /// <param name="useAutoDetect">Output: whether to use auto-detection.</param>
        /// <returns>True if user clicked Connect, false if cancelled.</returns>
        public static bool ShowConnectionDialog(IWin32Window owner,
            out string comPort, out int baudRate, out bool useAutoDetect)
        {
            using (ConnectionSettingsForm form = new ConnectionSettingsForm())
            {
                DialogResult result = form.ShowDialog(owner);
                comPort = form.SelectedComPort;
                baudRate = form.SelectedBaudRate;
                useAutoDetect = form.UseAutoDetect;
                return result == DialogResult.OK;
            }
        }
    }
}
