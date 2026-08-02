namespace PythonEmbedded.Net.Models;

/// <summary>Severity of a single <see cref="DiagnosticFinding"/>.</summary>
public enum DiagnosticSeverity
{
    /// <summary>Informational; no action needed.</summary>
    Info,

    /// <summary>Something looks off but isn't necessarily broken.</summary>
    Warning,

    /// <summary>The installation/environment is unusable as-is.</summary>
    Error,
}

/// <summary>A single diagnostic observation produced by a <c>DiagnoseAsync</c> call.</summary>
/// <param name="Severity">How serious the finding is.</param>
/// <param name="Code">A short stable identifier for the finding, e.g. <c>"executable-missing"</c>.</param>
/// <param name="Message">A human-readable description.</param>
public sealed record DiagnosticFinding(DiagnosticSeverity Severity, string Code, string Message);

/// <summary>The result of a health check, e.g. <see cref="PythonInstallation.DiagnoseAsync"/>.</summary>
/// <param name="IsHealthy">True when <see cref="Findings"/> contains no <see cref="DiagnosticSeverity.Error"/> entries.</param>
/// <param name="Findings">All findings collected during the check, in the order they were checked.</param>
public sealed record DiagnosticsResult(bool IsHealthy, IReadOnlyList<DiagnosticFinding> Findings);
