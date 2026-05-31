using Flax.Build;

public class WsSourceControlTarget : GameProjectTarget
{
    /// <inheritdoc />
    public override void Init()
    {
        base.Init();

        // WsSourceControl is editor-only, so no modules are added for game builds
    }
}
