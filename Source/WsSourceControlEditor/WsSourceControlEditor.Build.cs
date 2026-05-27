using Flax.Build;
using Flax.Build.NativeCpp;

public class WsSourceControlEditor : GameEditorModule
{
    /// <inheritdoc />
    public override void Setup(BuildOptions options)
    {
        base.Setup(options);

        // Reference game source module to access game code types
        options.PublicDependencies.Add("WsSourceControl");

        BuildNativeCode = false;
    }
}
