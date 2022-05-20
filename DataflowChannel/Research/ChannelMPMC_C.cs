// Maksim Burtsev https://github.com/MBurtsev
// Licensed under the MIT license.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;

namespace DataflowChannel.Research_C
{
    /// <summary>
    /// MPOC - Multiple Producer One Consumer.
    /// At the core a cycle buffer that implements a producer-consumer pattern. 
    /// Producers use spin lock for initialization thread only once. All
    /// write operations fully lock-free\wait-free.
    /// Customer fully lock-free\wait-free.
    /// No order means that read order is not equal to write order.
    /// </summary>
    public partial class ChannelMPMC<T>
    {
        private const int THREADS_STORAGE_SIZE = 32;
        // The default value that is used if the user has not specified a capacity.
        private const int SEGMENT_CAPACITY = 32 * 1024;
        // Channel data
        private ChannelData _channel;

        public ChannelMPMC() : this(SEGMENT_CAPACITY)
        {
        }

        public ChannelMPMC(int capacity)
        {
            _channel = new ChannelData((Thread.CurrentThread.ManagedThreadId % THREADS_STORAGE_SIZE + 1) * THREADS_STORAGE_SIZE);

            for (var i = 0; i < _channel.Storage.Length; i++)
            {
                _channel.Storage[i] = new ThreadContext();
            }
        }

        public bool IsEmpty
        {
            get
            {
                var cur = _channel.Head;

                while (cur != null)
                {

                    if (cur.Buffer.Reader != cur.Buffer.Writer
                                                           ||
                            cur.Buffer.Reader.ReaderPosition != cur.Buffer.Writer.WriterPosition
                        )
                    {
                        return false;
                    }

                    cur = cur.Next;
                }

                return true;
            }
        }

        public int Count
        {
            get
            {
                var count = 0;

                var cur = _channel.Head;

                while (cur != null)
                {
                    var seg = cur.Buffer.Reader;

                    count += cur.Buffer.Reader.WriterPosition - cur.Buffer.Reader.ReaderPosition;

                    seg = seg.Next;

                    while (seg != null)
                    {
                        count += seg.WriterPosition;

                        if (seg == cur.Buffer.Writer)
                        {
                            break;
                        }

                        if (seg.Next == null)
                        {
                            seg = cur.Buffer.Head;
                        }
                        else
                        {
                            seg = seg.Next;
                        }
                    }

                    cur = cur.Next;
                }

                return count;
            }
        }

        public void Write(T value)
        {
            unchecked
            {
                var channel = _channel;
                var context = GetContext();
                ref var buffer = ref context.Buffer;
                var seg = buffer.Writer;
                var opr = Interlocked.Add(ref channel.WriterOperation, 1);

                channel.OperationTable[opr % 4] = context.Id;

                if (seg.WriterPosition == SEGMENT_CAPACITY)
                {
                    CycleBufferSegment next;

                    var flag = seg.Next == null;
                    var reader = buffer.Reader;

                    if (!flag && seg.Next != reader)
                    {
                        next = seg.Next;
                    }
                    else if (flag && buffer.Head != reader)
                    {
                        next = buffer.Head;
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

                    buffer.Writer = next;

                    return;
                }

                seg.WriterMessages[seg.WriterPosition] = value;
                seg.WriterPosition++;

                // set link
                opr--;
                seg = channel.Storage[opr % 4].Buffer.Writer;
                seg.WriterMessages[0] = value;
            }
        }

        public bool TryRead([MaybeNullWhen(false)] out T value)
        {
            unchecked
            {
                var channel = _channel;
                var start   = channel.Reader;
                var cur     = start;

                if (cur == null)
                {
                    value = default;

                    return false;
                }

                do
                {
                    var seg = cur.Buffer.Reader;

                    if (seg.ReaderPosition == SEGMENT_CAPACITY)
                    {
                        if (seg == cur.Buffer.Writer)
                        {
                            goto proceed;
                        }

                        CycleBufferSegment next;

                        if (seg.Next != null)
                        {
                            next = seg.Next;
                        }
                        else
                        {
                            next = cur.Buffer.Head;
                        }

                        next.ReaderPosition = 0;

                        seg = next;

                        cur.Buffer.Reader = seg;
                    }

                    // reader position check
                    if (seg.ReaderPosition != seg.WriterPosition)
                    {
                        value = seg.ReaderMessages[seg.ReaderPosition];

                        seg.ReaderPosition++;

                        return true;
                    }

                proceed:
                    cur = channel.Reader = cur.Next;

                    if (cur == null)
                    {
                        cur = channel.Reader = channel.Head;
                    }
                }
                while (cur != start);

                value = default;

                return false;
            }
        }

        public void Clear()
        {
            var channel = _channel;

            // set lock
            while (Interlocked.CompareExchange(ref channel.SyncChannel, 1, 0) != 0)
            {
                channel = Volatile.Read(ref _channel);
            }

            var newChannel = new ChannelData(channel.Size);

            for (var i = 0; i < newChannel.Storage.Length; i++)
            {
                newChannel.Storage[i] = new ThreadContext();
            }

            _channel = newChannel;

            // unlock
            channel.SyncChannel = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ThreadContext GetContext()
        {
            unchecked
            {
                var id      = Thread.CurrentThread.ManagedThreadId;
                var channel = _channel;
                var hash    = id % channel.Size;
                var thread  = channel.Storage[hash];

                if (thread.Id == id)
                {
                    return thread;
                }

                return SetupThread(id);
            }
        }

        private ThreadContext SetupThread(int id)
        {
            var channel = _channel;

            // set lock
            while (Interlocked.CompareExchange(ref channel.SyncChannel, 1, 0) != 0)
            {
                channel = Volatile.Read(ref _channel);
            }

            try
            {
                var current = channel;
                var len     = channel.Size;
                var hash    = id % len;
                var max     = Math.Max(id, channel.Storage[hash].Id);

                if (max >= len)
                {
                    // Search for max id
                    for (var i = 0; i < len; ++i)
                    {
                        var item = channel.Storage[i].Id;

                        if (max < item)
                        {
                            max = item;
                        }
                    }

                    len = (max % THREADS_STORAGE_SIZE + 1) * THREADS_STORAGE_SIZE * 2;

                    current = new ChannelData(len);

                    // Move data to new channel
                    for (var i = 0; i < channel.Size; ++i)
                    {
                        var item = channel.Storage[i];

                        current.Storage[item.Id] = item;
                    }

                    // Fill with empty
                    for (var i = 0; i < len; ++i)
                    {
                        if (current.Storage[i] == null)
                        {
                            current.Storage[i] = new ThreadContext();
                        }
                    }

                    current.Head = channel.Head;
                }
                //// check the element in the correct place
                //else if (channel.Storage[hash].Id != 0 && channel.Storage[hash].Id != hash)
                //{
                //    var tmp = channel.Storage[hash];

                //    channel.Storage[tmp.Id] = tmp;
                //    channel.Storage[hash]   = new ThreadData(_capacity);
                //}

                var thread = current.Storage[id];

                thread.Id = id;
                thread.Next = current.Head;
                current.Head = thread;

                if (thread.Next == null)
                {
                    current.Reader = thread;
                }

                // write new link
                if (current != channel)
                {
                    Volatile.Write(ref _channel, current);
                }

                return thread;
            }
            finally
            {
                // unlock
                channel.SyncChannel = 0;
            }
        }

        #region ' Structures '

        private sealed class ChannelData
        {
            public ChannelData(int capacity)
            {
                Storage = new ThreadContext[capacity];
                Head = null;
                Reader = null;
                Size = capacity;
            }

            // Head of linked list for reader
            public ThreadContext Head;
            // Current reader position
            public ThreadContext Reader;
            // To synchronize threads when expanding the storage or setup new thread
            public int SyncChannel;
            // For writers
            public readonly ThreadContext[] Storage;
            //
            public readonly int[] OperationTable = new int[4];
            public int WriterOperation;
            // Hash divider
            public readonly int Size;
        }

        [DebuggerDisplay("Id:{Id}")]
        private sealed class ThreadContext
        {
            public ThreadContext()
            {
                Id = 0;
                Buffer = new CycleBuffer();
            }

            // Owner id
            public int Id;
            // Channel data
            public CycleBuffer Buffer;
            // Next thread data segment
            public ThreadContext Next;

            //EmptySpace empty;
        }

        private /*sealed class*/ struct CycleBuffer
        {
            public CycleBuffer()
            {
                var seg = new CycleBufferSegment();

                Head = seg;
                Reader = seg;
                Writer = seg;
            }

            // Head segment
            public CycleBufferSegment Head;
            // Current reader segment
            public CycleBufferSegment Reader;
            // Current writer segment
            public CycleBufferSegment Writer;
        }

        [DebuggerDisplay("Reader:{ReaderPosition}, Writer:{WriterPosition}")]
        private sealed class CycleBufferSegment
        {
            public CycleBufferSegment()
            {
                var messages = new T[SEGMENT_CAPACITY];

                ReaderMessages = messages;
                WriterMessages = messages;
            }

            // Reading thread position
            public int ReaderPosition;

            public readonly T[] ReaderMessages;

            // Writing thread position
            public int WriterPosition;

            public readonly T[] WriterMessages;

            // Next segment
            public CycleBufferSegment Next;

            public override string ToString()
            {
                return GetHashCode().ToString();
            }
        }

        #endregion
    }
}