#tool "nuget:?package=coverlet.console&version=6.0.4"
#tool "nuget:?package=GitVersion.Tool&version=6.4.0"

using System.Diagnostics;
using System.Text;
using System.Text.Json;

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var nugetSource = Argument("nugetSource", "https://api.nuget.org/v3/index.json");

var solution = "./stdray.Twitter.sln";
var libraryProject = "./stdray.Twitter/stdray.Twitter.csproj";
var testsProject = "./stdray.Twitter.Tests/stdray.Twitter.Tests.csproj";
var artifactsDir = "./artifacts";
var testResultsDir = "./artifacts/test-results";

var packageName = "stdray.Twitter";
var packageDescription = "A C# library for retrieving Twitter/X.com tweet content by ID";
var packageAuthors = new[] { "stdray" };
var packageOwners = new[] { "stdray" };
var projectUrl = "https://github.com/stdray/stdray-twitter";
var repositoryUrl = "https://github.com/stdray/stdray-twitter";
var repositoryType = "git";
var tags = new[] { "twitter", "x.com", "tweets", "social-media", "api" };
var packageLicenseExpression = "MIT";

GitVersionResult gitVersion = null;

string RequireGitVersionField(string value, string propertyName)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        throw new CakeException($"GitVersion did not provide required value '{propertyName}'.");
    }

    return value;
}

DotNetMSBuildSettings CreateVersionMsBuildSettings(GitVersionResult versionInfo)
{
    var versionValue = RequireGitVersionField(versionInfo.FullSemVer, nameof(versionInfo.FullSemVer));
    var informationalVersion = RequireGitVersionField(versionInfo.InformationalVersion, nameof(versionInfo.InformationalVersion));
    var assemblyVersion = RequireGitVersionField(versionInfo.AssemblySemVer, nameof(versionInfo.AssemblySemVer));
    var fileVersion = RequireGitVersionField(versionInfo.AssemblySemFileVer, nameof(versionInfo.AssemblySemFileVer));
    var shortSha = RequireGitVersionField(versionInfo.ShortSha, nameof(versionInfo.ShortSha));
    var commitDate = RequireGitVersionField(versionInfo.CommitDate, nameof(versionInfo.CommitDate));

    return new DotNetMSBuildSettings()
        .WithProperty("Version", versionValue)
        .WithProperty("PackageVersion", versionValue)
        .WithProperty("InformationalVersion", informationalVersion)
        .WithProperty("AssemblyVersion", assemblyVersion)
        .WithProperty("FileVersion", fileVersion)
        .WithProperty("GitShortSha", shortSha)
        .WithProperty("GitCommitDate", commitDate);
}

void UpdateGithubStepSummary(string versionValue)
{
    var summaryPath = EnvironmentVariable("GITHUB_STEP_SUMMARY");

    if (string.IsNullOrWhiteSpace(summaryPath))
    {
        return;
    }

    var builder = new StringBuilder()
        .AppendLine("## NuGet Package Version")
        .AppendLine()
        .AppendLine($"- Version: `{versionValue}`")
        .AppendLine();

    System.IO.File.AppendAllText(summaryPath, builder.ToString());
}

GitVersionResult ResolveGitVersion()
{
    var psi = new ProcessStartInfo
    {
        FileName = "dotnet",
        Arguments = "gitversion /output json /nofetch",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        WorkingDirectory = System.IO.Directory.GetCurrentDirectory()
    };

    using var process = Process.Start(psi);
    var stdout = process.StandardOutput.ReadToEnd();
    var stderr = process.StandardError.ReadToEnd();
    process.WaitForExit();

    if (process.ExitCode != 0)
    {
        throw new CakeException($"GitVersion failed with exit code {process.ExitCode}: {stderr}");
    }

    var options = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    var result = JsonSerializer.Deserialize<GitVersionResult>(stdout, options);

    if (result == null)
    {
        throw new CakeException("Unable to deserialize GitVersion output.");
    }

    return result;
}

class GitVersionResult
{
    public string FullSemVer { get; set; }
    public string InformationalVersion { get; set; }
    public string ShortSha { get; set; }
    public string CommitDate { get; set; }
    public string AssemblySemVer { get; set; }
    public string AssemblySemFileVer { get; set; }
}

DotNetBuildSettings CreateVersionedBuildSettings(GitVersionResult versionInfo) =>
    new()
    {
        Configuration = configuration,
        NoRestore = true,
        MSBuildSettings = CreateVersionMsBuildSettings(versionInfo)
    };

Task("Clean")
    .Does(() =>
{
    if (DirectoryExists(artifactsDir))
    {
        DeleteDirectory(artifactsDir, new DeleteDirectorySettings { Recursive = true, Force = true });
    }

    DotNetClean(libraryProject, new DotNetCleanSettings
    {
        Configuration = configuration
    });

    DotNetClean(testsProject, new DotNetCleanSettings
    {
        Configuration = configuration
    });
});

Task("Restore")
    .IsDependentOn("Clean")
    .Does(() =>
{
    DotNetRestore(libraryProject);
    DotNetRestore(testsProject);
});

Task("Version")
    .IsDependentOn("Restore")
    .Does(() =>
{
    gitVersion = ResolveGitVersion();

    Information("GitVersion FullSemVer: {0}", gitVersion.FullSemVer);
    Information("GitVersion InformationalVersion: {0}", gitVersion.InformationalVersion);
    Information("GitVersion ShortSha: {0}", gitVersion.ShortSha);
});

Task("Build")
    .IsDependentOn("Version")
    .Does(() =>
{
    DotNetBuild(libraryProject, CreateVersionedBuildSettings(gitVersion));
    DotNetBuild(testsProject, CreateVersionedBuildSettings(gitVersion));
});

Task("Test")
    .IsDependentOn("Build")
    .Does(() =>
{
    EnsureDirectoryExists(testResultsDir);

    DotNetTest(testsProject, new DotNetTestSettings
    {
        Configuration = configuration,
        NoBuild = true,
        NoRestore = true,
        Collectors = new[] { "XPlat Code Coverage" },
        Loggers = new[] { "trx" },
        ResultsDirectory = testResultsDir
    });
});

Task("Pack")
    .IsDependentOn("Test")
    .Does(() =>
{
    EnsureDirectoryExists(artifactsDir);

    UpdateGithubStepSummary(gitVersion.FullSemVer);

    var packSettings = new DotNetPackSettings
    {
        Configuration = configuration,
        OutputDirectory = artifactsDir,
        NoBuild = true,
        NoRestore = true,
        IncludeSource = false,
        IncludeSymbols = true,
        SymbolPackageFormat = "snupkg",
        MSBuildSettings = CreateVersionMsBuildSettings(gitVersion)
            .WithProperty("PackageId", packageName)
            .WithProperty("PackageDescription", packageDescription)
            .WithProperty("Authors", string.Join(",", packageAuthors))
            .WithProperty("Company", string.Join(",", packageOwners))
            .WithProperty("PackageProjectUrl", projectUrl)
            .WithProperty("RepositoryUrl", repositoryUrl)
            .WithProperty("RepositoryType", repositoryType)
            .WithProperty("PackageTags", string.Join(" ", tags))
            .WithProperty("PackageLicenseExpression", packageLicenseExpression)
    };

    DotNetPack(libraryProject, packSettings);
});

Task("NuGetPush")
    .IsDependentOn("Pack")
    .Does(() =>
{
    var apiKey = EnvironmentVariable("NUGET_API_KEY");

    if (string.IsNullOrWhiteSpace(apiKey))
    {
        Warning("NUGET_API_KEY environment variable is not set. Skipping package publishing.");
        return;
    }

    var packages = GetFiles("./artifacts/*.nupkg");

    foreach (var package in packages)
    {
        DotNetNuGetPush(package, new DotNetNuGetPushSettings
        {
            Source = nugetSource,
            ApiKey = apiKey
        });

        Information("Published package {0}", package.GetFilename());
    }
});

Task("Default")
    .IsDependentOn("NuGetPush");

Task("CI")
    .IsDependentOn("NuGetPush");

RunTarget(target);