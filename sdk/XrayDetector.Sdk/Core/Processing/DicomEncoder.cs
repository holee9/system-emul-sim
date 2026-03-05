using System.Text;
using XrayDetector.Common.Dto;
using FellowOakDicom;
using FellowOakDicom.IO.Buffer;

namespace XrayDetector.Core.Processing;

/// <summary>
/// DICOM encoder for medical image export (REQ-SDK-041, optional).
/// Uses fo-dicom library to create DICOM files with appropriate tags.
/// </summary>
public sealed class DicomEncoder
{
    private readonly string _manufacturer;
    private readonly string _institutionName;
    private readonly string _softwareVersion;

    /// <summary>
    /// Creates a new DICOM encoder with default metadata.
    /// </summary>
    public DicomEncoder()
        : this(
            manufacturer: "ABYZ-Lab X-Ray Systems",
            institutionName: "ABYZ-Lab",
            softwareVersion: "1.0.0")
    {
    }

    /// <summary>
    /// Creates a new DICOM encoder with custom metadata.
    /// </summary>
    /// <param name="manufacturer">Device manufacturer name.</param>
    /// <param name="institutionName">Institution name.</param>
    /// <param name="softwareVersion">Software version.</param>
    public DicomEncoder(string manufacturer, string institutionName, string softwareVersion)
    {
        _manufacturer = manufacturer ?? throw new ArgumentNullException(nameof(manufacturer));
        _institutionName = institutionName ?? throw new ArgumentNullException(nameof(institutionName));
        _softwareVersion = softwareVersion ?? throw new ArgumentNullException(nameof(softwareVersion));
    }

    /// <summary>
    /// Encodes 16-bit grayscale pixel data as DICOM file (REQ-SDK-041).
    /// </summary>
    /// <param name="pixelData">16-bit grayscale pixel data.</param>
    /// <param name="metadata">Frame metadata.</param>
    /// <param name="outputPath">Output DICOM file path.</param>
    /// <param name="patientInfo">Optional patient information.</param>
    /// <param name="studyInfo">Optional study information.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task EncodeDicomAsync(
        ushort[] pixelData,
        FrameMetadata metadata,
        string outputPath,
        DicomPatientInfo? patientInfo = null,
        DicomStudyInfo? studyInfo = null,
        CancellationToken cancellationToken = default)
    {
        if (pixelData == null)
            throw new ArgumentNullException(nameof(pixelData));
        if (metadata == null)
            throw new ArgumentNullException(nameof(metadata));
        if (string.IsNullOrEmpty(outputPath))
            throw new ArgumentException("Output path cannot be null or empty", nameof(outputPath));

        await Task.Run(() =>
        {
            patientInfo ??= new DicomPatientInfo();
            studyInfo ??= new DicomStudyInfo();

            // Create DICOM dataset
            var dataset = new DicomDataset();

            // SOP Common Module
            dataset.Add(DicomTag.SOPClassUID, DicomUID.XRayAngiographicImageStorage);
            dataset.Add(DicomTag.SOPInstanceUID, GenerateUid());

            // Patient Module
            dataset.Add(DicomTag.PatientName, patientInfo.PatientName);
            dataset.Add(DicomTag.PatientID, patientInfo.PatientId);
            dataset.Add(DicomTag.PatientBirthDate, patientInfo.BirthDate);
            dataset.Add(DicomTag.PatientSex, patientInfo.Sex);

            // Study Module
            dataset.Add(DicomTag.StudyDate, studyInfo.StudyDate);
            dataset.Add(DicomTag.StudyTime, studyInfo.StudyTime);
            dataset.Add(DicomTag.StudyDescription, studyInfo.StudyDescription);
            dataset.Add(DicomTag.StudyInstanceUID, studyInfo.StudyInstanceUid);
            dataset.Add(DicomTag.AccessionNumber, studyInfo.AccessionNumber);

            // Series Module
            dataset.Add(DicomTag.SeriesNumber, int.Parse(studyInfo.SeriesNumber));
            dataset.Add(DicomTag.SeriesInstanceUID, studyInfo.SeriesInstanceUid);
            dataset.Add(DicomTag.Modality, "CR"); // Computed Radiography

            // Equipment Module
            dataset.Add(DicomTag.Manufacturer, _manufacturer);
            dataset.Add(DicomTag.ManufacturerModelName, "XDP-001");
            dataset.Add(DicomTag.SoftwareVersions, _softwareVersion);
            dataset.Add(DicomTag.InstitutionName, _institutionName);

            // Image Pixel Module
            dataset.Add(DicomTag.SamplesPerPixel, (ushort)1);
            dataset.Add(DicomTag.PhotometricInterpretation, "MONOCHROME2");
            dataset.Add(DicomTag.Rows, (ushort)metadata.Height);
            dataset.Add(DicomTag.Columns, (ushort)metadata.Width);
            dataset.Add(DicomTag.BitsAllocated, (ushort)16);
            dataset.Add(DicomTag.BitsStored, (ushort)metadata.BitDepth);
            dataset.Add(DicomTag.HighBit, (ushort)(metadata.BitDepth - 1));
            dataset.Add(DicomTag.PixelRepresentation, (ushort)0); // Unsigned integer

            // Frame of Reference Module
            dataset.Add(DicomTag.FrameOfReferenceUID, GenerateUid());

            // Image Module
            dataset.Add(DicomTag.InstanceNumber, (int)metadata.FrameNumber);
            dataset.Add(DicomTag.ImageType, "ORIGINAL\\PRIMARY");
            dataset.Add(DicomTag.AcquisitionDateTime, FormatDicomDateTime(metadata.Timestamp));

            // Pixel Data - Use native 16-bit encoding
            // For large data, use Add(DicomTag, IByteBuffer) overload
            byte[] pixelBytes = new byte[pixelData.Length * 2];
            for (int i = 0; i < pixelData.Length; i++)
            {
                pixelBytes[i * 2] = (byte)((pixelData[i] >> 8) & 0xFF);
                pixelBytes[i * 2 + 1] = (byte)(pixelData[i] & 0xFF);
            }

            var buffer = new MemoryByteBuffer(pixelBytes);
            dataset.Add(DicomTag.PixelData, (IByteBuffer)buffer);

            // Save DICOM file
            new DicomFile(dataset).Save(outputPath);
        }, cancellationToken);
    }

    /// <summary>
    /// Generates a unique DICOM UID.
    /// Uses 2.25 prefix (UUID-based OID) as allowed by DICOM standard.
    /// UID must contain only digits 0-9 and periods.
    /// </summary>
    private static string GenerateUid()
    {
        // Use timestamp + random number to ensure uniqueness
        // Format: 2.25.<timestamp>.<random>
        string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        string random = Random.Shared.Next(10000000, 99999999).ToString();
        return $"2.25.{timestamp}.{random}";
    }

    /// <summary>
    /// Formats DateTime for DICOM (YYYYMMDDHHMMSS).
    /// </summary>
    private static string FormatDicomDateTime(DateTime dateTime)
    {
        return dateTime.ToString("yyyyMMddHHmmss");
    }
}

/// <summary>
/// Patient information for DICOM encoding.
/// </summary>
public sealed class DicomPatientInfo
{
    public string PatientName { get; set; } = "Anonymous^Anonymous";
    public string PatientId { get; set; } = "UNKNOWN";
    public string BirthDate { get; set; } = "";
    public string Sex { get; set; } = "O"; // Other/Unknown
}

/// <summary>
/// Study information for DICOM encoding.
/// </summary>
public sealed class DicomStudyInfo
{
    public string StudyDate { get; set; } = DateTime.Now.ToString("yyyyMMdd");
    public string StudyTime { get; set; } = DateTime.Now.ToString("HHmmss");
    public string StudyDescription { get; set; } = "X-Ray Acquisition";
    public string StudyInstanceUid { get; set; } = "";
    public string AccessionNumber { get; set; } = "";
    public string SeriesNumber { get; set; } = "1";
    public string SeriesInstanceUid { get; set; } = "";

    /// <summary>
    /// Creates study info with auto-generated UIDs.
    /// </summary>
    public DicomStudyInfo()
    {
        StudyInstanceUid = GenerateUid();
        SeriesInstanceUid = GenerateUid();
    }

    /// <summary>
    /// Creates study info with custom UIDs.
    /// </summary>
    public DicomStudyInfo(string studyInstanceUid, string seriesInstanceUid)
    {
        StudyInstanceUid = studyInstanceUid ?? GenerateUid();
        SeriesInstanceUid = seriesInstanceUid ?? GenerateUid();
    }

    private static string GenerateUid()
    {
        string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        string random = Random.Shared.Next(10000000, 99999999).ToString();
        return $"2.25.{timestamp}.{random}";
    }
}
