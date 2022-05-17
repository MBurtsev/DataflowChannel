// Maksim Burtsev https://github.com/MBurtsev
// Licensed under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace DataflowChannel_B11
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
        // The default value that is used if the user has not specified a capacity.
        private const int SEGMENT_CAPACITY = 32*1024;
        private const int OPERATION_CAPACITY = 1;
        // Current segment size
        private readonly int _capacity;
        private ChannelData _channel;

        public ChannelMPMC() : this(SEGMENT_CAPACITY * 8)
        { 
        }

        public ChannelMPMC(int capacity)
        {
            _capacity = capacity;
            _channel = new ChannelData(capacity);
        }

        public void Write(T value)
        {
            
            unchecked
            {
                var channel = _channel;
                //var operation = Interlocked.Add(ref channel.WriterOperation, 1);
                var operation = 2;
                ref var data = ref channel.Storage[operation % OPERATION_CAPACITY];

                Interlocked.CompareExchange(ref data.WriterSync, 0, 1);

                data.WriterSync = 0;

                var seg = data.Writer;

                if (seg.WriterPosition == _capacity)
                {
                    CycleBufferSegment next;

                    var flag = seg.Next == null;

                    if (!flag && seg.Next != data.Reader)
                    {
                        next = seg.Next;
                    }
                    else if (flag && data.Head != data.Reader)
                    {
                        next = data.Head;
                    }
                    else
                    {
                        next = new CycleBufferSegment(_capacity)
                        {
                            Next = seg.Next
                        };

                        seg.Next = next;
                    }

                    next.WriterMessages[0] = value;
                    next.WriterPosition = 1;

                    data.Writer = next;

                    return;
                }

                var pos = seg.WriterPosition;

                seg.WriterMessages[pos] = value;
                seg.WriterPosition = pos + 1;
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

        #region ' Structures '

        private sealed class ChannelData
        {
            public ChannelData(int capacity)
            {
                Storage = new CycleBuffer[OPERATION_CAPACITY];

                for (var i = 0; i < OPERATION_CAPACITY; i++)
                {
                    Storage[i] = new CycleBuffer(capacity);
                }
            }

            public readonly CycleBuffer[] Storage;
            // Current reader operation number
            public int ReaderOperation;
            // Current writer operation number
            public int WriterOperation;
        }

        private /*sealed class*/ struct CycleBuffer
        {
            public CycleBuffer(int capacity)
            {
                var seg = new CycleBufferSegment(capacity);

                Head   = seg;
                Reader = seg;
                Writer = seg;
                WriterSync = 0;
            }

            // head segment
            public CycleBufferSegment Head;

            // current reader segment
            public CycleBufferSegment Reader;

            // current writer segment
            public CycleBufferSegment Writer;

            public int WriterSync;
        }

        private sealed class CycleBufferSegment
        {
            public CycleBufferSegment(int capacity)
            {
                var mes = new T[capacity];
                
                ReaderMessages = mes;
                WriterMessages = mes;
            }

            // Reading thread position
            public int ReaderPosition;

            public T[] ReaderMessages;
            // Writing thread position
            public int WriterPosition;

            public T[] WriterMessages;

            // Next segment
            public CycleBufferSegment Next;

            public override string ToString()
            {
                return this.GetHashCode().ToString();
            }
        }

        #endregion
    }
}