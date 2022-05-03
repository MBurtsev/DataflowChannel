using System.Runtime.InteropServices;

namespace DataflowChannel
{
    internal sealed class CycleBufferSegment<T>
    {
        public CycleBufferSegment(int capacity)
        {
            ReaderMessages = new T[capacity];
            WriterMessages = ReaderMessages;
        }

        // Reading thread position
        public int ReaderPosition;
        private long _empty00; // To prevent thread friction on the databus.
        private long _empty01; // Improves performance x2
        private long _empty02;
        private long _empty03;
        private long _empty04;
        private long _empty05;
        private long _empty06;
        private long _empty07;

        public T[] ReaderMessages;
        private long _empty08;
        private long _empty09;
        private long _empty10;
        private long _empty11;
        private long _empty12;
        private long _empty13;
        private long _empty14;
        private long _empty15;

        // Writing thread position
        public int WriterPosition;
        private long _empty16;
        private long _empty17;
        private long _empty18;
        private long _empty19;
        private long _empty20;
        private long _empty21;
        private long _empty22;
        private long _empty23;

        public T[] WriterMessages;
        private long _empty24;
        private long _empty25;
        private long _empty26;
        private long _empty27;
        private long _empty28;
        private long _empty29;
        private long _empty30;
        private long _empty31;

        // Next segment
        public CycleBufferSegment<T> Next;

        public override string ToString()
        {
            return this.GetHashCode().ToString();
        }
    }
}