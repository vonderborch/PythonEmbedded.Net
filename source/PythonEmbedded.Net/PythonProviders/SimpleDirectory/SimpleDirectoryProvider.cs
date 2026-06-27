using System.Globalization;
using PythonEmbedded.Net.Helpers;
using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net.PythonProviders.SimpleDirectory;

public class SimpleDirectoryProvider : PythonProvider
{
    public SimpleDirectoryProvider(string archiveDirectory)
    {
        ArchiveDirectory = archiveDirectory;
    }
    
    
    public string ArchiveDirectory { get; }
    
    
    public override async Task<List<(DateTime buildDate, List<string> pythonVersions, List<PythonRelease>? releaseAssets)>> GetAvailablePythonBuildsAndVersionsAsync(PlatformInfo platform, bool includeReleaseObjects = false,
        int maxResults = 10, DateTime? minBuildDate = null, DateTime? maxBuildDate = null, List<Version>? requiredVersions = null,
        CancellationToken cancellationToken = default)
    {
        List<(DateTime buildDate, List<string> pythonVersions, List<PythonRelease>? releaseAssets)> availableBuildDates = new();
        string platformTargetTriple = platform.TargetTriple?.ToLowerInvariant()!;
        
        minBuildDate ??= DateTime.MinValue;
        maxBuildDate ??= DateTime.MaxValue;
        requiredVersions ??= new List<Version>();
        
        // Scan through the directory's subdirectories and their files and check if they match. subdirectories = named by date
        foreach (var subdirectory in Directory.GetDirectories(ArchiveDirectory))
        {
            var releaseDate = DateTime.ParseExact(Path.GetFileName(subdirectory), "yyyyMMdd", CultureInfo.InvariantCulture);
            if (releaseDate < minBuildDate || releaseDate > maxBuildDate)
            {
                continue;
            }
            
            List<string> matchingVersions = new();
            List<PythonRelease>? matchingReleaseAssets = includeReleaseObjects ? new() : null;
            foreach (var file in Directory.GetFiles(subdirectory))
            {
                if (AssetHelpers.IsMatchingPlatform(file, platformTargetTriple))
                {
                    Version? pythonVersion = AssetHelpers.GetVersionFromAssetName(file);
                    if (pythonVersion is not null)
                    {
                        bool meetsMatchingVersionRequirements = requiredVersions.Count == 0;
                        foreach (var requiredVersion in requiredVersions)
                        {
                            if (AssetHelpers.IsMatchingVersion(pythonVersion, requiredVersion))
                            {
                                meetsMatchingVersionRequirements = true;
                                break;
                            }
                        }
                                    
                        if (meetsMatchingVersionRequirements)
                        {
                            matchingVersions.Add(pythonVersion.ToString());

                            if (includeReleaseObjects)
                            {
                                PythonRelease releaseObject =
                                    new SimpleDirectoryRelease(file, pythonVersion, platform, releaseDate);
                                matchingReleaseAssets!.Add(releaseObject);
                            }
                        }
                    }
                }
            }
            
            if (matchingVersions.Count > 0)
            {
                availableBuildDates.Add((releaseDate, matchingVersions, matchingReleaseAssets));
                            
                // break if we have enough results
                if (maxResults > 0 && availableBuildDates.Count >= maxResults)
                {
                    break;
                }
            }
        }
        
        return availableBuildDates;
    }
}
