// Maksim Burtsev https://github.com/MBurtsev
// Licensed under the MIT license.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;

/* Research aggressive memory allocation.
 * Each next segment is 2 times larger. As practice has shown, this approach is not justified. 
 * Losses increase with the growth of threads concurrency.
 
| Method | Threads |     Mean |     Error |    StdDev |      Min |      Max |          Op/s |     Op/s total |
|------- |-------- |---------:|----------:|----------:|---------:|---------:|--------------:|--------------- |
|   Read |       1 | 6.317 ns | 0.7995 ns | 0.1237 ns | 6.164 ns | 6.422 ns | 158,291,164.6 | 158,291,164.60 |
|   Read |       1 | 6.349 ns | 1.0468 ns | 0.2719 ns | 6.077 ns | 6.771 ns | 157,495,022.8 | 157,495,022.80 |
|   Read |       1 | 6.268 ns | 0.4170 ns | 0.0645 ns | 6.188 ns | 6.327 ns | 159,548,713.3 | 159,548,713.30 |
|   Read |       1 | 6.212 ns | 0.3040 ns | 0.0790 ns | 6.120 ns | 6.327 ns | 160,976,086.9 | 160,976,086.90 |

| Method | Threads |      Mean |     Error |    StdDev |       Min |      Max |          Op/s |     Op/s total |
|------- |-------- |----------:|----------:|----------:|----------:|---------:|--------------:|--------------- |
|  Write |       1 |  9.803 ns | 3.5358 ns | 0.9182 ns |  8.744 ns | 10.76 ns | 102,008,138.3 | 102,008,138.30 |
|  Write |       2 | 12.698 ns | 2.6105 ns | 0.4040 ns | 12.354 ns | 13.28 ns |  78,750,700.5 | 157,501,401.00 |
|  Write |       4 | 14.616 ns | 2.2556 ns | 0.5858 ns | 13.731 ns | 15.15 ns |  68,418,520.1 | 273,674,080.40 |
|  Write |       8 | 18.859 ns | 0.4787 ns | 0.0741 ns | 18.769 ns | 18.95 ns |  53,024,278.1 | 424,194,224.80 |

|          Method | Threads |     Mean |     Error |   StdDev |   Median |       Min |      Max |         Op/s |     Op/s total |
|---------------- |-------- |---------:|----------:|---------:|---------:|----------:|---------:|-------------:|--------------- |
| WriteWithReader |       1 | 17.26 ns | 33.502 ns | 8.700 ns | 11.92 ns |  9.171 ns | 27.14 ns | 57,936,858.3 |  57,936,858.30 |
| WriteWithReader |       2 | 13.07 ns |  3.829 ns | 0.994 ns | 13.13 ns | 11.621 ns | 14.08 ns | 76,491,967.0 | 152,983,934.00 |
| WriteWithReader |       4 | 16.14 ns |  7.519 ns | 1.953 ns | 15.45 ns | 13.733 ns | 18.80 ns | 61,943,706.8 | 247,774,827.20 |
| WriteWithReader |       8 | 18.38 ns |  6.135 ns | 1.593 ns | 18.89 ns | 16.495 ns | 19.85 ns | 54,397,827.7 | 435,182,621.60 |

*/

namespace DataflowChannel_D
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
        private const int DEFAULT_SEGMENT_CAPACITY = 32 * 1024;
        // Channel data
        private ChannelData _channel;

        public ChannelMPOCnoOrder() : this(DEFAULT_SEGMENT_CAPACITY)
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

                if (pos == link.WriterMessages.Length)
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
                    next = new CycleBufferSegment(seg.Messages.Length * 2)
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

                    if (pos == seg.Messages.Length)
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
                var seg = new CycleBufferSegment(DEFAULT_SEGMENT_CAPACITY);
                
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
            public CycleBufferSegment(int size)
            {
                var messages = new T[size];

                Messages   = messages;
                WriterLink = new WriterLink(messages);
            }

            // Reading thread position
            public int ReaderPosition;
            //
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