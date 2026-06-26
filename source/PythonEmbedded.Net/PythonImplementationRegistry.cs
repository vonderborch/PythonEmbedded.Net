using PythonEmbedded.Net.Helpers;
using PythonEmbedded.Net.ImplementationRegistration;
using Singletons.Net;

namespace PythonEmbedded.Net;

/// <summary>
/// A singleton class responsible for managing and retrieving Python runtime backends
/// and environments associated with Python implementations. It provides mechanisms
/// to query registered backends and environments and ensures that the relevant
/// mappings are up-to-date.
/// </summary>
public class PythonImplementationRegistry : SingletonBase<PythonImplementationRegistry>
{
    /// <summary>
    /// A private field that stores a mapping between backend names and their corresponding
    /// CLR types representing Python runtime backends. This dictionary is used internally
    /// by the <see cref="PythonImplementationRegistry"/> class to manage and retrieve
    /// backend types dynamically during runtime. It is populated during the scanning process
    /// when discovering registered backends in the application.
    /// </summary>
    private Dictionary<string, Type> _backends = new();

    /// <summary>
    /// A private field that holds a mapping between Python environment names and their associated
    /// types, representing the implementations of these environments. This dictionary is used to
    /// register and query the available Python runtime environments within the
    /// <see cref="PythonImplementationRegistry"/> class. It plays a key role in resolving the types
    /// for environments during runtime operations, ensuring efficient management of environment
    /// registrations.
    /// </summary>
    private Dictionary<string, Type> _environments = new();

    /// <summary>
    /// A private field that provides a mapping between backend names and their
    /// associated Python environments. This dictionary is used to establish and
    /// maintain the relationship between registered backends and corresponding
    /// environments, enabling efficient lookups and ensuring consistency across
    /// the mappings in the <see cref="PythonImplementationRegistry"/> class.
    /// </summary>
    private Dictionary<string, string> _backendToEnvironmentMapping = new();

    /// <summary>
    /// A private field that maintains a mapping between Python environment names
    /// and the corresponding lists of backend names associated with each environment.
    /// This dictionary enables quick lookups for identifying all backends that belong
    /// to a specific Python environment. It is primarily used for managing relationships
    /// between Python runtime environments and their backends in the
    /// <see cref="PythonImplementationRegistry"/> class.
    /// </summary>
    private Dictionary<string, List<string>> _environmentToBackendsMapping = new();

    /// <summary>
    /// Retrieves the type associated with a registered Python runtime backend.
    /// </summary>
    /// <param name="backend">
    /// The name of the backend for which the type is being requested.
    /// </param>
    /// <returns>
    /// The <see cref="Type"/> of the registered backend.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the specified backend is not registered.
    /// </exception>
    public Type GetBackendType(string backend)
    {
        Scan();
        if (_backends.ContainsKey(backend))
        {
            throw new InvalidOperationException($"Backend '{backend}' is not registered.");
        }

        return _backends[backend];
    }

    /// <summary>
    /// Retrieves the type associated with a registered Python runtime environment.
    /// </summary>
    /// <param name="environment">
    /// The name of the environment for which the type is being requested.
    /// </param>
    /// <returns>
    /// The <see cref="Type"/> of the registered environment.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the specified environment is not registered.
    /// </exception>
    public Type GetEnvironmentType(string environment)
    {
        Scan();
        if (_environments.ContainsKey(environment))
        {
            throw new InvalidOperationException($"Environment '{environment}' is not registered.");
        }

        return _environments[environment];
    }

    /// <summary>
    /// Retrieves a list of all runtime backends and their associated environments
    /// registered for Python runtime integration.
    /// </summary>
    /// <returns>
    /// A sorted list of tuples, where each entry contains the name of a backend
    /// and the name of its associated environment.
    /// </returns>
    public List<(string backend, string environment)> GetRuntimeBackends()
    {
        Scan();
        List<(string _backends, string environment)> backendsAndEnvironments = _backendToEnvironmentMapping.Select(x => (x.Key, x.Value)).ToList();
        backendsAndEnvironments.Sort();
        return backendsAndEnvironments;
    }

    /// <summary>
    /// Scans assemblies and registers backend and environment implementations for Python runtime integration.
    /// </summary>
    /// <param name="refresh">
    /// A boolean value that indicates whether to force a refresh of the scanned data.
    /// If set to true, the method will clear all previously scanned data and re-scan the assemblies.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if a backend is associated with an environment that is not registered.
    /// </exception>
    public void Scan(bool refresh = false)
    {
        if (!refresh && _backends.Count > 0)
        {
            return;
        }

        _backends.Clear();
        _environments.Clear();
        _backendToEnvironmentMapping.Clear();
        _environmentToBackendsMapping.Clear();
        
        // Scan assemblies for any PythonRuntimeBackendRegistration labeled classes
        List<(PythonRuntimeBackendRegistrationAttribute attributeInstance, Type type)> backendTypes = ReflectionHelpers.GetTypesWithAttribute<PythonRuntimeBackendRegistrationAttribute>();
        
        // Scan assemblies for any PythonRuntimeEnvironmentRegistration labeled classes
        List<(PythonEnvironmentRegistrationAttribute attributeInstance, Type type)> environmentTypes = ReflectionHelpers.GetTypesWithAttribute<PythonEnvironmentRegistrationAttribute>();
        
        // Populate the mapping dictionaries
        foreach (var backend in backendTypes)
        {
            _backends.Add(backend.attributeInstance.Name, backend.type);
            _backendToEnvironmentMapping.Add(backend.attributeInstance.Name, backend.attributeInstance.EnvironmentName);
            if (!_environmentToBackendsMapping.ContainsKey(backend.attributeInstance.Name))
            {
                _environmentToBackendsMapping.Add(backend.attributeInstance.Name, new List<string>());
            }
            _environmentToBackendsMapping[backend.attributeInstance.Name].Add(backend.attributeInstance.Name);
        }
        foreach (var environment in environmentTypes)
        {
            _environments.Add(environment.attributeInstance.Name, environment.type);
        }
        
        // Validate that all backends are associated with an actually registered environment
        foreach (var backend in backendTypes)
        {
            if (!_environments.ContainsKey(backend.attributeInstance.EnvironmentName))
            {
                throw new InvalidOperationException($"Backend '{backend.attributeInstance.Name}' is not associated with any registered environment.");
            }
        }
    }
}
