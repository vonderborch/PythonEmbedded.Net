namespace PythonEmbedded.Net.ImplementationRegistration;

/// <summary>
/// An attribute used to designate a class as an implementation of a Python environment.
/// This attribute is intended for classes that encapsulate functionality
/// related to embedding and executing Python code within a .NET application.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class PythonEnvironmentRegistrationAttribute : Attribute
{
    /// <summary>
    /// An attribute used to designate a class as an implementation of a Python environment.
    /// This is intended to be used for classes that handle the functionality
    /// of embedding and executing Python code in a .NET application.
    /// </summary>
    public PythonEnvironmentRegistrationAttribute(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Environment implementation name cannot be null or whitespace.", nameof(name));
        }

        Name = name;
    }

    /// <summary>
    /// Gets the name of the Python environment implementation.
    /// This name is used to uniquely identify the implementation of the Python environment
    /// class that handles embedding and executing Python code within a .NET application.
    /// </summary>
    public string Name { get; }
}
