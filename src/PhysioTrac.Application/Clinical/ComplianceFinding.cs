namespace PhysioTrac.Application.Clinical;

public record ComplianceFinding(string Code, string Severity, string Title, string Detail, bool FinalizationBlocker = false);
