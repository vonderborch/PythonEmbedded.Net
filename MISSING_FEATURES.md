# Missing API Features and Quality-of-Life Improvements

This document outlines potential API features and quality-of-life improvements that could enhance the PythonEmbedded.Net package.

## Package Management Features

### 1. **Package Querying**
- `ListInstalledPackagesAsync()` - List all installed packages with versions
- `GetPackageVersionAsync(string packageName)` - Get version of a specific package
- `IsPackageInstalledAsync(string packageName)` - Check if a package is installed
- `GetPackageInfoAsync(string packageName)` - Get detailed package information

### 2. **Package Uninstallation**
- `UninstallPackageAsync(string packageName, bool removeDependencies = false)`
- `UninstallPackage(string packageName, bool removeDependencies = false)` - Sync version

### 3. **Package Upgrade/Management**
- `UpgradeAllPackagesAsync()` - Upgrade all installed packages
- `ListOutdatedPackagesAsync()` - List packages with available updates
- `UpgradePackageAsync(string packageName)` - Already exists, but could be expanded
- `DowngradePackageAsync(string packageName, string targetVersion)` - Downgrade to specific version

### 4. **Package Search**
- `SearchPackagesAsync(string query)` - Search PyPI for packages
- `GetPackageMetadataAsync(string packageName)` - Get package metadata from PyPI

## Virtual Environment Features

### 5. **Requirements Export**
- `ExportRequirementsAsync(string outputPath)` - Export installed packages to requirements.txt
- `ExportRequirementsToStringAsync()` - Get requirements.txt as string
- `ExportRequirementsFreezeAsync(string outputPath)` - Export with exact versions (pip freeze)
- `ExportRequirementsFreezeToStringAsync()` - Get pip freeze output as string

### 6. **Virtual Environment Operations**
- `CloneVirtualEnvironmentAsync(string sourceName, string targetName)` - Clone/copy a virtual environment
- `ExportVirtualEnvironmentAsync(string name, string outputPath)` - Export venv to archive
- `ImportVirtualEnvironmentAsync(string name, string archivePath)` - Import venv from archive
- `GetVirtualEnvironmentSizeAsync(string name)` - Get disk usage of virtual environment
- `GetVirtualEnvironmentInfoAsync(string name)` - Get detailed information about a venv

## Instance Management

### 7. **Instance Information**
- `GetInstanceInfoAsync(string pythonVersion, string? buildDate)` - Get detailed instance info
- `GetInstanceSizeAsync(string pythonVersion, string? buildDate)` - Get disk usage
- `ValidateInstanceIntegrityAsync(string pythonVersion, string? buildDate)` - Verify installation integrity
- `GetPythonVersionInfoAsync()` - Get detailed Python version information (version, build info, etc.)
- `GetPipVersionAsync()` - Get pip version

### 8. **Instance Operations**
- `UpgradeInstanceAsync(string fromVersion, string toVersion)` - Upgrade Python version (complex)
- `ExportInstanceAsync(string pythonVersion, string outputPath)` - Backup/export instance
- `ImportInstanceAsync(string archivePath)` - Import/restore instance
- `GetInstanceDiskUsageAsync()` - Get total disk usage for all instances

## Execution Enhancements

### 9. **Environment Variables**
- `ExecuteCommandAsync(..., Dictionary<string, string>? environmentVariables = null)`
- `ExecuteScriptAsync(..., Dictionary<string, string>? environmentVariables = null)`
- Support for setting environment variables per execution

### 10. **Execution Configuration**
- `ExecuteCommandAsync(..., string? workingDirectory = null)` - Override working directory
- `ExecuteCommandAsync(..., ProcessPriority? priority = null)` - Set process priority
- `ExecuteCommandAsync(..., int? maxMemoryMB = null)` - Memory limits
- `ExecuteCommandAsync(..., TimeSpan? timeout = null)` - Per-execution timeout (in addition to CancellationToken)

### 11. **Execution Information**
- `GetExecutionStatistics()` - Get statistics about executions (count, average time, etc.)

## Configuration & Settings

### 12. **Pip Configuration**
- `ConfigurePipIndexAsync(string indexUrl, bool trusted = false)` - Configure custom PyPI index
- `ConfigurePipProxyAsync(string proxyUrl)` - Configure proxy for pip
- `GetPipConfigurationAsync()` - Get current pip configuration
- `InstallPackageAsync(..., string? indexUrl = null)` - Use custom index per installation

### 13. **Download Configuration**
- Support for download progress callbacks (already has IProgress<long>, but could expose in manager API)
- Resume interrupted downloads
- Parallel downloads for multiple instances
- Download retry with exponential backoff
- Download verification (checksums)

### 14. **Manager Configuration**
- Default Python version setting
- Default pip index URL
- Proxy settings
- Timeout configurations
- Retry policies

## Diagnostics & Health Checks

### 15. **Health Checks**
- `ValidatePythonInstallationAsync()` - Comprehensive health check
- `CheckDiskSpaceAsync(long requiredBytes)` - Check available disk space
- `TestNetworkConnectivityAsync()` - Check GitHub API connectivity
- `GetSystemRequirementsAsync()` - Get system requirements check
- `DiagnoseIssuesAsync()` - Run diagnostics and return issues found

### 16. **Information & Statistics**
- `GetManagerStatisticsAsync()` - Get statistics about managed instances
- `GetPythonVersionAsync()` - Get Python version string
- `GetPipVersionAsync()` - Get pip version
- `GetSystemInfoAsync()` - Get system/platform information
- `GetInstanceMetadataAsync(string pythonVersion, string? buildDate)` - Get detailed metadata

## Quality of Life Improvements

### 17. **Batch Operations**
- `InstallPackagesAsync(IEnumerable<string> packages, bool parallel = false)`
- `UninstallPackagesAsync(IEnumerable<string> packages, bool parallel = false)`
- `CreateVirtualEnvironmentsAsync(IEnumerable<string> names, bool parallel = false)`
- `DeleteVirtualEnvironmentsAsync(IEnumerable<string> names, bool parallel = false)`

### 18. **Progress Reporting**
- Progress callbacks for package installation
- Progress callbacks for virtual environment creation
- Overall operation progress (download + extract + verify)

### 19. **Error Recovery**
- Automatic retry for transient failures
- Resume interrupted downloads
- Rollback on installation failure
- Better error recovery strategies

### 20. **Validation & Safety**
- `ValidatePythonVersionStringAsync(string version)` - Validate version format
- `ValidatePackageSpecificationAsync(string spec)` - Validate package spec format
- `PreFlightCheckAsync(string pythonVersion)` - Pre-flight checks before download
- Warning for disk space before large operations

### 21. **Convenience Methods**
- `GetLatestPythonVersionAsync()` - Get the latest available Python version
- `GetRecommendedPythonVersionAsync()` - Get recommended version for current platform
- `FindBestMatchingVersionAsync(string versionSpec)` - Find best matching version
- `EnsurePythonVersionAsync(string minVersion, string? maxVersion)` - Ensure version range

### 22. **Event/Callback Support**
- Events for instance creation/deletion
- Events for virtual environment operations
- Events for package installation
- Events for download progress (beyond IProgress)

### 23. **AsyncEnumerable Support**
- `ListInstancesAsync()` - Stream instances as they're discovered
- `ListAvailableVersionsAsync()` - Stream versions as they're fetched
- `ListPackagesAsync()` - Stream packages as they're parsed

## Advanced Features

### 24. **Plugin/Extension Support**
- Custom package sources
- Custom download providers
- Custom execution strategies
- Plugin architecture for extensibility

### 25. **Multi-Instance Operations**
- Execute command across multiple Python versions
- Install package across multiple instances
- Batch operations across instances

### 26. **Caching & Performance**
- Package download caching
- Virtual environment templates
- Instance templates
- Pre-warmed instances

### 27. **Security Features**
- Package verification (checksums, signatures)
- Sandboxed execution
- Resource quotas
- Security policy enforcement

## Documentation & Developer Experience

### 28. **Better Error Messages**
- More descriptive error messages with suggested fixes
- Error codes for programmatic handling
- Troubleshooting hints in exceptions

### 29. **Logging Enhancements**
- Structured logging with more context
- Log levels for different operations
- Performance metrics in logs
- Operation tracing

### 30. **Testing & Validation**
- Test execution capabilities
- Validation utilities
- Mock/test helpers for unit tests
- Integration test utilities

## Priority Recommendations

### High Priority (Most Useful)
1. **Package Querying** (#1) - Essential for dependency management
2. **Package Uninstallation** (#2) - Basic package management
3. **Requirements Export** (#5) - Common workflow need
4. **Environment Variables** (#9) - Needed for many Python scripts
5. **Pip Configuration** (#12) - Important for enterprise/air-gapped environments

### Medium Priority (Very Useful)
6. **Health Checks** (#15) - Helpful for diagnostics
7. **Instance Information** (#7) - Useful for monitoring
8. **Batch Operations** (#17) - Quality of life improvement
9. **Progress Reporting** (#18) - Better user experience
10. **Validation & Safety** (#20) - Prevent errors early

### Low Priority (Nice to Have)
11. **Virtual Environment Cloning** (#6) - Less common use case
12. **Advanced Execution Configuration** (#10) - Edge cases
13. **Multi-Instance Operations** (#25) - Advanced scenarios
14. **Plugin Support** (#24) - Complex to implement

