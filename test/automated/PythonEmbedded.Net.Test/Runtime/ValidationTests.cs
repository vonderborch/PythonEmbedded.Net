using NUnit.Framework;
using PythonEmbedded.Net;

namespace PythonEmbedded.Net.Test.Runtime;

/// <summary>
/// Unit tests for validation methods.
/// </summary>
[TestFixture]
public class ValidationTests
{
    [Test]
    public void ValidatePythonVersionString_WithValidVersion_ReturnsTrue()
    {
        Assert.That(BasePythonRuntime.ValidatePythonVersionString("3.12.0"), Is.True);
        Assert.That(BasePythonRuntime.ValidatePythonVersionString("3.12"), Is.True);
        Assert.That(BasePythonRuntime.ValidatePythonVersionString("3"), Is.True);
        Assert.That(BasePythonRuntime.ValidatePythonVersionString("3.12.0.1"), Is.True);
    }

    [Test]
    public void ValidatePythonVersionString_WithInvalidVersion_ReturnsFalse()
    {
        Assert.That(BasePythonRuntime.ValidatePythonVersionString(""), Is.False);
        Assert.That(BasePythonRuntime.ValidatePythonVersionString(" "), Is.False);
        Assert.That(BasePythonRuntime.ValidatePythonVersionString("abc"), Is.False);
        Assert.That(BasePythonRuntime.ValidatePythonVersionString("3.12.0a"), Is.False);
        Assert.That(BasePythonRuntime.ValidatePythonVersionString("3.12.0-beta"), Is.False);
    }

    [Test]
    public void ValidatePythonVersionString_WithNull_ReturnsFalse()
    {
        Assert.That(BasePythonRuntime.ValidatePythonVersionString(null!), Is.False);
    }

    [Test]
    public void ValidatePackageSpecification_WithValidSpec_ReturnsTrue()
    {
        Assert.That(BasePythonRuntime.ValidatePackageSpecification("numpy"), Is.True);
        Assert.That(BasePythonRuntime.ValidatePackageSpecification("numpy==1.20.0"), Is.True);
        Assert.That(BasePythonRuntime.ValidatePackageSpecification("numpy>=1.20.0"), Is.True);
        Assert.That(BasePythonRuntime.ValidatePackageSpecification("numpy<=1.20.0"), Is.True);
        Assert.That(BasePythonRuntime.ValidatePackageSpecification("numpy>1.20.0"), Is.True);
        Assert.That(BasePythonRuntime.ValidatePackageSpecification("numpy<1.20.0"), Is.True);
        Assert.That(BasePythonRuntime.ValidatePackageSpecification("numpy~=1.20.0"), Is.True);
        Assert.That(BasePythonRuntime.ValidatePackageSpecification("numpy!=1.20.0"), Is.True);
    }

    [Test]
    public void ValidatePackageSpecification_WithInvalidSpec_ReturnsFalse()
    {
        Assert.That(BasePythonRuntime.ValidatePackageSpecification(""), Is.False);
        Assert.That(BasePythonRuntime.ValidatePackageSpecification(" "), Is.False);
        Assert.That(BasePythonRuntime.ValidatePackageSpecification("==1.20.0"), Is.False); // Missing package name
        Assert.That(BasePythonRuntime.ValidatePackageSpecification("numpy=="), Is.False); // Missing version
    }

    [Test]
    public void ValidatePackageSpecification_WithNull_ReturnsFalse()
    {
        Assert.That(BasePythonRuntime.ValidatePackageSpecification(null!), Is.False);
    }
}

