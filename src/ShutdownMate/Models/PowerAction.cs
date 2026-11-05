namespace ShutdownMate.Models
{
    /// <summary>Süre dolduğunda yapılacak güç işlemi.</summary>
    public enum PowerAction
    {
        Shutdown,
        Restart,
        Sleep,
        Hibernate
    }
}
