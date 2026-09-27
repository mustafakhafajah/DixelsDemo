namespace Dixels.Portal.Cqrs;

/* "No result" for commands such as delete, so every handler can share one signature. */
public readonly struct Unit
{
    public static readonly Unit Value = new();
}
