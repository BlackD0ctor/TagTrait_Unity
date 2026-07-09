namespace TagTraitSystem.Runtime.Core
{
    /// <summary>
    /// Identifies a tag definition across the project.
    /// </summary>
    public enum TagID
    {
        None = 0,

        // Development and test placeholder. Production projects should add project-specific TagID values.
        // Do not reuse serialized numeric values without a migration plan.
        TestAlpha = 1,

        // Development and test placeholder. Production projects should add project-specific TagID values.
        // Do not reuse serialized numeric values without a migration plan.
        TestBeta = 2,

        // Development and test placeholder. Production projects should add project-specific TagID values.
        // Do not reuse serialized numeric values without a migration plan.
        TestGamma = 3,

        // Sample-only TagID. Replace or extend it with project-specific IDs as needed.
        SampleKeyword = 100,

        // Sample-only TagID. Replace or extend it with project-specific IDs as needed.
        SamplePermanent = 101,

        // Sample-only TagID. Replace or extend it with project-specific IDs as needed.
        SampleRefresh = 102,

        // Sample-only TagID. Replace or extend it with project-specific IDs as needed.
        SampleMaxDuration = 103,

        // Sample-only TagID. Replace or extend it with project-specific IDs as needed.
        SampleStackCount = 104
    }
}
