// Maksim Burtsev https://github.com/MBurtsev
// Licensed under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Threading;

// Here is the working class, but not so fast as I want

namespace DataflowChannelA
{
    /// <summary>
    /// MPOC - Multiple Producer One Consumer.
    /// At the core a cycle buffer that implements a producer-consumer pattern. 
    /// Synchronization of writer threads only with Interlocked.Add.
    /// Reader fully lock-free\wait-free.
    /// </summary>
    public partial class ChannelMPOC<T>
    {
        // The default value that is used if the user has not specified a capacity.
        private const int DEFAULT_CAPACITY = 32 * 1024;
        private readonly int _capacity;

        // Chennel data
        private CycleBufferMPOC<T> _data;

        public ChannelMPOC() : this(DEFAULT_CAPACITY)
        { 
        }

        public ChannelMPOC(int capacity)
        {
            _data   = new CycleBufferMPOC<T>(capacity);
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
                var pos = Interlocked.Add(ref seg.WriterSync, 1);

                if (pos == _capacity + 1)
                {
                    CycleBufferSegmentMPOC<T> next;

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
                        next = new CycleBufferSegmentMPOC<T>(_capacity)
                        {
                            Next = seg.Next
                        };

                        seg.Next = next;
                    }

                    next.WriterMessages[0] = value;
                    next.WriterPosition = 1;
                    next.WriterSync = 1;

                    seg.WriterPosition = _capacity;

                    Volatile.Write(ref _data.Writer, next);

                    return;
                }
                else if (pos > _capacity + 1)
                {
                    while (Volatile.Read(ref _data.Writer) == seg)
                    {
                    }

                    Write(value);

                    return;
                }

                seg.WriterMessages[pos - 1] = value;

                Interlocked.Add(ref seg.WriterPosition, 1);
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

                    CycleBufferSegmentMPOC<T> next;

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
            _data = new CycleBufferMPOC<T>(_capacity);
        }

        #region ' Structures '

        private sealed class CycleBufferMPOC<T>
        {
            public CycleBufferMPOC(int capacity)
            {
                var seg = new CycleBufferSegmentMPOC<T>(capacity);

                Head = seg;
                Reader = seg;
                Writer = seg;
            }

            // head segment
            public CycleBufferSegmentMPOC<T> Head;
            private long _empty00;
            private long _empty01;
            private long _empty02;
            private long _empty03;
            private long _empty04;
            private long _empty05;
            private long _empty06;
            private long _empty07;

            // current reader segment
            public CycleBufferSegmentMPOC<T> Reader;
            private long _empty08;
            private long _empty09;
            private long _empty10;
            private long _empty11;
            private long _empty12;
            private long _empty13;
            private long _empty14;
            private long _empty15;

            // current writer segment
            public CycleBufferSegmentMPOC<T> Writer;
            private long _empty16;
            private long _empty17;
            private long _empty18;
            private long _empty19;
            private long _empty20;
            private long _empty21;
            private long _empty22;
            private long _empty23;
        }

        private sealed class CycleBufferSegmentMPOC<T>
        {
            public CycleBufferSegmentMPOC(int capacity)
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

            // For sync writers
            public int WriterSync;
            private long _empty24;
            private long _empty25;
            private long _empty26;
            private long _empty27;
            private long _empty28;
            private long _empty29;
            private long _empty30;
            private long _empty31;

            public T[] WriterMessages;
            private long _empty32;
            private long _empty33;
            private long _empty34;
            private long _empty35;
            private long _empty36;
            private long _empty37;
            private long _empty38;
            private long _empty39;

            // Next segment
            public CycleBufferSegmentMPOC<T> Next;
            private long _empty40;
            private long _empty41;
            private long _empty42;
            private long _empty43;
            private long _empty44;
            private long _empty45;
            private long _empty46;
            private long _empty47;

            public override string ToString()
            {
                return this.GetHashCode().ToString();
            }
        }

        #endregion

    }
}