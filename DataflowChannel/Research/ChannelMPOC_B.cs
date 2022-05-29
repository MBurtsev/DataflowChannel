// Maksim Burtsev https://github.com/MBurtsev
// Licensed under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Threading;

// Here is the working class, but not so fast as I want

namespace DataflowChannel_B
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
        private CycleBuffer _data;

        public ChannelMPOC() : this(DEFAULT_CAPACITY)
        { 
        }

        public ChannelMPOC(int capacity)
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
                var pos = Interlocked.Add(ref seg.WriterSync, 1);

                if (pos == _capacity + 1)
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

            // current reader segment
            public CycleBufferSegment Reader;

            // current writer segment
            public CycleBufferSegment Writer;
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

            public T[] ReaderMessages;

            // Writing thread position
            public int WriterPosition;

            // For sync writers
            public int WriterSync;

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