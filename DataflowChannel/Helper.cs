// Maksim Burtsev https://github.com/MBurtsev
// Licensed under the MIT license.

using System.Runtime.InteropServices;

namespace DataflowChannel
{
    internal class Helper
    {
    }

    [StructLayout(LayoutKind.Explicit, Size = 256/*PaddingHelpers.CACHE_LINE_SIZE*/)]
    struct EmptySpace
    {
        [FieldOffset(0)]
        int _empty;
    };

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
