namespace TagTraitSystem.Runtime.Core
{
    /// <summary>
    /// Defines how duplicate tag applications are handled.
    /// </summary>
    public enum StackPolicy
    {
        None = 0,
        MaxDuration = 1,
        Refresh = 2,
        StackCount = 3
    }
}
