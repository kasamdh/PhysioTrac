namespace PhysioTrac.Application.Configuration;

/// <summary>Where uploaded patient files actually live. Deliberately never a
/// path under wwwroot -- nothing here is ever meant to be reachable by a
/// direct static-file URL; every read goes through DocumentService's own
/// tenant/patient access check.</summary>
public class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Absolute, or relative to the app's content root.</summary>
    public string PatientDocumentRoot { get; set; } = "App_Data/patient-documents";

    public long MaxUploadBytes { get; set; } = 25 * 1024 * 1024;

    public string[] AllowedExtensions { get; set; } =
    {
        ".pdf", ".jpg", ".jpeg", ".png", ".tif", ".tiff", ".doc", ".docx",
    };
}
