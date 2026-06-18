namespace PythonEmbedded.Net.Models;

public class ManagedPythonMetadata
{
    public Version Version { get; set; }
    
    public string? BuildDate { get; set; }
    
    public List<ManagedPythonEnvironmentMetadata> Environments { get; set; } = new();
}