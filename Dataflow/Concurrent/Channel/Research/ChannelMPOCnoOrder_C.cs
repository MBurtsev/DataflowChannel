// Maksim Burtsev https://github.com/MBurtsev
// Licensed under the MIT license.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Dataflow.Concurrent.Channel_C
{
    /// <summary>
    /// MPOC - Multiple Producer One Consumer.
    /// At the core a cycle buffer that implements a producer-consumer pattern. 
    /// Producers use a spinlock for an initialization thread only once at a time.
    /// All read-write operations fully lock-free\wait-free.
    /// No order means that read order is not equal to write order.
    /// </summary>
    public partial class ChannelMPOCnoOrder_C<T>
    {
        private const int THREADS_STORAGE_SIZE = 32;
        // The default value that is used if the user has not specified a capacity.
        private const int SEGMENT_CAPACITY = 32 * 1024;
        // Channel data
        private ChannelData _channel;

        public ChannelMPOCnoOrder_C() : this(SEGMENT_CAPACITY)
        {
        }

        public ChannelMPOCnoOrder_C(int capacity)
        {
            _channel = new ChannelData((Thread.CurrentThread.ManagedThreadId % THREADS_STORAGE_SIZE + 1) * THREADS_STORAGE_SIZE);

            for (var i = 0; i < _channel.Storage.Length; i++)
            {
                _channel.Storage[i] = new CycleBuffer();
            }
        }

        public bool IsEmpty
        {
            get
            {
                var cur = _channel.Head;

                while (cur != null)
                {

                    if (cur.Reader.Messages != cur.WriterMessages
                                                      ||
                            cur.Reader.ReaderPosition != cur.WriterPosition
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

                //var cur = _channel.Head;

                //while (cur != null)
                //{
                //    var seg = cur.Reader;

                //    count += cur.WriterPosition - cur.Reader.ReaderPosition;

                //    seg = seg.Next;

                //    while (seg != null)
                //    {
                //        count += seg.WriterPosition;

                //        if (seg == cur.Writer)
                //        {
                //            break;
                //        }

                //        if (seg.Next == null)
                //        {
                //            seg = cur.Head;
                //        }
                //        else
                //        {
                //            seg = seg.Next;
                //        }
                //    }

                //    cur = cur.Next;
                //}

                return count;
            }
        }

        public void Write(T value)
        {
            unchecked
            {
                var id      = Thread.CurrentThread.ManagedThreadId;
                var channel = _channel;
                var buffer  = channel.Storage[id % channel.Size];

                if (buffer.Id != id)
                {
                    buffer = SetupThread(id);
                }

                var pos = buffer.WriterPosition;

                if (pos == SEGMENT_CAPACITY)
                {
                    SetNextSegment(buffer, value);

                    return;
                }

                buffer.WriterMessages[pos] = value;
                buffer.WriterPosition = pos + 1;
            }
        }

        private void SetNextSegment(CycleBuffer buffer, T value)
        {
            unchecked
            {
                var seg = buffer.Writer;
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

                buffer.WriterMessages = next.Messages;
                buffer.WriterMessages[0] = value;
                buffer.WriterPosition = 1;
                buffer.Writer = next;
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
                    var seg = cur.Reader;
                    var pos = seg.ReaderPosition;

                    if (pos == SEGMENT_CAPACITY)
                    {
                        if (seg == cur.Writer)
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
                            next = cur.Head;
                        }

                        pos = next.ReaderPosition = 0;
                        seg = cur.Reader = next;
                    }

                    // reader position check
                    if (seg != cur.Writer || pos != cur.WriterPosition)
                    {
                        value = seg.Messages[pos];

                        seg.ReaderPosition = pos + 1;

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
                newChannel.Storage[i] = new CycleBuffer();
            }

            _channel = newChannel;

            // unlock
            channel.SyncChannel = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private CycleBuffer GetContext()
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

        private CycleBuffer SetupThread(int id)
        {
            var channel = _channel;

            // set lock
            while (Interlocked.CompareExchange(ref channel.SyncChannel, 1, 0) != 0)
            {
                channel = Volatile.Read(ref _channel);
                Thread.Yield();
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
                            current.Storage[i] = new CycleBuffer();
                        }
                    }

                    current.Head = channel.Head;
                }

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
                Storage = new CycleBuffer[capacity];
                Head = null;
                Reader = null;
                Size = capacity;
            }

            // Head of linked list for reader
            public CycleBuffer Head;
            // Current reader position
            public CycleBuffer Reader;

            //EmptySpace _empty00;

            // To synchronize threads when expanding the storage or setup new thread
            public int SyncChannel;
            // Hash divider
            public readonly int Size;
            // For writers
            public readonly CycleBuffer[] Storage;

            //EmptySpace _empty01;
        }

        private sealed class CycleBuffer
        {
            public CycleBuffer()
            {
                var seg = new CycleBufferSegment();

                Head = seg;
                Reader = seg;
                Writer = seg;
                WriterMessages = seg.Messages;
                Next = null;
                Id = 0;
                //_empty00 = default;
                //_empty01 = default;
            }

            // Owner id
            public int Id;
            // Writing thread position
            public int WriterPosition;
            // Current writer segment
            public T[] WriterMessages;
            // Next thread data segment
            public CycleBuffer Next;
            //
            public CycleBufferSegment Writer;
            // Current reader segment
            public CycleBufferSegment Reader;
            // Head segment
            public CycleBufferSegment Head;

            //EmptySpace _empty00;
            //EmptySpace _empty01;
        }

        [DebuggerDisplay("Reader:{ReaderPosition}")]
        private sealed class CycleBufferSegment
        {
            public CycleBufferSegment()
            {
                var messages = new T[SEGMENT_CAPACITY];

                Messages = messages;
            }

            // Reading thread position
            public int ReaderPosition;

            public readonly T[] Messages;

            // Next segment
            public CycleBufferSegment Next;
        }

        #endregion
    }
}