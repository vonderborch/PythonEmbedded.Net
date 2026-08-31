using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using PythonEmbedded.Net;
using PythonEmbedded.Net.Exceptions;
using PythonEmbedded.Net.Extensibility;
using PythonEmbedded.Net.Internals;
using PythonEmbedded.Net.Models;
using PythonEmbedded.Net.Sources.SourceBuild;
using PythonEmbedded.Net.Sources.SourceBuild.Internals;
using PythonEmbedded.Net.Test;

namespace PythonEmbedded.Net.IntegrationTest;

/// <summary>
/// Offline tests for the source-build satellite: version resolution against canned python.org indexes,
/// and the command lines the builders compose. The one test that actually compiles CPython is opt-in.
/// </summary>
[TestFixture]
public class SourceBuildTests
{
    /// <summary>The shape python.org's <c>/ftp/python/</c> listing really has, trimmed to what matters.</summary>
    private const string TopLevelIndex = """
                                         <html><head><title>Index of /ftp/python/</title></head><body>
                                         <a href="../">../</a>
                                         <a href="2.7.18/">2.7.18/</a>                                          20-Apr-2020 13:19       -
                                         <a href="3.12.11/">3.12.11/</a>                                        03-Jun-2026 18:00       -
                                         <a href="3.13.7/">3.13.7/</a>                                          30-Aug-2026 12:00       -
                                         <a href="3.15.0/">3.15.0/</a>                                          22-Jul-2026 09:41       -
                                         <a href="doc/">doc/</a>                                                01-Jan-2020 00:00       -
                                         </body></html>
                                         """;

    /// <summary>A released version's directory: the final tarball is there.</summary>
    private const string FinalReleaseIndex = """
                                             <a href="Python-3.13.7.tgz">Python-3.13.7.tgz</a>                  30-Aug-2026 12:00    27M
                                             <a href="Python-3.13.7.tgz.asc">Python-3.13.7.tgz.asc</a>          30-Aug-2026 12:00   833
                                             <a href="Python-3.13.7.tar.xz">Python-3.13.7.tar.xz</a>            30-Aug-2026 12:00    20M
                                             """;

    /// <summary>
    /// An unreleased version's directory. This is the case that makes a second listing mandatory: the
    /// 3.15.0 directory exists and is newest, but holds only pre-releases.
    /// </summary>
    private const string PreReleaseOnlyIndex = """
                                               <a href="Python-3.15.0b4.tgz">Python-3.15.0b4.tgz</a>            22-Jul-2026 09:41    28M
                                               <a href="Python-3.15.0b4.tar.xz">Python-3.15.0b4.tar.xz</a>      22-Jul-2026 09:41    21M
                                               """;

    private sealed class MapHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, string> responses;

        public MapHandler(Dictionary<string, string> responses) => this.responses = responses;

        public List<string> Requested { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            string url = request.RequestUri!.ToString();
            Requested.Add(url);
            return Task.FromResult(responses.TryGetValue(url, out string? body)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private static SourceContext Context(TempRoot root, HttpMessageHandler handler, bool offline = false)
        => new(
            new HttpClient(handler), null, Path.Combine(root.Path, "cache"),
            PlatformTriple.Current, NullLogger.Instance, offline);

    private static MapHandler PythonOrg() => new(new Dictionary<string, string>
    {
        ["https://www.python.org/ftp/python/"] = TopLevelIndex,
        ["https://www.python.org/ftp/python/3.13.7/"] = FinalReleaseIndex,
        ["https://www.python.org/ftp/python/3.15.0/"] = PreReleaseOnlyIndex,
        ["https://www.python.org/ftp/python/3.12.11/"] =
            """<a href="Python-3.12.11.tgz">Python-3.12.11.tgz</a>                  03-Jun-2026 18:00    26M""",
    });

    private static Task<PythonOrgCatalog.Release?> ResolveAsync(SourceContext context, string request)
        => PythonOrgCatalog.ResolveAsync(
            PythonVersionRequest.Parse(request), context, TimeSpan.FromHours(1), CancellationToken.None);

    [Test]
    public async Task Resolves_The_Newest_Final_Release_For_An_Open_Request()
    {
        using TempRoot root = new();
        SourceContext context = Context(root, PythonOrg());

        PythonOrgCatalog.Release? release = await ResolveAsync(context, "latest");

        Assert.That(release, Is.Not.Null);
        Assert.Multiple(() =>
        {
            // 3.15.0 is the newest directory but has no final tarball, so 3.13.7 wins.
            Assert.That(release!.Version, Is.EqualTo(new PythonVersion(3, 13, 7)));
            Assert.That(release.TarballUri.ToString(), Is.EqualTo("https://www.python.org/ftp/python/3.13.7/Python-3.13.7.tgz"));
        });
    }

    [Test]
    public async Task An_Open_Request_Never_Picks_Up_A_Pre_Release()
    {
        using TempRoot root = new();
        MapHandler handler = PythonOrg();
        SourceContext context = Context(root, handler);

        PythonOrgCatalog.Release? release = await ResolveAsync(context, "3.15");

        Assert.Multiple(() =>
        {
            Assert.That(release, Is.Null, "3.15.0 has only a beta, and 'latest'-style requests are stable-only");
            Assert.That(handler.Requested, Does.Contain("https://www.python.org/ftp/python/3.15.0/"),
                "the release directory must be listed; its existence alone proves nothing");
        });
    }

    [Test]
    public async Task A_Pinned_Pre_Release_Is_Found_Inside_The_Final_Versions_Directory()
    {
        using TempRoot root = new();
        MapHandler handler = PythonOrg();
        SourceContext context = Context(root, handler);

        PythonOrgCatalog.Release? release = await ResolveAsync(context, "3.15.0b4");

        Assert.That(release, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(release!.Version, Is.EqualTo(new PythonVersion(3, 15, 0, "b4")));
            Assert.That(release.TarballUri.ToString(), Is.EqualTo("https://www.python.org/ftp/python/3.15.0/Python-3.15.0b4.tgz"));
            Assert.That(handler.Requested, Does.Not.Contain("https://www.python.org/ftp/python/"),
                "a fully-pinned version names its own directory, so the top-level index is unnecessary");
        });
    }

    [Test]
    public async Task An_Unknown_Version_Resolves_To_Null_Rather_Than_Throwing()
    {
        using TempRoot root = new();
        SourceContext context = Context(root, PythonOrg());

        Assert.That(await ResolveAsync(context, "3.9.99"), Is.Null);
    }

    [Test]
    public async Task Offline_Without_Cache_Yields_To_The_Next_Source()
    {
        using TempRoot root = new();
        SourceContext context = Context(root, PythonOrg(), offline: true);

        PythonInstallInfo? info = await new SourceBuildSource().TryInstallAsync(
            PythonVersionRequest.Parse("3.13"), Path.Combine(root.Path, "staging"), context, null, CancellationToken.None);

        Assert.That(info, Is.Null, "an offline metadata failure must not break the source chain");
    }

    [Test]
    public void Default_Configure_Arguments_Stage_The_Install_At_Slash_Install()
    {
        IReadOnlyList<string> arguments = PosixBuilder.ComposeConfigureArguments(
            Options(new SourceBuildSource()), ToolchainEnvironment.Empty);

        Assert.Multiple(() =>
        {
            // The whole relocation strategy rests on these two: /install is what SysconfigPatcher rewrites.
            Assert.That(arguments, Does.Contain("--prefix=/install"));
            Assert.That(arguments, Does.Contain("--with-ensurepip=no"));
            Assert.That(arguments, Does.Contain("--enable-shared"));
            Assert.That(arguments, Does.Contain("--enable-optimizations"));
            Assert.That(arguments, Does.Contain("--with-lto"));
            Assert.That(arguments, Does.Not.Contain("--disable-gil"));
        });
    }

    [Test]
    public void Configure_Arguments_Follow_The_Option_Matrix()
    {
        IReadOnlyList<string> plain = PosixBuilder.ComposeConfigureArguments(
            Options(new SourceBuildSource { Optimize = false, Lto = false, Shared = false }),
            ToolchainEnvironment.Empty);

        IReadOnlyList<string> freeThreaded = PosixBuilder.ComposeConfigureArguments(
            Options(new SourceBuildSource { FreeThreaded = true, ConfigureArguments = ["--with-pydebug"] }),
            ToolchainEnvironment.Empty with { OpenSslPrefix = "/opt/openssl" });

        Assert.Multiple(() =>
        {
            Assert.That(plain, Is.EquivalentTo(new[] { "--prefix=/install", "--with-ensurepip=no" }));
            Assert.That(freeThreaded, Does.Contain("--disable-gil"));
            Assert.That(freeThreaded, Does.Contain("--with-openssl=/opt/openssl"));
            Assert.That(freeThreaded, Does.Contain("--with-openssl-rpath=auto"));
            Assert.That(freeThreaded[^1], Is.EqualTo("--with-pydebug"), "caller arguments come last so they win");
        });
    }

    [Test]
    public void Shared_Builds_Get_A_Relative_Runpath_And_Callers_Can_Override_Any_Variable()
    {
        Dictionary<string, string> shared = PosixBuilder.ComposeEnvironment(
            Options(new SourceBuildSource()), ToolchainEnvironment.Empty);

        Dictionary<string, string> overridden = PosixBuilder.ComposeEnvironment(
            Options(new SourceBuildSource { BuildEnvironment = new Dictionary<string, string> { ["LDFLAGS"] = "-L/custom" } }),
            ToolchainEnvironment.Empty);

        Dictionary<string, string> statically = PosixBuilder.ComposeEnvironment(
            Options(new SourceBuildSource { Shared = false }), ToolchainEnvironment.Empty);

        Assert.Multiple(() =>
        {
            // Relative, because the tree is moved out of staging after the build.
            Assert.That(shared["LDFLAGS"], Does.Contain(OperatingSystem.IsMacOS() ? "@loader_path/../lib" : "$$ORIGIN/../lib"));
            Assert.That(overridden["LDFLAGS"], Is.EqualTo("-L/custom"));
            Assert.That(statically.ContainsKey("LDFLAGS"), Is.False);
        });
    }

    [Test]
    public void Conda_Forge_Provisioning_Points_The_Build_At_Its_Prefix()
    {
        ToolchainEnvironment toolchain = PosixToolchain.CondaForgeEnvironment("/root/tools/build-deps-3.13");

        Assert.Multiple(() =>
        {
            Assert.That(toolchain.CppFlags, Does.Contain("-I/root/tools/build-deps-3.13/include"));
            // Absolute on purpose: the built interpreter keeps loading these libraries from here.
            Assert.That(toolchain.LdFlags, Does.Contain("-Wl,-rpath,/root/tools/build-deps-3.13/lib"));
            Assert.That(toolchain.DependencyPrefix, Is.EqualTo("/root/tools/build-deps-3.13"));
            Assert.That(toolchain.Variables["PATH"], Does.StartWith("/root/tools/build-deps-3.13/bin"));
        });
    }

    [Test]
    public void Variant_Builds_Are_Named_Apart_So_Their_Installs_Do_Not_Collide()
    {
        Assert.Multiple(() =>
        {
            Assert.That(new SourceBuildSource().Name, Is.EqualTo("source-build"));
            Assert.That(new SourceBuildSource { FreeThreaded = true }.Name, Is.EqualTo("source-build-ft"));
            Assert.That(new SourceBuildSource { Name = "py-debug" }.Name, Is.EqualTo("py-debug"));
        });
    }

    [Test]
    public void Differently_Configured_Builds_Get_Their_Own_Build_Directories()
    {
        string optimized = Options(new SourceBuildSource()).Fingerprint;
        string plain = Options(new SourceBuildSource { Optimize = false }).Fingerprint;
        string sameAsOptimized = Options(new SourceBuildSource()).Fingerprint;

        Assert.Multiple(() =>
        {
            Assert.That(plain, Is.Not.EqualTo(optimized));
            Assert.That(sameAsOptimized, Is.EqualTo(optimized), "the fingerprint must be stable across runs");
        });
    }

    [Test]
    public void A_Missing_Toolchain_Names_The_Command_That_Fixes_It()
    {
        string command = DependencyProvisioner.ManualInstallCommand();

        Assert.That(command, Is.Not.Empty);
        if (OperatingSystem.IsMacOS())
        {
            Assert.That(command, Does.StartWith("brew install openssl@3"));
        }
        else if (OperatingSystem.IsWindows())
        {
            Assert.That(command, Does.Contain("Microsoft.VisualStudio.2022.BuildTools"));
        }
    }

    [Test]
    public void Module_Verification_Separates_Fatal_Gaps_From_Merely_Unfortunate_Ones()
    {
        Assert.Multiple(() =>
        {
            // No ssl means no pip install, so it can never be a warning.
            Assert.That(ModuleVerifier.CriticalModules, Does.Contain("ssl"));
            Assert.That(ModuleVerifier.CriticalModules, Does.Contain("ensurepip"));
            Assert.That(ModuleVerifier.OptionalModules, Does.Contain("lzma"));
            Assert.That(ModuleVerifier.OptionalModules.Intersect(ModuleVerifier.CriticalModules), Is.Empty);
            if (OperatingSystem.IsWindows())
            {
                Assert.That(ModuleVerifier.OptionalModules, Does.Not.Contain("readline"), "readline is POSIX-only");
            }
        });
    }

    [Test]
    public void Free_Threaded_Builds_Are_Rejected_Before_3_13()
    {
        using TempRoot root = new();
        SourceContext context = Context(root, PythonOrg());

        PythonException ex = Assert.ThrowsAsync<PythonException>(() =>
            new SourceBuildSource { FreeThreaded = true }.TryInstallAsync(
                PythonVersionRequest.Parse("3.12.11"), Path.Combine(root.Path, "staging"), context, null, CancellationToken.None))!;

        Assert.That(ex.Kind, Is.EqualTo(PythonErrorKind.UnsupportedPlatform));
    }

    /// <summary>
    /// The real thing, end to end. Compiling CPython takes many minutes even unoptimized, so this only
    /// runs when explicitly asked for: <c>PYEMBED_TEST_SOURCE_BUILD=1</c>.
    /// </summary>
    [Test]
    [Category("RequiresNetwork")]
    public async Task Compiles_And_Runs_A_Real_Interpreter()
    {
        if (Environment.GetEnvironmentVariable("PYEMBED_TEST_SOURCE_BUILD") != "1")
        {
            Assert.Ignore("Set PYEMBED_TEST_SOURCE_BUILD=1 to run the full source build (slow).");
        }

        using TempRoot root = new();
        PythonOptions options = new() { RootDirectory = root.Path };
        options.Sources.Clear();
        options.Sources.Add(new SourceBuildSource
        {
            Optimize = false,
            Lto = false,
            ProvisionDependencies = true,
        });

        PythonHost host = new(options);
        PythonVirtualEnvironment env = await host.GetEnvironmentAsync("3.13", "default", CancellationToken.None);
        PythonResult result = await env.RunCodeAsync("import ssl, sys; print(sys.version_info[:2])");

        // Proves --with-ensurepip=no is safe: the base install ships no pip, but 'python -m venv' seeded
        // one into this environment from the stdlib's ensurepip, and it can reach PyPI over the built ssl.
        await env.Packages.InstallAsync(new PackageRequest { Packages = ["six"] });
        PythonResult imported = await env.RunCodeAsync("import six; print(six.__version__)");

        DiagnosticsResult diagnostics = await env.Installation.DiagnoseAsync();

        Assert.Multiple(() =>
        {
            Assert.That(env.Installation.SourceName, Is.EqualTo("source-build"));
            Assert.That(result.StandardOutput.Trim(), Is.EqualTo("(3, 13)"));
            Assert.That(imported.StandardOutput.Trim(), Is.Not.Empty);
            Assert.That(diagnostics.IsHealthy, Is.True, string.Join("; ", diagnostics.Findings.Select(f => f.Message)));
        });
    }

    /// <summary>The options snapshot a build runs against, as <see cref="SourceBuildSource"/> composes it.</summary>
    private static BuildOptions Options(SourceBuildSource source) => new(
        source.Name, source.Optimize, source.Lto, source.Shared, source.FreeThreaded, source.JobCount,
        source.ProvisionDependencies, source.AllowElevation, source.ConfigureArguments, source.BuildEnvironment,
        source.BuildTimeout, source.KeepBuildDirectory);
}
