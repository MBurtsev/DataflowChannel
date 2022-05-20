// Maksim Burtsev https://github.com/MBurtsev
// Licensed under the MIT license.

using DataflowChannel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace DataflowChannel
{
    /// <summary>
    ///  - One Producer One Consumer.
    /// Channel to use only two threads at the same time. 
    /// At the core a cycle buffer that implements a producer-consumer pattern. 
    /// Fully wait-free implementation without any CAS operations.
    /// </summary>
    [DebuggerDisplay("Count:{Count}")]
    public partial class ChannelOPOC<T>
    {
        // Segment size
        private const int SEGMENT_CAPACITY = 32 * 1024;

        // Channel data
        private CycleBuffer _data;

        public ChannelOPOC() : this(SEGMENT_CAPACITY * 8)
        {
        }

        public ChannelOPOC(int capacity)
        {
            _data = new CycleBuffer();

            var seg = _data.Head;
            var len = capacity % SEGMENT_CAPACITY + 1;

            for (var i = 0; i < len; i++)
            {
                seg.Next = new CycleBufferSegment();

                seg = seg.Next;
            }
        }

        public bool IsEmpty
        {
            get
            {
                 return            _data.Reader == _data.Writer 
                                                  && 
                    _data.Reader.ReaderPosition == _data.Writer.WriterPosition;
            }
        }

        public int Count
        {
            get
            {
                var seg = _data.Reader;
                var count = _data.Reader.WriterPosition - _data.Reader.ReaderPosition;
                
                seg = seg.Next;

                while (seg != null)
                {
                    count += seg.WriterPosition;

                    if (seg == _data.Writer)
                    {
                        break;
                    }

                    if (seg.Next == null)
                    {
                        seg = _data.Head;
                    }
                    else
                    {
                        seg = seg.Next;
                    }
                }

                return count;
            }
        }

        public void Write(T value)
        {
            unchecked
            {
                var seg = _data.Writer;
                var pos = seg.WriterPosition;

                if (pos == SEGMENT_CAPACITY)
                {
                    CycleBufferSegment next;

                    var flag = seg.Next == null;

                    if (!flag && seg.Next != _data.Reader)
                    {
                        next = seg.Next;
                    }
                    else if (flag && _data.Head != _data.Reader)
                    {
                        next = _data.Head;
                    }
                    else
                    {
                        next = new CycleBufferSegment()
                        {
                            Next = seg.Next
                        };

                        seg.Next = next;
                    }

                    next.WriterMessages[0] = value;
                    next.WriterPosition = 1;

                    _data.Writer = next;

                    return;
                }

                seg.WriterMessages[pos] = value;
                seg.WriterPosition = pos + 1;
            }
        }

        public bool TryRead([MaybeNullWhen(false)] out T value)
        {
            unchecked
            {
                var data = _data;
                var seg = data.Reader;

                if (seg.ReaderPosition == SEGMENT_CAPACITY)
                {
                    if (seg == data.Writer)
                    {
                        value = default;

                        return false;
                    }

                    CycleBufferSegment next;

                    if (seg.Next != null)
                    {
                        next = seg.Next;
                    }
                    else
                    {
                        next = data.Head;
                    }

                    next.ReaderPosition = 0;

                    seg = next;

                    data.Reader = seg;
                }

                // reader position check
                if (seg.ReaderPosition != seg.WriterPosition)
                {
                    value = seg.ReaderMessages[seg.ReaderPosition];

                    seg.ReaderPosition++;

                    return true;
                }

                value = default;

                return false;
            }
        }

        public void Clear()
        {
            _data = new CycleBuffer();
        }

        #region ' Structures '

        private sealed class CycleBuffer
        {
            public CycleBuffer()
            {
                var seg = new CycleBufferSegment();

                Head   = seg;
                Reader = seg;
                Writer = seg;
            }

            // head segment
            public CycleBufferSegment Head;
            // current reader segment
            public CycleBufferSegment Reader;
            EmptySpace _empty00;

            // current writer segment
            public CycleBufferSegment Writer;
        }

        private sealed class CycleBufferSegment
        {
            public CycleBufferSegment()
            {
                ReaderMessages = new T[SEGMENT_CAPACITY];
                WriterMessages = ReaderMessages;
            }

            // Reading thread position
            public int ReaderPosition;
            public T[] ReaderMessages;

            EmptySpace _empty00;

            // Writing thread position
            public int WriterPosition;
            public T[] WriterMessages;

            EmptySpace _empty01;

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