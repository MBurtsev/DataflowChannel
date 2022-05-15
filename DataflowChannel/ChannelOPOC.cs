// Maksim Burtsev https://github.com/MBurtsev
// Licensed under the MIT license.

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
        // The default value that is used if the user has not specified a capacity.
        private const int DEFAULT_CAPACITY = 64 * 1024;
        private readonly int _capacity;

        // Chennel data
        private CycleBuffer _data;

        public ChannelOPOC() : this(DEFAULT_CAPACITY)
        { 
        }

        public ChannelOPOC(int capacity)
        {
            _data   = new CycleBuffer(capacity);
            _capacity = capacity;
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

                if (seg.WriterPosition == _capacity)
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
                        next = new CycleBufferSegment(_capacity)
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

                var pos = seg.WriterPosition;

                seg.WriterMessages[pos] = value;
                seg.WriterPosition = pos + 1;
            }
        }

        public bool TryRead([MaybeNullWhen(false)] out T value)
        {
            unchecked
            {
                var seg = _data.Reader;

                if (seg.ReaderPosition == _capacity)
                {
                    if (seg == _data.Writer)
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
                        next = _data.Head;
                    }

                    next.ReaderPosition = 0;

                    seg = next;

                    _data.Reader = seg;
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
            _data = new CycleBuffer(_capacity);
        }

        #region ' Structures '

        private sealed class CycleBuffer
        {
            public CycleBuffer(int capacity)
            {
                var seg = new CycleBufferSegment(capacity);

                Head = seg;
                Reader = seg;
                Writer = seg;
            }

            // head segment
            public CycleBufferSegment Head;
            private long _empty00;
            private long _empty01;
            private long _empty02;
            private long _empty03;
            private long _empty04;
            private long _empty05;
            private long _empty06;
            private long _empty07;

            // current reader segment
            public CycleBufferSegment Reader;
            private long _empty08;
            private long _empty09;
            private long _empty10;
            private long _empty11;
            private long _empty12;
            private long _empty13;
            private long _empty14;
            private long _empty15;

            // current writer segment
            public CycleBufferSegment Writer;
            private long _empty16;
            private long _empty17;
            private long _empty18;
            private long _empty19;
            private long _empty20;
            private long _empty21;
            private long _empty22;
            private long _empty23;
        }

        private sealed class CycleBufferSegment
        {
            public CycleBufferSegment(int capacity)
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
            public CycleBufferSegment Next;
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