// Maksim Burtsev https://github.com/MBurtsev
// Licensed under the MIT license.

using System.Diagnostics.CodeAnalysis;

namespace DataflowChannel
{
    /// <summary>
    /// OPOC - One Producer One Consumer.
    /// Channel to use only two threads at the same time. 
    /// At the core a cycle buffer that implements a producer-consumer pattern. 
    /// Fully Wait-Free implementation without any CAS operations.
    /// </summary>
    public partial class ChannelOPOC<T>
    {
        // The default value that is used if the user has not specified a capacity.
        private const int DEFAULT_CAPACITY = 32 * 1024;
        private readonly int _capacity;

        // Chennel data
        private CycleBuffer<T> _buffer;

        public ChannelOPOC() : this(DEFAULT_CAPACITY)
        { 
        }

        public ChannelOPOC(int capacity)
        {
            _buffer   = new CycleBuffer<T>(capacity);
            _capacity = capacity;
        }

        public bool IsEmpty
        {
            get
            {
                 return            _buffer.Reader == _buffer.Writer 
                                                  && 
                    _buffer.Reader.ReaderPosition == _buffer.Writer.WriterPosition;
            }
        }

        public int Count
        {
            get
            {
                var seg = _buffer.Reader;
                var count = _buffer.Reader.WriterPosition - _buffer.Reader.ReaderPosition;
                
                seg = seg.Next;

                while (seg != null)
                {
                    count += seg.WriterPosition;

                    if (seg == _buffer.Writer)
                    {
                        break;
                    }

                    if (seg.Next == null)
                    {
                        seg = _buffer.Head;
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
            checked
            {
                var seg = _buffer.Writer;

                if (seg.WriterPosition == _capacity)
                {
                    CycleBufferSegment<T> new_seg;

                    var flag = seg.Next == null;

                    if (!flag && seg.Next != _buffer.Reader)
                    {
                        new_seg = seg.Next;
                    }
                    else if (flag && _buffer.Head != _buffer.Reader)
                    {
                        new_seg = _buffer.Head;
                    }
                    else
                    {
                        new_seg = new CycleBufferSegment<T>(_capacity)
                        {
                            Next = seg.Next
                        };

                        seg.Next = new_seg;
                    }

                    new_seg.WriterMessages[0] = value;
                    new_seg.WriterPosition = 1;

                    _buffer.Writer = new_seg;

                    return;
                }

                seg.WriterMessages[seg.WriterPosition] = value;
                seg.WriterPosition++;
            }
        }

        public bool TryRead([MaybeNullWhen(false)] out T value)
        {
            var seg = _buffer.Reader;

            if (seg.ReaderPosition == _capacity)
            {
                if (seg == _buffer.Writer)
                {
                    value = default;

                    return false;
                }

                CycleBufferSegment<T> new_seg;

                if (seg.Next != null)
                {
                    new_seg = seg.Next;
                }
                else
                {
                    new_seg = _buffer.Head;
                }

                new_seg.ReaderPosition = 0;

                seg = new_seg;

                _buffer.Reader = seg;
            }

            // validation reader position
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
}