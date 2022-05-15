// Maksim Burtsev https://github.com/MBurtsev
// Licensed under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace DataflowChannel_B0
{
    /// <summary>
    /// MPOC - Multiple Producer Multiple Consumer.
    /// At the core a cycle buffer that implements a producer-consumer pattern. 
    /// Producers use spin lock for initialization thread only once. All
    /// write operations fully lock-free\wait-free.
    /// Customer fully lock-free\wait-free.
    /// No order means that read order is not equal to write order.
    /// </summary>
    public partial class ChannelMPMC<T>
    {
        private const int THREADS_STORAGE_SIZE = 64;
        // The default value that is used if the user has not specified a capacity.
        private const int DEFAULT_CAPACITY = 32 * 1024;
        private const int DATA_CAPACITY = 1024;
        // Current segment size
        private readonly int _capacity;
        private ChannelData _channel;
        private int _val;

        public ChannelMPMC() : this(DEFAULT_CAPACITY)
        { 
        }

        public ChannelMPMC(int capacity)
        {
            _capacity = capacity;
            _channel = new ChannelData(capacity);

            _channel.WriterOperation = 7893022;
        }

        public bool IsEmpty
        {
            get
            {


                return true;
            }
        }

        public int Count
        {
            get
            {
                var count = 0;

                return count;
            }
        }

        public int HZ
        {
            get
            {
                _channel.WriterOperation++;

                return _channel.WriterOperation;
            }
        }

        public void Write(T value)
        {
            
            unchecked
            {
                var channel = _channel;

                // 7s
                var operation = Interlocked.Add(ref channel.WriterOperation, 1);

                // 3.6s
                //var operation = ++channel.WriterOperation;

                // ~1s
                //var operation = channel.WriterOperation;
                //Volatile.Write(ref channel.WriterOperation, operation + 1);

                //channel.WriterOperation = operation + 1;
                //Interlocked.Add(ref channel.WriterOperation, 1);


                var data = channel.Storage[operation % DATA_CAPACITY];

                var seg = data.Writer;

                //if (seg.WriterPosition == _capacity)
                //{
                //    CycleBufferSegmentOPOC<T> next;

                //    var flag = seg.Next == null;

                //    if (!flag && seg.Next != data.Reader)
                //    {
                //        next = seg.Next;
                //    }
                //    else if (flag && data.Head != data.Reader)
                //    {
                //        next = data.Head;
                //    }
                //    else
                //    {
                //        next = new CycleBufferSegmentOPOC<T>(_capacity)
                //        {
                //            Next = seg.Next
                //        };

                //        seg.Next = next;
                //    }

                //    next.WriterMessages[0] = value;
                //    next.WriterPosition = 1;

                //    data.Writer = next;

                //    return;
                //}

                seg.WriterMessages[seg.WriterPosition] = value;
                //seg.WriterPosition++;
            }
        }

        public bool TryRead([MaybeNullWhen(false)] out T value)
        {
            //unchecked
            //{
            //    var channel = _channel;
            //    var start   = channel.Reader;
            //    var cur     = start;

            //    if (cur == null)
            //    {
            //        value = default;

            //        return false;
            //    }

            //    do
            //    {
            //        var seg = cur.Data.Reader;

            //        if (seg.ReaderPosition == _capacity)
            //        {
            //            if (seg == cur.Data.Writer)
            //            {
            //                goto proceed;
            //            }

            //            CycleBufferSegmentMPOC next;

            //            if (seg.Next != null)
            //            {
            //                next = seg.Next;
            //            }
            //            else
            //            {
            //                next = cur.Data.Head;
            //            }

            //            next.ReaderPosition = 0;

            //            seg = next;

            //            cur.Data.Reader = seg;
            //        }

            //        // reader position check
            //        if (seg.ReaderPosition != seg.WriterPosition)
            //        {
            //            value = seg.ReaderMessages[seg.ReaderPosition];

            //            seg.ReaderPosition++;

            //            return true;
            //        }

            //    proceed: 
            //        cur = channel.Reader = cur.Next;

            //        if (cur == null)
            //        {
            //            cur = channel.Reader = channel.Head;
            //        }
            //    } 
            //    while (cur != start);

            value = default;

            return false;
            //}
        }

        public void Clear()
        {

        }

        #region ' Structures '

        private sealed class ChannelData
        {
            public ChannelData(int capacity)
            {
                Storage = new CycleBufferOPOC<T>[DATA_CAPACITY];

                for (var i = 0; i < DATA_CAPACITY; i++)
                {
                    Storage[i] = new CycleBufferOPOC<T>(capacity);
                }
            }

            public readonly CycleBufferOPOC<T>[] Storage;
            private long _empty00;
            private long _empty01;
            private long _empty02;
            private long _empty03;
            private long _empty04;
            private long _empty05;
            private long _empty06;
            private long _empty07;
            // Current reader operation number
            public int ReaderOperation;
            private long _empty08;
            private long _empty09;
            private long _empty10;
            private long _empty11;
            private long _empty12;
            private long _empty13;
            private long _empty14;
            private long _empty15;
            // Current writer operation number
            public int WriterOperation;
            private long _empty16;
            private long _empty17;
            private long _empty18;
            private long _empty19;
            private long _empty20;
            private long _empty21;
            private long _empty22;
            private long _empty23;
        }

        private sealed class CycleBufferOPOC<T>
        {
            public CycleBufferOPOC(int capacity)
            {
                var seg = new CycleBufferSegmentOPOC<T>(capacity);

                Head   = seg;
                Reader = seg;
                Writer = seg;
            }

            // head segment
            public CycleBufferSegmentOPOC<T> Head;
            private long _empty00;
            private long _empty01;
            private long _empty02;
            private long _empty03;
            private long _empty04;
            private long _empty05;
            private long _empty06;
            private long _empty07;

            // current reader segment
            public CycleBufferSegmentOPOC<T> Reader;
            private long _empty08;
            private long _empty09;
            private long _empty10;
            private long _empty11;
            private long _empty12;
            private long _empty13;
            private long _empty14;
            private long _empty15;

            // current writer segment
            public CycleBufferSegmentOPOC<T> Writer;
            private long _empty16;
            private long _empty17;
            private long _empty18;
            private long _empty19;
            private long _empty20;
            private long _empty21;
            private long _empty22;
            private long _empty23;
        }

        private sealed class CycleBufferSegmentOPOC<T>
        {
            public CycleBufferSegmentOPOC(int capacity)
            {
                ReaderMessages = new T[capacity];
                WriterMessages = ReaderMessages;
            }

            // Reading thread position
            public int ReaderPosition;
            private long _empty00; // To prevent thread friction on the databus.
            private long _empty01; // To improve performance
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
            public CycleBufferSegmentOPOC<T> Next;
            private long _empty32;
            private long _empty33;
            private long _empty34;
            private long _empty35;
            private long _empty36;
            private long _empty37;
            private long _empty38;
            private long _empty39;

            public override string ToString()
            {
                return this.GetHashCode().ToString();
            }
        }

        #endregion
    }
}