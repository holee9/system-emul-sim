namespace McuSimulator.Core.Command;

/// <summary>
/// Command types (from fw/include/protocol/command_protocol.h).
/// </summary>
public enum CommandType : ushort
{
    StartScan = 0x01,
    StopScan = 0x02,
    GetStatus = 0x10,
    SetConfig = 0x20,
    Reset = 0x30
}
