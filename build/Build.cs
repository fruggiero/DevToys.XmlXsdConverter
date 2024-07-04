using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Nuke.Common;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.NerdbankGitVersioning;
using Nuke.Common.Utilities.Collections;

[SuppressMessage("ReSharper", "AssignmentInsteadOfDiscard")]
[SuppressMessage("ReSharper", "AllUnderscoreLocalParameterName")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
[GitHubActions(
    "continuous",
    GitHubActionsImage.UbuntuLatest,
    On = new[] { GitHubActionsTrigger.Push },
    FetchDepth = 0,
    InvokedTargets = new[] { nameof(Compile), nameof(Pack) },
    ImportSecrets = new[] {nameof(NuGetApiKey)}
    )]
class Build : NukeBuild
{
    static AbsolutePath SourceDirectory => RootDirectory / "src";
    static AbsolutePath TestsDirectory => RootDirectory / "test";
    static AbsolutePath OutputDirectory => RootDirectory / "output";

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;
    
    [Parameter("Package description")] readonly string PackageDescription;

    [Parameter("Authors")] readonly string Authors;

    [Parameter("Solution")] readonly string Solution;

    [Parameter("ProjectUrl")] readonly string ProjectUrl;

    [Parameter("Tags")] readonly string Tags;
    [Parameter("Title")] readonly string Title;

    [Parameter][Secret] readonly string NuGetApiKey;

    [NerdbankGitVersioning]
    readonly NerdbankGitVersioning NerdbankVersioning;
    
    public static int Main () => Execute<Build>(x => x.Compile);

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            SourceDirectory.GlobDirectories("**/bin", "**/obj").ForEach(x => x.DeleteDirectory());
            TestsDirectory.GlobDirectories("**/bin", "**/obj").ForEach(x => x.DeleteDirectory());
        });

    Target CleanOutput => _ => _
        .Before(Pack)
        .Executes(() =>
        {
            OutputDirectory.CreateOrCleanDirectory();
        });

    Target Restore => _ => _
        .Executes(() =>
        {
            DotNetTasks.DotNetRestore(_ =>
            {
                _ = _.SetProjectFile(Solution);
                return _;
            });
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetTasks.DotNetBuild(_ =>
            {
                _ = _.SetProjectFile(Solution)
                    .SetConfiguration(Configuration)
                    .SetAuthors(Authors)
                    .SetAssemblyVersion(NerdbankVersioning.AssemblyVersion)
                    .SetFileVersion(NerdbankVersioning.AssemblyFileVersion)
                    .SetInformationalVersion(NerdbankVersioning.AssemblyInformationalVersion);

                return _;
            });
        });
    
    Target Test => _ => _
        .After(Compile)
        .Executes(() =>
        {
            DotNetTasks.DotNetTest(_ => _
                .SetConfiguration(Configuration)
                .SetFilter("FullyQualifiedName!~IntegrationTests")
                .EnableNoBuild());
        });
    
    Target Pack => _ => _
        .After(Compile)
        .DependsOn(CleanOutput)
        .Produces(OutputDirectory / "*.nupkg")
        .Executes(() =>
        {
            DotNetTasks.DotNetPack(_ => _
                .SetProject(Solution)
                .SetOutputDirectory(OutputDirectory)
                .SetConfiguration(Configuration)
                .SetProperties(new Dictionary<string, object>
                {
                    {"Version", NerdbankVersioning.NuGetPackageVersion},
                    {"Description", PackageDescription},
                    {"Authors", Authors},
                    {"PackageProjectUrl", ProjectUrl}
                })
                .SetTitle(Title)
                .SetPackageTags(Tags.Split(';'))
                .EnableNoBuild());
        });

    Target Publish => _ => _
        .After(Pack)
        .Executes(() =>
        {
            var path = OutputDirectory / $"{System.IO.Path.GetFileNameWithoutExtension(Solution)}.{NerdbankVersioning.NuGetPackageVersion}.nupkg";
            DotNetTasks.DotNetNuGetPush(_ => _
                .SetApiKey(NuGetApiKey)
                .SetTargetPath(path)
                .SetSource("https://api.nuget.org/v3/index.json")
            );
        });
}
