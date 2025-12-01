# vinyl_burn
Vinyl record cutting software. Requires Windows 10.

This program 'Vinyl Burn' works in conjunction with the Arduino software to enable control of the movement of the cutter head of a record lathe. The software allows a complete record side to be planned, with settings for the sound files to be transferred to disc, groove pitches, inter-track gaps and lock groove stop points. When the 'Cut record' icon is clicked, the software communicates via USB with the Arduino to move the linear rail stepper motor.

## G-code / Marlin Support

This version includes support for G-code communication with Marlin firmware running on an ATmega2560 (Arduino Mega). This allows you to use standard 3D printer control boards or CNC controllers instead of custom Arduino firmware.

### Features

- **Manual COM Port Selection**: Connect to any COM port with a custom baud rate
- **G-code Protocol**: Translates lathe commands to standard G-code (G0, G1, M commands)
- **Marlin Compatibility**: Works with Marlin firmware on ATmega2560 boards
- **Position Tracking**: Parses M114 responses for position feedback
- **Endstop Support**: Reads M119 endstop status for limit switch detection

### Connection Options

When the application starts, you can choose between:

1. **Auto-detect**: Automatically finds connected Arduino devices (original behavior)
2. **Manual Connection**: Select a specific COM port and baud rate for G-code/Marlin devices

You can also change the connection at any time using the **Connection** menu:
- **Connection > Connect...**: Opens the connection settings dialog
- **Connection > Disconnect**: Disconnects from the current device

### Supported Baud Rates

- 9600
- 19200
- 38400
- 57600
- 115200 (default for Marlin)
- 250000

### G-code Commands Used

| Lathe Command | G-code Equivalent |
|---------------|-------------------|
| Stop | M410 (Quickstop), M18 (Disable steppers) |
| Forward | G91, G1 X{distance} F{feedrate} |
| Backward | G91, G1 X-{distance} F{feedrate} |
| Fast Wind Forward | G0 X{distance} |
| Fast Wind Backward | G0 X-{distance} |
| Home | G28 X |
| Zero Position | G92 X0 |

### Marlin Configuration Tips

For use with this software, configure your Marlin firmware:

1. Set `DEFAULT_AXIS_STEPS_PER_UNIT` for the X axis based on your leadscrew pitch
2. Enable `MIN_SOFTWARE_ENDSTOPS` and `MAX_SOFTWARE_ENDSTOPS` for safety
3. Configure endstops appropriately for your hardware

## Original Documentation

The document in the repository 'lathe_guide' should be consulted. That repository contains the Arduino sketch and FreeCAD source from which 3D-printable files (.STL) can be extracted.

Note: For development, the project 'VinylBurnUI' should be set as the startup project within the solution.
