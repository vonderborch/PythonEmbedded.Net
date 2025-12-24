# Models

Data models and configuration classes used throughout PythonEmbedded.Net.

## Classes

- [InstanceMetadata](./Models/InstanceMetadata.md) - Metadata for Python instances (stored in `instance_metadata.json` in each instance directory)
- [ManagerConfiguration](./Models/ManagerConfiguration.md) - Configuration for managers
- [ManagerMetadata](./Models/ManagerMetadata.md) - In-memory collection that manages multiple InstanceMetadata objects (no central metadata file)
- [PlatformInfo](./Models/PlatformInfo.md) - Platform information

## Records

- [PackageInfo](./Models/PackageInfo.md) - Package information record
- [OutdatedPackageInfo](./Models/PackageInfo.md) - Outdated package information record
- [PipConfiguration](./Models/PipConfiguration.md) - Pip configuration record
- [PyPIPackageInfo](./Models/PyPIPackageInfo.md) - PyPI package information record
- [PyPISearchResult](./Models/PyPIPackageInfo.md) - PyPI search result record

## Overview

Models provide:
- Configuration settings for managers and operations
- Metadata for Python instances (each instance has its own `instance_metadata.json` file)
- In-memory collection for managing multiple instances (no central metadata file)
- Package information and search results
- Platform detection and information



