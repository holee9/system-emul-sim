# License

## Proprietary License

Copyright (c) 2026 ABYZ-Lab. All rights reserved.

This software and associated documentation files (the "Software") are proprietary
and confidential. Unauthorized copying, distribution, modification, or use of this
Software, via any medium, is strictly prohibited without prior written permission
from the copyright holder.

---

## Terms

### Permitted Uses

- Internal development and testing by authorized team members
- Deployment on designated X-ray Detector Panel System hardware
- Generation of derivative artifacts (bitstreams, firmware binaries, SDK packages) for authorized deployments

### Prohibited Uses

- Redistribution of source code or binary artifacts to third parties
- Reverse engineering of generated bitstreams or firmware binaries
- Use in competing products or systems
- Sublicensing without written authorization

---

## Third-Party Components

This project uses the following open-source components. Their respective licenses apply to those components only:

### .NET Runtime and SDK

- **Component**: .NET 8.0 Runtime and SDK
- **License**: MIT License
- **Source**: https://github.com/dotnet/runtime

### NuGet Packages

| Package | License | Usage |
|---------|---------|-------|
| xUnit | Apache 2.0 | Unit test framework |
| FluentAssertions | Apache 2.0 | Test assertion library |
| coverlet | MIT | Code coverage collector |
| NSubstitute | BSD 3-Clause | Mocking framework |
| LibTiff.NET | BSD 3-Clause | TIFF file read/write |
| System.IO.Pipelines | MIT | High-performance I/O |
| YamlDotNet | MIT | YAML configuration parsing |
| fo-dicom | Microsoft Public License | DICOM format (optional) |

### FPGA IP

| IP Core | License | Usage |
|---------|---------|-------|
| AMD MIPI CSI-2 TX Subsystem | AMD Vivado HL Design Edition License | CSI-2 data transmission |
| AMD Clocking Wizard | AMD Vivado License (included) | Clock generation (MMCM) |

### Build Tools

| Tool | License | Usage |
|------|---------|-------|
| AMD Vivado | Commercial (AMD) | FPGA synthesis and simulation |
| GCC (cross-compiler) | GPL v3 (runtime exception) | SoC firmware compilation |
| CMake | BSD 3-Clause | Build system |
| CMocka | Apache 2.0 | Firmware unit testing |

---

## Disclaimer

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

---

## Contact

For licensing inquiries, contact the project maintainer.

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | ABYZ-Lab Agent (architect) | Initial license document |

---
