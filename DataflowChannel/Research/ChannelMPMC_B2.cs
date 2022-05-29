// Maksim Burtsev https://github.com/MBurtsev
// Licensed under the MIT license.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace DataflowChannel_B2
{
    /// <summary>
    /// MPOC - Multiple Producer Multiple Consumer.
    /// </summary>
    public partial class ChannelMPMC<T>
    {
        // The default value that is used if the user has not specified a capacity.
        private const int SEGMENT_CAPACITY = 32*1024;
        private const int MESSAGES_CAPACITY = 32;
        private const int OPERATION_CAPACITY = 1024;
        // Current segment size
        private readonly int _capacity;
        private ChannelData _channel;

        public ChannelMPMC() : this(MESSAGES_CAPACITY)
        { 
        }

        public ChannelMPMC(int capacity)
        {
            _capacity = capacity;
            _channel = new ChannelData();
        }

        public void Write(T value)
        {
            
            unchecked
            {
                var channel = _channel;
                var operation = Interlocked.Add(ref channel.WriterOperation, 1);

                ref var data = ref channel.Storage[operation % OPERATION_CAPACITY];
                ref var seg  = ref data.Segments[data.Writer];

                if (seg.WriterPosition == _capacity)
                {
                    int next;

                    var flag = seg.Next == 0;

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
                        next = data.Count++;

                        // resize segments
                        if (next == data.Segments.Length)
                        {
                            var tmp = new CycleBufferSegment[next * 2];
                            
                            Array.Copy(data.Segments, tmp, data.Segments.Length);

                            data.Segments = tmp;
                        }

                        data.Segments[next] = new CycleBufferSegment(true)
                        {
                            Next = seg.Next
                        };

                        seg.Next = next;
                    }

                    seg = ref data.Segments[next];

                    seg.Messages[0] = value;
                    seg.WriterPosition = 1;

                    data.Writer = next;

                    return;
                }

                var pos = seg.WriterPosition;

                seg.Messages[pos] = value;
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
            public ChannelData()
            {
                Storage = new CycleBuffer[OPERATION_CAPACITY];

                for (var i = 0; i < OPERATION_CAPACITY; i++)
                {
                    Storage[i] = new CycleBuffer(true);
                }
            }

            public readonly CycleBuffer[] Storage;
            // Current reader operation number
            public int ReaderOperation;
            // Current writer operation number
            public int WriterOperation;
        }

        private struct CycleBuffer
        {
            public CycleBuffer(bool flag)
            {
                Count    = 1;
                Head     = 0;
                Reader   = 0;
                Writer   = 0;
                Segments = new CycleBufferSegment[SEGMENT_CAPACITY];

                Segments[0] = new CycleBufferSegment(true);
            }

            public int Count;

            // head segment
            public int Head;

            // current reader segment
            public int Reader;

            // current writer segment
            public int Writer;

            public CycleBufferSegment[] Segments;
        }

        private struct CycleBufferSegment
        {
            public CycleBufferSegment(bool flag)
            {
                ReaderPosition = 0;
                WriterPosition = 0;
                Next           = 0;
                Messages       = new T[MESSAGES_CAPACITY];
            }

            // Reading thread position
            public int ReaderPosition;
            // Writing thread position
            public int WriterPosition;
            // Next segment
            public int Next;

            public T[] Messages;
        }

        #endregion
    }
}