using System.IO;
using Flax.Build;
using Flax.Build.NativeCpp;

public class WsSourceControl : GameEditorModule
{
    /// <inheritdoc />
    public override void Setup(BuildOptions options)
    {
        base.Setup(options);

        BuildNativeCode = false;

        var thirdParty = Path.Combine(FolderPath, "..", "..", "ThirdParty", "LibGit2Sharp");
        options.ScriptingAPI.FileReferences.Add(Path.Combine(thirdParty, "net8.0", "LibGit2Sharp.dll"));
        options.DependencyFiles.Add(Path.Combine(thirdParty, "net8.0", "LibGit2Sharp.dll.config"));
        options.DependencyFiles.Add(Path.Combine(thirdParty, "runtimes", "linux-x64", "native", "libgit2-3f4182d.so"));
        options.DependencyFiles.Add(Path.Combine(thirdParty, "runtimes", "win-x64", "native", "git2-3f4182d.dll"));
    }
}
