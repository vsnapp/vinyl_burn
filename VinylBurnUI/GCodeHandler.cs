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
using System.IO.Ports;
using System.Text;
using System.Collections.Generic;

namespace VinylBurnUI
{
    /// <summary>
    /// Handles G-code communication with Marlin firmware on ATmega2560.
    /// Converts vinyl lathe commands to standard G-code for stepper motor control.
    /// </summary>
    public class GCodeHandler
    {
        public string PortName { get; set; }
        public SerialPort MarlinPort { get; set; }
        public string Hardware { get; set; }

        // Properties for position tracking
        public int CurrentSteps { get; set; }
        public int RecdStepsTaken { get; set; }

        // Movement parameters
        private decimal currentFeedRate = 100; // mm/min
        private decimal stepsPerMm;
        private bool isHomed = false;

        // Buffer for incoming data
        private StringBuilder receiveBuffer = new StringBuilder();

        public event EventHandler<GCodeDataRecdEventArgs> MarlinDataRecd;

        public GCodeHandler()
        {
            // Calculate steps per mm based on leadscrew pitch
            // StepsPerRev = 200, CmPerRev = 0.1 => steps per cm = 2000, steps per mm = 200
            stepsPerMm = GenMethods.Constants.StepsPerRev / (GenMethods.Constants.CmPerRev * 10);
        }

        /// <summary>
        /// Opens a specified port for Marlin communication.
        /// </summary>
        public void OpenSerial(SerialPort iSerialPort)
        {
            if (MarlinPort != null)
                CloseSerial();

            MarlinPort = iSerialPort;
            try
            {
                MarlinPort.DataReceived += MarlinPort_DataReceived;
                // Use the settings from the passed serial port (already configured with user-selected baud rate)
                MarlinPort.NewLine = "\n";
                MarlinPort.Open();

                // Note: Marlin needs time to initialize after connection.
                // A brief delay helps ensure the board is ready to receive commands.
                // For better UX, consider using a background thread for connection in future versions.

                // Send initial setup commands
                InitializeMarlin();
            }
            catch (Exception e)
            {
                throw new Exception("Could not open Marlin on port " + PortName + ". " + e.Message);
            }
        }

        /// <summary>
        /// Initialize Marlin with required settings for vinyl lathe operation.
        /// </summary>
        private void InitializeMarlin()
        {
            // Set to relative positioning mode for easier step control
            SendGCode("G91"); // Relative positioning

            // Set units to millimeters
            SendGCode("G21");

            // Set initial feedrate
            SendGCode("G1 F" + currentFeedRate.ToString());
        }

        /// <summary>
        /// Handles data received from Marlin.
        /// Parses responses for position updates and acknowledgments.
        /// </summary>
        private void MarlinPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string dataRecd = MarlinPort.ReadExisting();
                receiveBuffer.Append(dataRecd);

                // Process complete lines
                string bufferContent = receiveBuffer.ToString();
                int newlineIndex;
                while ((newlineIndex = bufferContent.IndexOf('\n')) >= 0)
                {
                    string line = bufferContent.Substring(0, newlineIndex).Trim();
                    bufferContent = bufferContent.Substring(newlineIndex + 1);

                    ProcessMarlinResponse(line);
                }
                receiveBuffer.Clear();
                receiveBuffer.Append(bufferContent);
            }
            catch (Exception)
            {
                // Silently handle parsing errors
            }
        }

        /// <summary>
        /// Process a single line response from Marlin.
        /// </summary>
        private void ProcessMarlinResponse(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            // Parse position reports (M114 response)
            // Format: X:0.00 Y:0.00 Z:0.00 E:0.00 Count X:0 Y:0 Z:0
            if (line.StartsWith("X:") || line.Contains("Count"))
            {
                ParsePositionReport(line);
            }
            // Check for limit switch triggers (endstop status)
            else if (line.Contains("endstop") || line.Contains("TRIGGERED") || line.Contains("open"))
            {
                ParseEndstopStatus(line);
            }
            // Handle "ok" acknowledgments
            else if (line.Trim().Equals("ok", StringComparison.OrdinalIgnoreCase))
            {
                // Command acknowledged
            }
        }

        /// <summary>
        /// Parse position report from M114 command.
        /// </summary>
        private void ParsePositionReport(string line)
        {
            try
            {
                // Parse X position (we use X axis for linear rail)
                int xIndex = line.IndexOf("X:");
                if (xIndex >= 0)
                {
                    int endIndex = line.IndexOf(' ', xIndex);
                    if (endIndex < 0) endIndex = line.Length;
                    string xValue = line.Substring(xIndex + 2, endIndex - xIndex - 2);

                    if (decimal.TryParse(xValue, out decimal xPos))
                    {
                        // Convert mm position to steps
                        RecdStepsTaken = (int)(xPos * stepsPerMm);

                        // Fire event with position data
                        MarlinDataRecd?.Invoke(this, new GCodeDataRecdEventArgs(
                            RecdStepsTaken, 0, 0));
                    }
                }
            }
            catch
            {
                // Silently handle parsing errors
            }
        }

        /// <summary>
        /// Parse endstop status from M119 command.
        /// </summary>
        private void ParseEndstopStatus(string line)
        {
            int limitLo = 0;
            int limitHi = 0;

            // Check for X min endstop (limit low)
            if (line.Contains("x_min") && line.Contains("TRIGGERED"))
            {
                limitLo = 1;
            }
            // Check for X max endstop (limit high)
            if (line.Contains("x_max") && line.Contains("TRIGGERED"))
            {
                limitHi = 1;
            }

            MarlinDataRecd?.Invoke(this, new GCodeDataRecdEventArgs(
                RecdStepsTaken, limitLo, limitHi));
        }

        /// <summary>
        /// Event args for data received from Marlin.
        /// </summary>
        public class GCodeDataRecdEventArgs : EventArgs
        {
            public int RecdStepsTaken { get; private set; }
            public int RecdLimitLo { get; set; }
            public int RecdLimitHi { get; set; }

            public GCodeDataRecdEventArgs(int iRecdStepsTaken, int iRecdLimitLo, int iRecdLimitHi)
            {
                RecdStepsTaken = iRecdStepsTaken;
                RecdLimitLo = iRecdLimitLo;
                RecdLimitHi = iRecdLimitHi;
            }
        }

        /// <summary>
        /// Closes the Marlin port.
        /// </summary>
        public void CloseSerial()
        {
            try
            {
                if (MarlinPort != null && MarlinPort.IsOpen)
                {
                    // Stop any movement before closing
                    SendGCode("M410"); // Quickstop
                    MarlinPort.Close();
                }
            }
            catch (Exception)
            {
                // Silently handle close errors
            }
        }

        /// <summary>
        /// Send a G-code command to Marlin.
        /// </summary>
        public void SendGCode(string gcode)
        {
            try
            {
                if (MarlinPort == null || !MarlinPort.IsOpen) return;
                MarlinPort.WriteLine(gcode);
            }
            catch (Exception ex)
            {
                throw new Exception("Could not send G-code to Marlin. " + ex.Message);
            }
        }

        /// <summary>
        /// Send command or data to Marlin, converting from Arduino protocol to G-code.
        /// </summary>
        public void SendDataToMarlin(ArduinoXmitType iXtype, int iData)
        {
            try
            {
                if (MarlinPort == null || !MarlinPort.IsOpen) return;

                switch (iXtype)
                {
                    case ArduinoXmitType.Command:
                        SendCommand((ArduinoCommand)iData);
                        break;
                    case ArduinoXmitType.TTData:
                        // Turntable speed data - used to calculate feed rate
                        SetTurntableSpeed(iData);
                        break;
                    case ArduinoXmitType.LPcmData:
                        // Lines per cm - convert to feed rate
                        SetFeedRateFromLPcm(iData);
                        break;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Could not submit command to Marlin. " + ex.Message);
            }
        }

        /// <summary>
        /// Convert Arduino command to G-code command.
        /// </summary>
        private void SendCommand(ArduinoCommand cmd)
        {
            switch (cmd)
            {
                case ArduinoCommand.Stop:
                    // Emergency stop / halt movement
                    SendGCode("M410"); // Quickstop
                    SendGCode("M18");  // Disable steppers (optional, allows manual movement)
                    break;

                case ArduinoCommand.Forward:
                    // Move towards center (positive X direction)
                    SendGCode("M17");  // Enable steppers
                    SendGCode("G91");  // Relative mode
                    SendGCode("G1 X1000 F" + currentFeedRate.ToString()); // Move forward with current feedrate
                    break;

                case ArduinoCommand.Back:
                    // Move away from center (negative X direction)
                    SendGCode("M17");  // Enable steppers
                    SendGCode("G91");  // Relative mode
                    SendGCode("G1 X-1000 F" + currentFeedRate.ToString()); // Move backward
                    break;

                case ArduinoCommand.ForwardWind:
                    // Fast wind forward
                    SendGCode("M17");  // Enable steppers
                    SendGCode("G91");  // Relative mode
                    SendGCode("G0 X1000"); // Rapid move forward
                    break;

                case ArduinoCommand.BackWind:
                    // Fast wind backward
                    SendGCode("M17");  // Enable steppers
                    SendGCode("G91");  // Relative mode
                    SendGCode("G0 X-1000"); // Rapid move backward
                    break;

                case ArduinoCommand.GoHome:
                    // Home the axis
                    SendGCode("G28 X"); // Home X axis
                    isHomed = true;
                    RecdStepsTaken = 0;
                    break;

                case ArduinoCommand.Zeroise:
                    // Set current position as zero
                    SendGCode("G92 X0"); // Set current position to X=0
                    RecdStepsTaken = 0;
                    break;
            }

            // Request position update after command
            RequestPositionUpdate();
        }

        /// <summary>
        /// Request current position from Marlin.
        /// </summary>
        public void RequestPositionUpdate()
        {
            SendGCode("M114"); // Report current position
        }

        /// <summary>
        /// Request endstop status from Marlin.
        /// </summary>
        public void RequestEndstopStatus()
        {
            SendGCode("M119"); // Endstop status
        }

        /// <summary>
        /// Set turntable speed (in hundredths of RPM).
        /// This is used to calculate appropriate feed rates.
        /// </summary>
        private void SetTurntableSpeed(int speedx100)
        {
            // Store for feed rate calculations
            // Speed is in hundredths of RPM (e.g., 3333 = 33.33 RPM)
            decimal speedRPM = speedx100 / 100.0m;
            // This could be used to adjust timing, but for now we just store it
        }

        /// <summary>
        /// Convert lines per cm to Marlin feed rate.
        /// LPcm determines how fast the cutter moves relative to the groove spacing.
        /// </summary>
        private void SetFeedRateFromLPcm(int lpcm)
        {
            if (lpcm <= 0) return;

            // Lines per cm = grooves per cm
            // Higher LPcm = finer grooves = slower movement
            // Convert to mm/min feed rate
            // 
            // With StepsPerRev=200 and CmPerRev=0.1cm (1mm):
            // - 1 revolution = 1mm linear movement
            // - If we want X lines per cm, that's X/10 lines per mm
            // - Each line corresponds to one revolution of the record
            // 
            // For a 33.33 RPM record:
            // - 33.33 revolutions per minute
            // - If lpcm = 100 (100 lines/cm = 10 lines/mm)
            // - Linear speed = 33.33 / 10 = 3.33 mm/min
            //
            // General formula: feedRate = (RPM / (lpcm / 10)) = (RPM * 10) / lpcm

            // Use a default RPM if not set (45 RPM is common)
            decimal assumedRPM = 45;
            currentFeedRate = (assumedRPM * 10) / lpcm;

            // Ensure minimum feed rate
            if (currentFeedRate < 0.1m) currentFeedRate = 0.1m;

            // Apply the new feed rate
            SendGCode("G1 F" + currentFeedRate.ToString("F2"));
        }

        /// <summary>
        /// Move a specific number of steps (converted to mm for Marlin).
        /// </summary>
        public void MoveSteps(int steps)
        {
            // Convert steps to mm
            decimal mm = steps / stepsPerMm;
            SendGCode("G91"); // Relative mode
            SendGCode("G1 X" + mm.ToString("F4") + " F" + currentFeedRate.ToString("F2"));
        }

        /// <summary>
        /// Move to a specific position in steps (converted to mm for Marlin).
        /// </summary>
        public void MoveToPosition(int steps)
        {
            decimal mm = steps / stepsPerMm;
            SendGCode("G90"); // Absolute mode
            SendGCode("G1 X" + mm.ToString("F4") + " F" + currentFeedRate.ToString("F2"));
        }

        /// <summary>
        /// Generate G-code file for a complete cutting session.
        /// </summary>
        public List<string> GenerateGCodeForSession(decimal startRadius, decimal endRadius, List<int> segmentLPcm)
        {
            List<string> gcode = new List<string>();

            // Header
            gcode.Add("; Vinyl Burn G-code for Marlin");
            gcode.Add("; Generated: " + DateTime.Now.ToString());
            gcode.Add("");
            gcode.Add("G21 ; Set units to millimeters");
            gcode.Add("G90 ; Absolute positioning");
            gcode.Add("G28 X ; Home X axis");
            gcode.Add("G92 X0 ; Set current position as zero");
            gcode.Add("");

            // Calculate movements
            decimal currentPos = 0;
            decimal totalDistance = (startRadius - endRadius) * 10; // Convert cm to mm

            foreach (int lpcm in segmentLPcm)
            {
                decimal feedRate = 45 * 10 / lpcm; // Assuming 45 RPM
                gcode.Add("G1 F" + feedRate.ToString("F2") + " ; Set feed rate for " + lpcm + " LPcm");
            }

            // Footer
            gcode.Add("");
            gcode.Add("M400 ; Wait for moves to finish");
            gcode.Add("M18 ; Disable steppers");
            gcode.Add("; End of program");

            return gcode;
        }
    }
}
