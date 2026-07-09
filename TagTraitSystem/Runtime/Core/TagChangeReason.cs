namespace TagTraitSystem.Runtime.Core
{
    /// <summary>
    /// Describes why a tag state changed.
    /// </summary>
    public enum TagChangeReason
    {
        Added = 0,
        Removed = 1,
        DurationRefreshed = 2,
        DurationExtendedToMax = 3,
        StackIncreased = 4,
        StackDecreased = 5,
        ChangedToPermanent = 6,
        Expired = 7
    }
}
