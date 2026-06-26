namespace PythonEmbedded.Net.ImplementationRegistration;

/// <summary>
/// An attribute used to designate a class as a Python runtime implementation.
/// This attribute is intended to be applied to classes that provide specific runtime
/// implementations of Python functionality within an embedded .NET environment.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class PythonRuntimeBackendRegistrationAttribute : Attribute
{
    /// <summary>
    /// Attribute to designate a class as a Python runtime implementation.
    /// </summary>
    public PythonRuntimeBackendRegistrationAttribute(string name, string? environmentName = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Runtime implementation name cannot be null or whitespace.", nameof(name));
        }

        Name = name;
        EnvironmentName = environmentName ?? Constants.DefaultEnvironment;
        
        if (string.IsNullOrWhiteSpace(EnvironmentName))
        {
            throw new ArgumentException("Environment name cannot be null or whitespace.", nameof(environmentName));
        }
    }

    /// <summary>
    /// Gets the name of the Python runtime implementation.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the name of the environment associated with the Python runtime implementation.
    /// </summary>
    public string EnvironmentName { get; }
}
