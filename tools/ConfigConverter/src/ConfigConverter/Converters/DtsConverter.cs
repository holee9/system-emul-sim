using ConfigConverter.Models;

namespace ConfigConverter.Converters;

/// <summary>
/// Converts DetectorConfig to Device Tree Overlay (.dts format).
/// Implements REQ-TOOLS-021: Convert detector_config.yaml to SoC device tree overlay.
/// </summary>
public class DtsConverter
{
    /// <summary>
    /// Converts DetectorConfig to DTS overlay file content.
    /// </summary>
    /// <param name="config">Detector configuration</param>
    /// <returns>DTS file content as string</returns>
    /// <exception cref="ArgumentNullException">Thrown when config is null</exception>
    public string Convert(DetectorConfig config)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        var lines = new List<string>();

        // Header
        lines.Add("/dts-v1/;");
        lines.Add("/plugin/;");
        lines.Add(string.Empty);
        lines.Add("/*");
        lines.Add(" * Device Tree Overlay for X-ray Detector Panel System");
        lines.Add(" * Source: detector_config.yaml");
        lines.Add($" * Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        lines.Add(" *");
        lines.Add(" * WARNING: This file is auto-generated. Do not edit manually.");
        lines.Add(" */");
        lines.Add(string.Empty);

        // CSI-2 configuration
        AddCsi2Configuration(lines, config);

        // Ethernet configuration
        AddEthernetConfiguration(lines, config);

        return string.Join(Environment.NewLine, lines);
    }

    private void AddCsi2Configuration(List<string> lines, DetectorConfig config)
    {
        var csi2 = config.Fpga.DataInterface.Csi2;
        var csi2Rx = config.Controller.Csi2Rx;

        lines.Add("/*");
        lines.Add(" * CSI-2 MIPI D-PHY Receiver Configuration");
        lines.Add($" * Lane Count: {csi2.LaneCount}");
        lines.Add($" * Lane Speed: {csi2.LaneSpeedMbps} Mbps/lane");
        lines.Add($" * Data Type: {csi2.DataType}");
        lines.Add(" */");
        lines.Add(string.Empty);

        // Get CSI-2 interface index
        var csiInterface = csi2Rx?.InterfaceIndex ?? 0;

        // Data lanes
        var dataLanes = string.Join(" ", Enumerable.Range(1, csi2.LaneCount));

        // Data type code
        var dataTypeCode = csi2.DataType.ToUpper() switch
        {
            "RAW16" => "0x2c",
            "RAW14" => "0x2d",
            _ => "0x2c"
        };

        // Link frequency (in Hz)
        var linkFrequency = csi2.LaneSpeedMbps * 1_000_000;

        lines.Add("&{{");
        lines.Add($"    compatible = \"fsl,imx8mp-mipi-csi2\";");
        lines.Add($"    status = \"okay\";");
        lines.Add(string.Empty);
        lines.Add("    /*");
        lines.Add("     * CSI-2 lane mapping");
        lines.Add("     * Clk lane: lane 0");
        lines.Add($"     * Data lanes: {dataLanes}");
        lines.Add("     */");
        lines.Add($"    data-lanes = <{dataLanes}>;");
        lines.Add($"    lane-polarities = <0 0 0 0 0>;");
        lines.Add(string.Empty);
        lines.Add("    /*");
        lines.Add("     * Link frequency (per-lane, in Hz)");
        lines.Add($"     * {csi2.LaneSpeedMbps} Mbps/lane");
        lines.Add("     */");
        lines.Add("    link-frequencies = /bits/ 64");
        lines.Add($"        <{linkFrequency}>;");
        lines.Add(string.Empty);
        lines.Add("    /*");
        lines.Add("     * CSI-2 data type");
        lines.Add($"     * {csi2.DataType} ({dataTypeCode})");
        lines.Add("     */");
        lines.Add("    bus-type = <4>; /* MIPI CSI-2 */");
        lines.Add(string.Empty);
        lines.Add("    /*");
        lines.Add("     * Remote endpoint (FPGA as CSI-2 TX)");
        lines.Add("     */");
        lines.Add("    endpoint@0 {");
        lines.Add("        remote-endpoint = <&fpga_csi2_out>;");
        lines.Add($"        data-type = <{dataTypeCode}>; /* {csi2.DataType} */");
        lines.Add($"        virtual-channel = <{csi2.VirtualChannel}>;");
        lines.Add("    };");
        lines.Add("}};");
        lines.Add(string.Empty);

        // CSI-2 Receiver configuration
        lines.Add($"&mipi_csi_{csiInterface} {{");
        lines.Add("    status = \"okay\";");
        lines.Add(string.Empty);

        if (csi2Rx != null)
        {
            // DMA burst length
            lines.Add("    /*");
            lines.Add("     * DMA burst length configuration");
            lines.Add($"     * Burst size: {csi2Rx.DmaBurstLength} bytes");
            lines.Add("     */");
            lines.Add("    fsl,mipi-vcx-sys-burst-size = /bits/ 8");
            lines.Add($"        <{csi2Rx.DmaBurstLength}>;");
            lines.Add(string.Empty);
        }

        lines.Add("    /*");
        lines.Add("     * Virtual channel configuration");
        lines.Add($"     * Virtual Channel {csi2.VirtualChannel}");
        lines.Add("     */");
        lines.Add("    fsl,mipi-vcx = <0>; /* Single virtual channel */");
        lines.Add("}};");
        lines.Add(string.Empty);
    }

    private void AddEthernetConfiguration(List<string> lines, DetectorConfig config)
    {
        var ethernet = config.Controller.Ethernet;

        lines.Add("/*");
        lines.Add(" * Ethernet Streaming Configuration (SoC to Host)");
        lines.Add($" * Speed: {ethernet.Speed}");
        lines.Add($" * Protocol: {ethernet.Protocol.ToUpper()}");
        lines.Add($" * Port: {ethernet.Port}");
        lines.Add($" * MTU: {ethernet.Mtu}");
        lines.Add(" */");
        lines.Add(string.Empty);

        // Determine interface name based on speed
        var interfaceName = ethernet.Speed.ToLower() switch
        {
            "10gbe" => "eqos", // i.MX8MP 10GbE MAC
            "1gbe" => "fec",   // i.MX8MP 1GbE MAC
            _ => "ethernet"
        };

        var speedValue = ethernet.Speed.ToLower() == "10gbe" ? 10000 : 1000;

        lines.Add($"&{interfaceName} {{");
        lines.Add("    status = \"okay\";");
        lines.Add($"    phy-mode = \"rgmii-id\";");
        lines.Add(string.Empty);

        // Fixed link configuration
        lines.Add("    /*");
        lines.Add("     * Fixed-link configuration for direct FPGA connection");
        lines.Add($"     * Speed: {ethernet.Speed}");
        lines.Add("     */");
        lines.Add("    fixed-link {");
        lines.Add($"        speed = <{speedValue}>;");
        lines.Add("        full-duplex;");
        lines.Add("    };");
        lines.Add(string.Empty);

        // MAC address configuration
        lines.Add("    /*");
        lines.Add("     * MAC address configuration");
        lines.Add("     */");
        lines.Add("    mac-address = [00 04 25 1c a0 b0];");
        lines.Add("    local-mac-address = [00 04 25 1c a0 b0];");
        lines.Add(string.Empty);

        lines.Add("    /*");
        lines.Add("     * Network configuration notes:");
        lines.Add($"     * Protocol: {ethernet.Protocol}");
        lines.Add($"     * Port: {ethernet.Port}");
        lines.Add($"     * MTU: {ethernet.Mtu}");
        lines.Add("     */");
        lines.Add("}};");
        lines.Add(string.Empty);

        // PHY configuration for RGMII
        lines.Add("&mdio {");
        lines.Add("    status = \"okay\";");
        lines.Add(string.Empty);
        lines.Add("    /*");
        lines.Add("     * Fixed-link PHY (no actual PHY for direct FPGA-SoC)");
        lines.Add("     */");
        lines.Add("    ethernet-phy@0 {");
        lines.Add("        compatible = \"ethernet-phy-ieee802.3-c22\";");
        lines.Add("        reg = <0>;");
        lines.Add($"        max-speed = <{speedValue}>;");
        lines.Add("    };");
        lines.Add("}};");
    }
}
