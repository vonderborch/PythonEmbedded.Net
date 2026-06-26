using System.Reflection;

namespace PythonEmbedded.Net.Helpers;

/// <summary>
/// Provides utility methods for working with .NET reflection, such as retrieving loaded assemblies
/// or scanning for types that are marked with specific attributes.
/// </summary>
public static class ReflectionHelpers
{
    private static Assembly[]? _loadedAssemblies;

    /// <summary>
    /// Retrieves the assemblies currently loaded in the application domain.
    /// Allows the option to refresh the loaded assemblies list if requested.
    /// </summary>
    /// <param name="refresh">Specifies whether to refresh and reload the list of loaded assemblies.</param>
    /// <returns>An array of <see cref="Assembly"/> objects representing the assemblies currently loaded in the application domain.</returns>
    public static Assembly[] GetLoadedAssemblies(bool refresh = false)
    {
        if (refresh || _loadedAssemblies is null)
        {
            _loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
        }
        
        return _loadedAssemblies;
    }

    /// <summary>
    /// Scans all loaded assemblies for types annotated with the specified attribute.
    /// Returns a list of tuples containing the attribute instance and the associated type.
    /// </summary>
    /// <param name="refreshAssemblies">Specifies whether to refresh the list of loaded assemblies before scanning for types.</param>
    /// <typeparam name="T">The type of attribute to search for. Must inherit from <see cref="Attribute"/>.</typeparam>
    /// <returns>A list of tuples where each tuple contains an attribute instance of type <typeparamref name="T"/> and the type it is associated with.</returns>
    public static List<(T attributeInstance, Type type)> GetTypesWithAttribute<T>(bool refreshAssemblies = false)
        where T : Attribute
    {
        Type attributeToScanFor = typeof(T);
        List<(T attributeInstance, Type type)> typesWithAttribute = new();
        
        Assembly[] assemblies = GetLoadedAssemblies(refreshAssemblies);
        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes())
            {
                foreach (var typeAttribute in type.GetCustomAttributes(attributeToScanFor, false))
                {
                    typesWithAttribute.Add(((T)typeAttribute, type));
                }
            }
        }
        
        return typesWithAttribute;
    }
}
