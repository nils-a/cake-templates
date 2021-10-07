#load nuget:?package=Cake.Recipe&version=2.2.1

Environment.SetVariableNames();

BuildParameters.SetParameters(
  context: Context,
  buildSystem: BuildSystem,
  sourceDirectoryPath: "./src",
  title: "Cake.Templates",
  masterBranchName: "main",
  repositoryOwner: "nils-a",
  shouldRunDotNetCorePack: false, // we ship a custom pack task
  shouldUseDeterministicBuilds: true,
  shouldRunDupFinder: false,
  shouldRunInspectCode: false,
  shouldRunCoveralls: false,
  shouldRunCodecov: false);

BuildParameters.PrintParameters(Context);

ToolSettings.SetToolSettings(context: Context);

BuildParameters.Tasks.DotNetCorePackTask = Task("DotNetTemplate-Pack")
    .IsDependentOn("DotNetCore-Build")
    .IsDependeeOf("Package")
    .Does<BuildVersion>((context, buildVersion) =>
{
    // This is a copy of DotNetCore-Pack but excludes all projects under 
    // the templates folder 

    var projects = GetFiles(BuildParameters.SourceDirectoryPath + "/**/*.csproj")
        - GetFiles(BuildParameters.SourceDirectoryPath + "/**/templates/**/*.csproj")
        - GetFiles(BuildParameters.RootDirectoryPath + "/tools/**/*.csproj")
        - GetFiles(BuildParameters.SourceDirectoryPath + "/**/*.Tests.csproj")
        - GetFiles(BuildParameters.SourceDirectoryPath + "/packages/**/*.csproj");

    // We need to clone the settings class, so we don't
    // add additional properties to every other task.
    var msBuildSettings = new DotNetCoreMSBuildSettings();
    foreach (var kv in context.Data.Get<DotNetCoreMSBuildSettings>().Properties)
    {
        string value = string.Join(" ", kv.Value);
        msBuildSettings.WithProperty(kv.Key, value);
    }

    if (BuildParameters.ShouldBuildNugetSourcePackage)
    {
        msBuildSettings.WithProperty("SymbolPackageFormat", "snupkg");
    }

    var settings = new DotNetCorePackSettings {
        NoBuild = true,
        NoRestore = true,
        Configuration = BuildParameters.Configuration,
        OutputDirectory = BuildParameters.Paths.Directories.NuGetPackages,
        MSBuildSettings = msBuildSettings,
        IncludeSource = BuildParameters.ShouldBuildNugetSourcePackage,
        IncludeSymbols = BuildParameters.ShouldBuildNugetSourcePackage,
    };

    foreach (var project in projects)
    {
        DotNetCorePack(project.ToString(), settings);
    }
});

Task("local-install")
    .Description("use this task to locally install from a build nupkg. Be aware that this might break other installed templates.")
    .IsDependentOn("DotNetTemplate-Pack")
    .Does(() => {
        // uninstall
        Information("Uninstalling previous version");
        StartProcess("dotnet", new ProcessSettings{ Arguments = "new -u Cake.Templates" });

        // cleanup
        var userProfile = EnvironmentVariable("USERPROFILE", "");
        var templatePath = Directory(userProfile) + Directory(".templateengine");
        Information($"Resetting 'dotnet new' templates. Folder: {templatePath}");
        CleanDirectory(templatePath);
        StartProcess("dotnet", new ProcessSettings{ Arguments = "new --debug:reinit" });
        
        // install
        var nupkg = GetFiles("./BuildArtifacts/Packages/NuGet/*.nupkg").FirstOrDefault();
        if(nupkg == null) {
            throw new Exception("no nupkg found. Could not install.");
        }
        Information($"Installing nupkg locally: {nupkg}");
        StartProcess("dotnet", new ProcessSettings{ Arguments = $"new -i {nupkg.FullPath}" });

        Information($"listing installed templates");
        StartProcess("dotnet", new ProcessSettings{ Arguments = "new cake --list" });
    });


Build.RunDotNetCore();