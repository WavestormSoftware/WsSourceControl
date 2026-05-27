using Flax.Build;

public class WsSourceControlEditorTarget : GameProjectEditorTarget
{
    /// <inheritdoc />
    public override void Init()
    {
        base.Init();

        // Reference the modules for editor
        Modules.Add("WsSourceControl");
        Modules.Add("WsSourceControlEditor");
    }
}
