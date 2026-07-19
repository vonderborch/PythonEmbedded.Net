# PythonEmbedded.Net

A .NET library for managing local, embeddable Python instances. Download, install, manage, and execute Python environments directly within .NET applications without requiring system-wide Python installations.

![Logo](https://raw.githubusercontent.com/vonderborch/PythonEmbedded.Net/refs/heads/main/logo.png)

## Installation

### Nuget

[![NuGet version (PythonEmbedded.Net)](https://img.shields.io/nuget/v/PythonEmbedded.Net.svg?style=flat-square)](https://www.nuget.org/packages/PythonEmbedded.Net/)

The recommended installation approach is to use the available nuget
package: [PythonEmbedded.Net](https://www.nuget.org/packages/PythonEmbedded.Net/)

### Clone

Alternatively, you can clone this repo and reference the PythonEmbedded.Net project in your project.

## Features

- ✅ **Automatic Python Distribution Management**: Download and install Python distributions from [python-build-standalone](https://github.com/astral-sh/python-build-standalone)
- ✅ **Fast Package Management with uv**: [uv](https://github.com/astral-sh/uv) is the default package manager (auto-installed); opt into classic `python -m pip` / `python -m venv` with `useUv: false`
- ✅ **Multiple Instance Support**: Manage multiple Python versions and build dates simultaneously
- ✅ **Smart Version Matching**: 
  - Exact versions (e.g., "3.12.5") match exactly
  - Partial versions (e.g., "3.12") automatically find the latest patch version (e.g., "3.12.19")
- ✅ **Virtual Environment Management**: Create, clone, export, and import virtual environments for each Python instance
- ✅ **Package Installation**: Install, list, uninstall, and upgrade packages; requirements.txt and pyproject.toml; requirements checking and PyPI search
- ✅ **Python Execution**: Execute Python code via subprocess or in-process using Python.NET
- ✅ **Two Execution Modes**: 
  - **PythonManager**: Subprocess-based execution (standard Python execution)
  - **PythonNetManager**: Python.NET-based execution (in-process, high-performance)
- ✅ **Cross-Platform**: Supports Windows, Linux, and macOS
- ✅ **Archive Format Support**: Supports multiple archive formats (zip, tar.gz, tar.bz2, tar.bz, tar.zst)
- ✅ **Modern C# Design**: Abstract classes for extensibility, dependency injection support, IDisposable for resource management
- ✅ **Structured Logging**: Full support for Microsoft.Extensions.Logging
- ✅ **Performance Optimizations**: Optional caching for GitHub API responses, object pooling for frequently allocated objects

## Requirements

- .NET 9.0 or .NET 10.0 (library version **1.4.x**)
- Octokit (included)
- Python.NET (included, optional - only needed for PythonNetManager)
- Tomlyn (for pyproject.toml support, included)

## Quick Start

## Platform Support

The library automatically detects your platform and downloads the appropriate Python distribution from [python-build-standalone](https://github.com/astral-sh/python-build-standalone):

- **Windows**: x64, x86 (Windows 7+)
- **Linux**: x64, ARM64, ARMv7 (GNU libc and musl)
- **macOS**: Intel (x64), Apple Silicon (ARM64)

## Archive Format Support

The library supports multiple archive formats for Python distributions:

- **`.zip`** - Standard ZIP archives (Windows, cross-platform)
- **`.tar.gz`** - Gzip-compressed tar archives (Linux, macOS)
- **`.tar.bz2`** - Bzip2-compressed tar archives (Linux, macOS)
- **`.tar.bz`** - Bzip-compressed tar archives (Linux, macOS)
- **`.tar.zst`** - Zstandard-compressed tar archives (Linux, macOS)

Archive extraction uses system tools (`tar` command) where available. The library automatically detects and handles the appropriate format based on the downloaded asset.

## Design Principles

This library follows modern C# best practices:

- **Abstract Base Classes**: Extensible architecture using abstract base classes
- **Dependency Injection**: Support for DI containers with logger factories and caching
- **Resource Management**: IDisposable support for Python.NET runtimes
- **Modern C# Features**: Records, file-scoped namespaces, collection expressions, pattern matching, ConfigureAwait(false)
- **Structured Logging**: Full Microsoft.Extensions.Logging integration
- **Separation of Concerns**: Process execution extracted to a service
- **Performance Optimizations**: Optional caching, object pooling for hot paths

## Contributing

Contributions are welcome! Please read the contributing guidelines and submit pull requests.

## License

MIT License - see LICENSE file for details.

## Acknowledgments

This library utilizes [python-build-standalone](https://github.com/astral-sh/python-build-standalone) by [astral-sh](https://github.com/astral-sh) for providing high-quality, redistributable Python distributions. We are not associated with astral-sh, but we thank them for their fantastic work that makes this library possible.

## Links

- [Python Build Standalone](https://github.com/astral-sh/python-build-standalone) - Source of Python distributions
- [Python.NET](https://github.com/pythonnet/pythonnet) - Python.NET integration
- [Octokit](https://github.com/octokit/octokit.net) - GitHub API client
