namespace XrayDetector.Gui.Help;

/// <summary>
/// Data model for parameter tooltip information (SPEC-HELP-001 Wave 2).
/// </summary>
/// <param name="Name">Parameter display name.</param>
/// <param name="RangeDescription">Human-readable range (e.g., "Range: 40–150 kV").</param>
/// <param name="PhysicalMeaning">Korean description of what the parameter means physically.</param>
public record ParameterTooltipInfo(string Name, string RangeDescription, string PhysicalMeaning);

/// <summary>
/// Static registry of parameter tooltips for all simulator parameters (SPEC-HELP-001 Wave 2).
/// </summary>
public static class ParameterTooltips
{
    /// <summary>
    /// Dictionary of parameter tooltips keyed by parameter name.
    /// </summary>
    public static readonly Dictionary<string, ParameterTooltipInfo> Items = new()
    {
        ["kVp"] = new ParameterTooltipInfo(
            "kVp",
            "Range: 40–150 kV",
            "X선관 가속 전압 - 높을수록 투과력 증가"),

        ["mAs"] = new ParameterTooltipInfo(
            "mAs",
            "Range: 0.1–500 mAs",
            "X선 방사선량 - 높을수록 SNR 향상"),

        ["DefectRate"] = new ParameterTooltipInfo(
            "DefectRate",
            "Range: 0.0–1.0",
            "결함 픽셀 비율 시뮬레이션"),

        ["PacketLossRate"] = new ParameterTooltipInfo(
            "PacketLossRate",
            "Range: 0.0–1.0",
            "UDP 패킷 손실률"),

        ["ReorderRate"] = new ParameterTooltipInfo(
            "ReorderRate",
            "Range: 0.0–1.0",
            "패킷 재정렬 비율"),

        ["CorruptionRate"] = new ParameterTooltipInfo(
            "CorruptionRate",
            "Range: 0.0–1.0",
            "패킷 손상 비율"),
    };
}
