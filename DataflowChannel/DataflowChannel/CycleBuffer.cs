// Maksim Burtsev https://github.com/MBurtsev
// Licensed under the MIT license.

namespace DataflowChannel
{
    internal sealed class CycleBuffer<T>
    {
        public CycleBuffer(int capacity)
        {
            var seg = new CycleBufferSegment<T>(capacity);

            Head   = seg;
            Reader = seg;
            Writer = seg;
        }

        // head segment
        public CycleBufferSegment<T> Head;
        private long _empty00;
        private long _empty01;
        private long _empty02;
        private long _empty03;
        private long _empty04;
        private long _empty05;
        private long _empty06;
        private long _empty07;

        // current reader segment
        public CycleBufferSegment<T> Reader;
        private long _empty08;
        private long _empty09;
        private long _empty10;
        private long _empty11;
        private long _empty12;
        private long _empty13;
        private long _empty14;
        private long _empty15;

        // current writer segment
        public CycleBufferSegment<T> Writer;
        private long _empty16;
        private long _empty17;
        private long _empty18;
        private long _empty19;
        private long _empty20;
        private long _empty21;
        private long _empty22;
        private long _empty23;
    }
}