using Flax.Build;

public class WsVersionControlEditorTarget : GameProjectEditorTarget
{
    /// <inheritdoc />
    public override void Init()
    {
        base.Init();

        // Reference the modules for editor
        Modules.Add("WsVersionControl");
        Modules.Add("WsVersionControlEditor");
    }
}
