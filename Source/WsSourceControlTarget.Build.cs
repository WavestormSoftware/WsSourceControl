using Flax.Build;

public class WsSourceControlTarget : GameProjectTarget
{
    /// <inheritdoc />
    public override void Init()
    {
        base.Init();

        // Reference the modules for game
        Modules.Add("WsSourceControl");
    }
}
