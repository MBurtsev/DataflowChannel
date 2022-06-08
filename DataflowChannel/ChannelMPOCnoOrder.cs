// Maksim Burtsev https://github.com/MBurtsev
// Licensed under the MIT license.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;

namespace DataflowChannel
{
    /// <summary>
    /// MPOC - Multiple Producer One Consumer.
    /// At the core a cycle buffer that implements a producer-consumer pattern. 
    /// Producers use a spinlock for an initialization thread only once at a time.
    /// All read-write operations fully lock-free\wait-free.
    /// No order means that read order is not equal to write order.
    /// </summary>
    public partial class ChannelMPOCnoOrder<T>
    {
        private const int THREADS_STORAGE_SIZE = 32;
        // The default value that is used if the user has not specified a capacity.
        private const int SEGMENT_CAPACITY = 32 * 1024;
        // Channel data
        private ChannelData _channel;

        public ChannelMPOCnoOrder() : this(SEGMENT_CAPACITY)
        { 
        }

        public ChannelMPOCnoOrder(int capacity)
        {
            _channel  = new ChannelData(((Thread.CurrentThread.ManagedThreadId % THREADS_STORAGE_SIZE) + 1) * THREADS_STORAGE_SIZE);

            for (var i = 0; i < _channel.Storage.Length; i++)
            {
                var buffer = new CycleBuffer();

                _channel.Storage[i] = buffer;
                _channel.WriterLinks[i] = buffer.Head.WriterLink;
            }
        }

        public bool IsEmpty
        {
            get
            {
                var cur = _channel.Head;

                while (cur != null)
                {

                    if (          cur.Reader.Messages != cur.Reader.WriterLink.WriterMessages
                                                      ||
                            cur.Reader.ReaderPosition != cur.Reader.WriterLink.WriterPosition
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
                    var seg = cur.Reader;

                    count += cur.Reader.WriterLink.WriterPosition - cur.Reader.ReaderPosition;

                    seg = seg.Next;

                    while (seg != null)
                    {
                        count += seg.WriterLink.WriterPosition;

                        if (seg == cur.Writer)
                        {
                            break;
                        }

                        if (seg.Next == null)
                        {
                            seg = cur.Head;
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
                var id      = Thread.CurrentThread.ManagedThreadId;
                var link    = channel.WriterLinks[id % channel.Size];

                if (link.Id != id)
                {
                    link = SetupThread(id, ref channel);
                }

                var pos = link.WriterPosition;

                if (pos == SEGMENT_CAPACITY)
                {
                    SetNextSegment(channel, id, value);

                    return;
                }

                link.WriterMessages[pos] = value;
                link.WriterPosition = pos + 1;
            }
        }

        private void SetNextSegment(ChannelData channel, int id, T value)
        {
            unchecked
            {
                CycleBufferSegment next;

                var hash    = id % channel.Size;
                var buffer  = channel.Storage[hash];
                var seg     = buffer.Writer;
                var flag    = seg.Next == null;
                var reader  = buffer.Reader;

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

                    next.WriterLink.Id = id;

                    seg.Next = next;
                }

                var link = channel.WriterLinks[hash] = next.WriterLink;

                link.WriterMessages[0] = value;
                link.WriterPosition = 1;
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
                    if (seg != cur.Writer || pos != seg.WriterLink.WriterPosition)
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

        private WriterLink SetupThread(int id, ref ChannelData channel)
        {
            // set lock
            while (Interlocked.CompareExchange(ref channel.SyncChannel, 1, 0) != 0)
            {
                Thread.Yield();
                channel = Volatile.Read(ref _channel);
            }

            try
            {
                var current = channel;
                var len     = channel.Size;
                var hash    = id % len;
                var max     = Math.Max(id, channel.WriterLinks[hash].Id);

                if (max >= len)
                {
                    // Search for max id
                    for (var i = 0; i < len; ++i)
                    {
                        var item = channel.WriterLinks[i].Id;

                        if (max < item)
                        {
                            max = item;
                        }
                    }

                    len = (max / THREADS_STORAGE_SIZE + 1) * THREADS_STORAGE_SIZE * 2;

                    current = new ChannelData(len);

                    // Move data to new channel
                    for (var i = 0; i < channel.Size; ++i)
                    {
                        var link_item   = channel.WriterLinks[i];
                        var buffer_item = channel.Storage[i];

                        current.WriterLinks[link_item.Id] = link_item;
                        current.Storage[link_item.Id] = buffer_item;
                    }

                    // Fill with empty
                    for (var i = 0; i < len; ++i)
                    {
                        if (current.Storage[i] == null)
                        {
                            var new_buffer = new CycleBuffer();

                            current.Storage[i] = new_buffer;
                            current.WriterLinks[i] = new_buffer.Head.WriterLink;
                        }
                    }

                    current.Head = channel.Head;
                }

                var link   = current.WriterLinks[id];
                var buffer = current.Storage[id];

                //Console.WriteLine($"Setup {id}");

                link.Id = id;

                if (current.Head == null)
                {
                    current.Reader = buffer;
                }

                buffer.Next  = current.Head;
                current.Head = buffer;

                // write new link
                if (current != channel)
                {
                    _channel = channel = current;
                }

                return link;
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
                WriterLinks = new WriterLink[capacity];
                Head    = null;
                Reader  = null;
                Size    = capacity;
            }

            // Head of linked list for reader
            public CycleBuffer Head;
            // Current reader position
            public CycleBuffer Reader;
            // To synchronize threads when expanding the storage or setup new thread
            public int SyncChannel;
            // Hash divider
            public readonly int Size;
            // For writers
            public readonly CycleBuffer[] Storage;
            //
            public readonly WriterLink[] WriterLinks;
        }

        [DebuggerDisplay("{GetHashCode()}")]
        private sealed class CycleBuffer
        {
            public CycleBuffer()
            {
                var seg = new CycleBufferSegment();
                
                Head   = seg;
                Reader = seg;
                Writer = seg;
                Next   = null;
            }

            // Current reader segment
            public CycleBufferSegment Reader;
            // Current writer segment
            public CycleBufferSegment Writer;
            // Head segment
            public CycleBufferSegment Head;
            // Next thread data segment
            public CycleBuffer Next;
        }

        [DebuggerDisplay("Reader:{ReaderPosition}")]
        private sealed class CycleBufferSegment
        {
            public CycleBufferSegment()
            {
                var messages = new T[SEGMENT_CAPACITY];

                Messages   = messages;
                WriterLink = new WriterLink(messages);
            }

            // Reading thread position
            public int ReaderPosition;

            public readonly T[] Messages;
            // 
            public readonly WriterLink WriterLink;
            // Next segment
            public CycleBufferSegment Next;
        }

        private sealed class WriterLink
        {
            public WriterLink(T[] messages)
            {
                WriterMessages = messages;
            }

            // Owner id
            public int Id;
            // Writing thread position
            public int WriterPosition;
            // Current writer segment
            public readonly T[] WriterMessages;
        }

        #endregion
    }
}