namespace CCS.SspNet.Tests
{
    /// <summary>
    /// Trait values that split the suite into tiers.  Apply one with
    /// <c>[Trait(TestCategories.Name, TestCategories.Fast)]</c>.
    /// </summary>
    /// <remarks>
    /// Pull request builds run everything except <see cref="Slow"/> and <see cref="Hardware"/>.
    /// An untagged test is treated as fast and runs on every build, so only the slower tiers
    /// actually need tagging — the <see cref="Fast"/> tag is there to make the intent explicit.
    /// </remarks>
    public static class TestCategories
    {
        /// <summary>The trait name carrying these values.</summary>
        public const string Name = "Category";

        /// <summary>In-memory, no I/O, milliseconds.  Runs on every build.</summary>
        public const string Fast = "Fast";

        /// <summary>Real timeouts, retries or large payloads.  Excluded from pull request builds.</summary>
        public const string Slow = "Slow";

        /// <summary>Needs a physical device on a port.  Never runs in CI.</summary>
        public const string Hardware = "Hardware";
    }
}
