using Flax.Build;

public class WsVersionControlTarget : GameProjectTarget
{
    /// <inheritdoc />
    public override void Init()
    {
        base.Init();

        // Reference the modules for game
        Modules.Add("WsVersionControl");
    }
}
