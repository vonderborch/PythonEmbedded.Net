using PythonEmbedded.Net;

// Manual playground: point the library at the test fixture archives and exercise the happy path.
// Run test/tools/fetch-fixtures.sh first.

string repoRoot = AppContext.BaseDirectory;
while (!Directory.Exists(Path.Combine(repoRoot, "test", "fixtures")))
{
    repoRoot = Path.GetDirectoryName(repoRoot)
        ?? throw new InvalidOperationException("test/fixtures not found — run test/tools/fetch-fixtures.sh");
}

PythonEnvironment.Configure(o =>
{
    o.RootDirectory = Path.Combine(Path.GetTempPath(), "pyembed-devtest");
    o.AddDirectorySource(Path.Combine(repoRoot, "test", "fixtures"), "fixtures");
});

PythonVirtualEnvironment env = await PythonEnvironment.GetEnvironmentAsync("3.13", "devtest");
Console.WriteLine($"environment: {env}");

PythonResult result = await env.RunCodeAsync("import sys; print(f'hello from {sys.version}')");
Console.Write(result.StandardOutput);

foreach (InstalledPackage package in await env.Packages.ListAsync())
{
    Console.WriteLine($"  {package.Name} {package.Version}");
}
