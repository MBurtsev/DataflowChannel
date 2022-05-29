// Maksim Burtsev https://github.com/MBurtsev
// Licensed under the MIT license.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace DataflowChannel_C
{
    public partial class ChannelMPMC_C<T>
    {
        // The default value that is used if the user has not specified a capacity.
        private const int SEGMENT_CAPACITY = 32*1024;
        private const int OPERATION_CAPACITY = 1024;
        // Current segment size
        private readonly int _capacity;
        private ChannelData _channel;

        public ChannelMPMC_C() : this(SEGMENT_CAPACITY * 8)
        { 
        }

        public ChannelMPMC_C(int capacity)
        {
            _capacity = capacity;
            _channel = new ChannelData(capacity);
        }

        public void Write(T value)
        {
            unchecked
            {
                var channel = _channel;
                var operation = Interlocked.Add(ref channel.WriterOperation, 1);
                //var operation = ++channel.WriterOperation;
                //var operation = Thread.CurrentThread.ManagedThreadId;
                ref var data = ref channel.Storage[operation % OPERATION_CAPACITY];
                //var data = channel.Storage[operation % OPERATION_CAPACITY];
                var seg = data.Writer;
                var pos = seg.WriterPosition;

                if (pos == _capacity)
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

                seg.WriterMessages[pos] = value;
                seg.WriterPosition = pos + 1;
            }
        }

        public bool TryRead([MaybeNullWhen(false)] out T value)
        {
            unchecked
            {
                var channel = _channel;
                var operation = Interlocked.Add(ref channel.ReaderOperation, 1);
                ref var data = ref channel.Storage[operation % OPERATION_CAPACITY];
                var seg = data.Reader;
                var pos = seg.ReaderPosition;

                if (pos == SEGMENT_CAPACITY)
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

                    next.ReaderPosition = pos = 0;
                    data.Reader = seg = next;
                }

                // reader position check
                if (pos != seg.WriterPosition)
                {
                    value = seg.ReaderMessages[pos];

                    seg.ReaderPosition = pos + 1;

                    return true;
                }

                value = default;

                return false;
            }
        }

        #region ' Structures '

        private sealed class ChannelData
        {
            public ChannelData(int capacity)
            {
                Storage = new CycleBuffer[OPERATION_CAPACITY];

                var proc = Environment.ProcessorCount;

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
        }

        #endregion
    }
}