# Contributing to PythonEmbedded.Net

Thank you for your interest in contributing to PythonEmbedded.Net! This document provides guidelines and instructions for contributing.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [Development Setup](#development-setup)
- [Coding Standards](#coding-standards)
- [Testing](#testing)
- [Submitting Changes](#submitting-changes)
- [Documentation](#documentation)

## Code of Conduct

By participating in this project, you agree to maintain a respectful and inclusive environment for all contributors.

## Getting Started

1. **Fork the repository** on GitHub
2. **Clone your fork** locally
3. **Create a branch** for your changes
4. **Make your changes**
5. **Test your changes**
6. **Submit a pull request**

## Development Setup

### Prerequisites

- .NET 9.0 SDK or later
- Visual Studio, Rider, or VS Code with C# extension
- Git

### Building the Project

```bash
# Clone the repository
git clone https://github.com/vonderborch/PythonEmbedded.Net.git
cd PythonEmbedded.Net

# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Run tests
dotnet test
```

### Project Structure

```
PythonEmbedded.Net/
├── source/
│   └── PythonEmbedded.Net/        # Main library source
│       ├── Exceptions/            # Custom exceptions
│       ├── Helpers/               # Utility classes
│       ├── Models/                # Data models
│       ├── Services/              # Service implementations
│       └── *.cs                   # Core classes
├── test/
│   └── automated/
│       └── PythonEmbedded.Net.Test/  # Unit and integration tests
└── Docs/                          # Documentation
```

## Coding Standards

### C# Style Guide

This project follows modern C# best practices:

- **File-scoped namespaces**: `namespace PythonEmbedded.Net;`
- **Records for value objects**: Use `record` types for immutable data
- **Async/Await**: Use `ConfigureAwait(false)` in library code
- **Interfaces first**: Create interfaces for testability
- **Nullable reference types**: Enabled, use `?` appropriately
- **Collection expressions**: Use `[]` for arrays when appropriate

### Naming Conventions

- **Classes**: PascalCase (e.g., `PythonManager`)
- **Interfaces**: PascalCase with `I` prefix (e.g., `IPythonRuntime`)
- **Methods**: PascalCase (e.g., `ExecuteCommandAsync`)
- **Parameters**: camelCase (e.g., `pythonVersion`)
- **Private fields**: camelCase with `_` prefix (e.g., `_logger`)

### Code Organization

- One class per file
- Group related functionality in namespaces
- Use regions sparingly, prefer clear organization
- Keep methods focused and single-purpose

### Documentation

- XML documentation comments for all public APIs
- Inline comments for complex logic
- Clear, descriptive variable and method names

Example:

```csharp
/// <summary>
/// Executes a Python command and returns the result.
/// </summary>
/// <param name="command">The Python command to execute.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>The execution result containing exit code, stdout, and stderr.</returns>
public async Task<PythonExecutionResult> ExecuteCommandAsync(
    string command,
    CancellationToken cancellationToken = default)
{
    // Implementation
}
```

## Testing

### Test Structure

- **Unit tests**: Test individual components with mocks
- **Integration tests**: Test with real Python installations (marked `[Ignore]` for CI)

### Writing Tests

```csharp
[TestFixture]
public class PythonManagerTests
{
    [Test]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange
        var githubClient = new GitHubClient(new ProductHeaderValue("Test"));
        
        // Act
        var manager = new PythonManager("./test-instances", githubClient);
        
        // Assert
        Assert.NotNull(manager);
    }
}
```

### Running Tests

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test test/automated/PythonEmbedded.Net.Test/PythonEmbedded.Net.Test.csproj

# Run with coverage
dotnet test /p:CollectCoverage=true
```

### Test Utilities

Use test utilities from `TestUtilities` namespace:
- `TestDirectoryHelper`: Create and cleanup test directories
- `MockPythonInstanceHelper`: Create mock Python installations

## Submitting Changes

### Before Submitting

1. **Ensure tests pass**: Run `dotnet test`
2. **Build succeeds**: Run `dotnet build`
3. **Code formatting**: Ensure consistent formatting
4. **Documentation**: Update XML docs and markdown docs as needed

### Pull Request Process

1. **Create a descriptive title**: Clearly describe the change
2. **Write a detailed description**:
   - What changed and why
   - How to test the changes
   - Related issues (if any)
3. **Keep changes focused**: One logical change per PR
4. **Update documentation**: Include relevant doc updates

### Commit Messages

Follow conventional commit format:

```
type(scope): subject

body (optional)

footer (optional)
```

Types:
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation changes
- `refactor`: Code refactoring
- `test`: Test changes
- `chore`: Maintenance tasks

Examples:

```
feat(runtime): Add support for custom process executor

Allow injection of IProcessExecutor for testing and customization.

Closes #123
```

```
fix(manager): Handle missing Python executable gracefully

Throw PythonNotInstalledException with clear error message when
executable is not found.
```

## Documentation

### Code Documentation

- Add XML documentation for all public APIs
- Keep documentation up-to-date with code changes
- Include examples in XML docs for complex APIs

### Markdown Documentation

Update relevant docs in `Docs/` directory:

- **API-Reference.md**: API changes
- **Examples.md**: New usage examples
- **Architecture.md**: Design changes
- **Error-Handling.md**: New exceptions
- **Troubleshooting.md**: Common issues

### Documentation Format

- Use clear, concise language
- Include code examples
- Cross-reference related docs
- Keep table of contents updated

## Architecture Guidelines

### Design Principles

- **Interfaces first**: Create interfaces for major components
- **Dependency injection**: Support DI for testability
- **Separation of concerns**: Clear responsibility boundaries
- **Resource management**: Implement IDisposable where appropriate

### Adding New Features

1. **Design the interface**: Define the public API first
2. **Create interfaces**: Add interfaces for testability
3. **Implement base classes**: Use abstract base classes for shared logic
4. **Add concrete implementations**: Implement specific behaviors
5. **Write tests**: Comprehensive test coverage
6. **Update documentation**: API reference and examples

### Extending Existing Features

- Maintain backward compatibility when possible
- Use virtual methods for extensibility
- Document breaking changes clearly
- Provide migration guidance

## Review Process

- All PRs require review
- Address review comments promptly
- Maintain a constructive discussion
- Tests must pass before merge

## Questions?

- Check existing documentation
- Review existing issues and PRs
- Ask questions in issues or discussions

Thank you for contributing to PythonEmbedded.Net!

