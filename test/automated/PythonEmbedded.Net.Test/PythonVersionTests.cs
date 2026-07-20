namespace PythonEmbedded.Net.Test;

[TestFixture]
public class PythonVersionTests
{
    [TestCase("3.13.14", 3, 13, 14, null)]
    [TestCase("3.15.0b3", 3, 15, 0, "b3")]
    [TestCase("3.14.0rc1", 3, 14, 0, "rc1")]
    public void Parse_FullVersions(string input, int major, int minor, int patch, string? suffix)
    {
        PythonVersion version = PythonVersion.Parse(input);
        Assert.Multiple(() =>
        {
            Assert.That(version.Major, Is.EqualTo(major));
            Assert.That(version.Minor, Is.EqualTo(minor));
            Assert.That(version.Patch, Is.EqualTo(patch));
            Assert.That(version.Suffix, Is.EqualTo(suffix));
            Assert.That(version.ToString(), Is.EqualTo(input));
        });
    }

    [TestCase("3.13")]
    [TestCase("3")]
    [TestCase("")]
    [TestCase("python3")]
    public void Parse_Rejects_PartialOrInvalid(string input)
        => Assert.That(() => PythonVersion.Parse(input), Throws.TypeOf<FormatException>());

    [Test]
    public void CompareTo_Orders_Numerically_And_PreReleaseFirst()
    {
        PythonVersion[] versions =
        [
            PythonVersion.Parse("3.15.0b3"),
            PythonVersion.Parse("3.13.14"),
            PythonVersion.Parse("3.15.0"),
            PythonVersion.Parse("3.9.2"),
        ];
        PythonVersion[] sorted = [.. versions.OrderBy(v => v)];

        Assert.That(
            sorted.Select(v => v.ToString()),
            Is.EqualTo((string[])["3.9.2", "3.13.14", "3.15.0b3", "3.15.0"]));
    }

    [TestCase("latest", "3.13.14", true)]
    [TestCase("3", "3.13.14", true)]
    [TestCase("3", "2.7.18", false)]
    [TestCase("3.13", "3.13.14", true)]
    [TestCase("3.13", "3.12.9", false)]
    [TestCase("3.13.14", "3.13.14", true)]
    [TestCase("3.13.14", "3.13.2", false)]
    [TestCase("3.15.0", "3.15.0b3", false)]
    [TestCase("3.15.0b3", "3.15.0b3", true)]
    public void Request_Matches(string request, string version, bool expected)
        => Assert.That(
            PythonVersionRequest.Parse(request).Matches(PythonVersion.Parse(version)),
            Is.EqualTo(expected));

    [TestCase("banana")]
    [TestCase("3.13.x")]
    [TestCase("")]
    public void Request_Parse_Rejects_Invalid(string input)
        => Assert.That(() => PythonVersionRequest.Parse(input), Throws.InstanceOf<Exception>());

    [Test]
    public void PlatformTriple_Current_Matches_KnownFormat()
        => Assert.That(PlatformTriple.Current.Value, Does.Match("^(x86_64|aarch64)-(pc-windows-msvc|apple-darwin|unknown-linux-gnu)$"));
}
