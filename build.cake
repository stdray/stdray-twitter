#tool "nuget:?package=coverlet.console&version=6.0.4"

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var verbosity = Argument("verbosity", Verbosity.Normal);

// Package information - will be overridden by GitVersion if available
var version = "1.0.0";
var informationalVersion = "1.0.0";

// Try to get version from GitVersion
try
{
    var gitVersionInfo = GitVersion(new GitVersionSettings
    {
        OutputType = GitVersionOutput.Json
    });

    version = gitVersionInfo.LegacySemVerPadded;
    informationalVersion = gitVersionInfo.InformationalVersion;
}
catch
{
    Warning("GitVersion not available, using default version");
}

// Package information
var packageName = "stdray.Twitter";
var packageDescription = "A C# library for retrieving Twitter/X.com tweet content by ID";
var packageAuthors = new[] { "stdray" };
var packageOwners = new[] { "stdray" };
var projectUrl = "https://github.com/stdray/test-twi";
var copyright = $"Copyright (c) {DateTime.Now.Year} stdray";
var tags = new[] { "twitter", "x.com", "tweets", "social-media", "api" };
var repositoryUrl = "https://github.com/stdray/test-twi";
var repositoryType = "git";

var artifactsDir = "./artifacts";
var solution = "./stdray.Twitter.sln";
var projectsToTest = new[] { "./stdray.Twitter.Tests" };

Task("Clean")
    .Description("Cleans the build artifacts")
    .Does(() =>
    {
        if (DirectoryExists(artifactsDir))
        {
            DeleteDirectory(artifactsDir, new DeleteDirectorySettings { Recursive = true, Force = true });
        }
    });

Task("Restore")
    .Description("Restores NuGet packages")
    .Does(() =>
    {
        DotNetRestore(solution);
    });

Task("Version")
    .Description("Displays version information")
    .Does(() =>
    {
        Information($"Calculated version: {version}");
        Information($"Informational version: {informationalVersion}");
    });

Task("Build")
    .IsDependentOn("Clean")
    .IsDependentOn("Restore")
    .IsDependentOn("Version")
    .Description("Builds the solution")
    .Does(() =>
    {
        var buildSettings = new DotNetBuildSettings
        {
            Configuration = configuration,
            NoRestore = true,
            MSBuildSettings = new DotNetMSBuildSettings()
                .SetVersion(version)
                .SetAssemblyVersion(version)
                .SetFileVersion(version)
                .SetInformationalVersion(informationalVersion)
        };

        DotNetBuild(solution, buildSettings);
    });

Task("Test")
    .IsDependentOn("Build")
    .Description("Runs unit tests and collects coverage")
    .Does(() =>
    {
        foreach (var project in projectsToTest)
        {
            var testSettings = new DotNetTestSettings
            {
                Configuration = configuration,
                NoBuild = true,
                NoRestore = true,
                Collectors = new[] { "XPlat Code Coverage" },
                Loggers = new[] { "trx" },
                ResultsDirectory = "./artifacts/test-results"
            };

            DotNetTest(project, testSettings);
        }
    });

Task("Pack")
    .IsDependentOn("Test")
    .Description("Creates NuGet packages")
    .Does(() =>
    {
        var packSettings = new DotNetPackSettings
        {
            Configuration = configuration,
            OutputDirectory = "./artifacts",
            NoBuild = true,
            NoRestore = true,
            IncludeSource = false,
            IncludeSymbols = true,
            SymbolPackageFormat = "snupkg",
            MSBuildSettings = new DotNetMSBuildSettings()
                .SetVersion(version)
                .SetAssemblyVersion(version)
                .SetFileVersion(version)
                .SetInformationalVersion(informationalVersion)
                .WithProperty("PackageId", packageName)
                .WithProperty("PackageDescription", packageDescription)
                .WithProperty("Authors", string.Join(",", packageAuthors))
                .WithProperty("Company", string.Join(",", packageOwners))
                .WithProperty("Copyright", copyright)
                .WithProperty("PackageProjectUrl", projectUrl)
                .WithProperty("RepositoryUrl", repositoryUrl)
                .WithProperty("RepositoryType", repositoryType)
                .WithProperty("PackageTags", string.Join(" ", tags))
                .WithProperty("PackageLicenseExpression", "MIT") // Default license, can be changed
        };

        DotNetPack("./stdray.Twitter/stdray.Twitter.csproj", packSettings);
    });

Task("Publish")
    .IsDependentOn("Pack")
    .Description("Publishes the NuGet package")
    .Does(() =>
    {
        var packageFiles = GetFiles("./artifacts/*.nupkg");

        foreach (var packageFile in packageFiles)
        {
            var apiKey = EnvironmentVariable("NUGET_API_KEY");

            if (string.IsNullOrEmpty(apiKey))
            {
                Warning("NUGET_API_KEY environment variable is not set. Skipping package publishing.");
                return;
            }

            try
            {
                DotNetNuGetPush(packageFile, new DotNetNuGetPushSettings
                {
                    Source = "https://api.nuget.org/v3/index.json",
                    ApiKey = apiKey
                });

                Information($"Successfully published package: {packageFile}");
            }
            catch (Exception ex)
            {
                Error($"Failed to publish package: {ex.Message}");
                throw;
            }
        }
    });

Task("Default")
    .IsDependentOn("Test")
    .Description("Default task that builds and runs tests");

Task("CI")
    .IsDependentOn("Publish")
    .Description("CI task that builds, tests, packages, and publishes");

RunTarget(target);