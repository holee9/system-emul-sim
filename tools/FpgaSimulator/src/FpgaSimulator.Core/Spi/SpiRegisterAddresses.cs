namespace FpgaSimulator.Core.Spi;

/// <summary>
/// SPI register address definitions from fpga-design.md Section 6.3.
/// </summary>
public static class SpiRegisterAddresses
{
    // Identification and Debug Registers (0x00 - 0x1F)
    public const byte DEVICE_ID = 0x00;
    public const byte ILA_CAPTURE_0 = 0x10;
    public const byte ILA_CAPTURE_1 = 0x11;
    public const byte ILA_CAPTURE_2 = 0x12;
    public const byte ILA_CAPTURE_3 = 0x13;

    // Status and Control Registers (0x20 - 0x2F)
    public const byte STATUS = 0x20;
    public const byte CONTROL = 0x21;

    // Frame Counter Registers (0x30 - 0x3F)
    public const byte FRAME_COUNT_HI = 0x30;
    public const byte FRAME_COUNT_LO = 0x31;

    // Timing Configuration Registers (0x40 - 0x5F)
    public const byte TIMING_ROW_PERIOD = 0x40;
    public const byte TIMING_GATE_ON = 0x41;
    public const byte TIMING_GATE_OFF = 0x42;
    public const byte ROIC_SETTLE_US = 0x43;
    public const byte ADC_CONV_US = 0x44;
    public const byte FRAME_BLANK_US = 0x45;

    // Panel Configuration Registers (0x50 - 0x5F)
    public const byte PANEL_ROWS = 0x50;
    public const byte PANEL_COLS = 0x51;
    public const byte BIT_DEPTH = 0x52;
    public const byte PIXEL_FORMAT = 0x53;

    // CSI-2 Configuration Registers (0x60 - 0x7F)
    public const byte CSI2_LANE_COUNT = 0x60;
    public const byte CSI2_LANE_SPEED = 0x61;

    // Error Flag Registers (0x80 - 0x8F)
    public const byte ERROR_FLAGS = 0x80;

    // Data Interface Status Registers (0x90 - 0x9F)
    public const byte DATA_IF_STATUS = 0x90;
    public const byte TX_FRAME_COUNT = 0x94;
    public const byte TX_ERROR_COUNT = 0x98;

    // Version Registers (0xF0 - 0xFF)
    public const byte VERSION = 0xF4;
    public const byte BUILD_DATE = 0xF8;
}
