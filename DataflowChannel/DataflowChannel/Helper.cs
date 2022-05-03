// Maksim Burtsev https://github.com/MBurtsev
// Licensed under the MIT license.

namespace DataflowChannel
{
    internal class Helper
    {
    }

    /// <summary>
    ///     A size greater than or equal to the size of the most common CPU cache lines.
    /// </summary>
    internal static class PaddingHelpers
    {
#if TARGET_ARM64
        internal const int CACHE_LINE_SIZE = 128;
#elif TARGET_32BIT
        internal const int CACHE_LINE_SIZE = 32;
#else
        internal const int CACHE_LINE_SIZE = 64;
#endif
    }
}
