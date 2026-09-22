namespace PhysioTrac.Application.Configuration;

public class AppOptions
{
    public const string SectionName = "App";

    /// <summary>Base URL of the SPA — used to build invitation-activation
    /// links (`{FrontendBaseUrl}/{slug}/activate?token=...`), mirroring the
    /// original `settings.FRONTEND_BASE_URL`.</summary>
    public string FrontendBaseUrl { get; set; } = "http://localhost:5173";
}
