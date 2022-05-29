// Maksim Burtsev https://github.com/MBurtsev
// Licensed under the MIT license.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;

namespace DataflowChannel_A
{
    /// <summary>
    /// MPOC - Multiple Producer Multiple Consumer.
    /// </summary>
    public partial class ChannelMPMC<T>
    {
        private const int THREADS_STORAGE_SIZE = 64;
        // The default value that is used if the user has not specified a capacity.
        private const int DEFAULT_CAPACITY = 32 * 1024;
        // Current segment size
        private readonly int _capacity;
        // Channel data
        private ChannelData _channel;

        public ChannelMPMC() : this(DEFAULT_CAPACITY)
        { 
        }

        public ChannelMPMC(int capacity)
        {
            _capacity = capacity;
            _channel  = new ChannelData(((Thread.CurrentThread.ManagedThreadId % THREADS_STORAGE_SIZE) + 1) * THREADS_STORAGE_SIZE);

            for (var i = 0; i < _channel.Storage.Length; i++)
            {
                _channel.Storage[i] = new ThreadData(_capacity);
            }
        }

        public bool IsEmpty
        {
            get
            {
                var cur = _channel.Head;

                while (cur != null)
                {

                    if (                   cur.Data.Reader != cur.Data.Writer
                                                           ||
                            cur.Data.Reader.ReaderPosition != cur.Data.Writer.WriterPosition
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
                    var seg = cur.Data.Reader;

                    count += cur.Data.Reader.WriterPosition - cur.Data.Reader.ReaderPosition;

                    seg = seg.Next;

                    while (seg != null)
                    {
                        count += seg.WriterPosition;

                        if (seg == cur.Data.Writer)
                        {
                            break;
                        }

                        if (seg.Next == null)
                        {
                            seg = cur.Data.Head;
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
                var storage = GetStorage(out var channel);
                var data    = storage.Data;
                var seg     = data.Writer;

                if (seg.WriterPosition == _capacity)
                {
                    CycleBufferSegmentMPOC next;

                    var flag = seg.Next == null;
                    var reader = data.Reader;

                    if (!flag && seg.Next != reader)
                    {
                        next = seg.Next;
                    }
                    else if (flag && data.Head != reader)
                    {
                        next = data.Head;
                    }
                    else
                    {
                        next = new CycleBufferSegmentMPOC(_capacity)
                        {
                            Next = seg.Next
                        };

                        seg.Next = next;
                    }

                    next.Back = seg;
                    next.WriterPosition = 0;

                    data.Writer = seg = next;
                }

                // Get last writer thread id
                var last = channel.WriterLastId;
                // Declare new operation id
                var operation = Interlocked.Add(ref channel.WriterOperation, 1);
                // Write value
                seg.WriterMessages[seg.WriterPosition] = 
                    new MessageItem { Value = value, Operation = (uint)operation-- };
                // Don't worry about out of sync. Since the write is atomic,
                // we'll be fine with either of the last values.
                channel.WriterLastId = storage.Id;

                seg.WriterPosition++;

                // Search for previous operation
                while (true)
                {
                    var thread = channel.Storage[last % channel.Size];
                    var tSeg   = thread.Data.Writer;
                    var tPos   = tSeg.WriterPosition - 1;
                    
                    while (true)
                    {
                        ref var tOpr = ref tSeg.WriterMessages[tPos];

                        if (tOpr.Operation == operation)
                        {
                            tSeg.WriterMessages[tPos].Next = storage.Id;

                            return;
                        }
                        else if (tOpr.Operation < operation)
                        {
                            if (tOpr.Next != 0)
                            {
                                last = tOpr.Next;

                                break;
                            }

                            while (Volatile.Read(ref tOpr.Next) == 0)
                            {
                            }

                            last = tOpr.Next;

                            break;
                        }

                        tPos--;

                        if (tPos < 0)
                        {
                            tSeg = tSeg.Back;
                            tPos = _capacity - 1;

                            if (tSeg == null)
                            {
                                return;
                            }
                        }
                    }
                }
            }
        }

        public bool TryRead([MaybeNullWhen(false)] out T value)
        {
            //unchecked
            //{
            //    var channel = _channel;
            //    var start   = channel.Reader;
            //    var cur     = start;

            //    if (cur == null)
            //    {
            //        value = default;

            //        return false;
            //    }

            //    do
            //    {
            //        var seg = cur.Data.Reader;

            //        if (seg.ReaderPosition == _capacity)
            //        {
            //            if (seg == cur.Data.Writer)
            //            {
            //                goto proceed;
            //            }

            //            CycleBufferSegmentMPOC next;

            //            if (seg.Next != null)
            //            {
            //                next = seg.Next;
            //            }
            //            else
            //            {
            //                next = cur.Data.Head;
            //            }

            //            next.ReaderPosition = 0;

            //            seg = next;

            //            cur.Data.Reader = seg;
            //        }

            //        // reader position check
            //        if (seg.ReaderPosition != seg.WriterPosition)
            //        {
            //            value = seg.ReaderMessages[seg.ReaderPosition];

            //            seg.ReaderPosition++;

            //            return true;
            //        }

            //    proceed: 
            //        cur = channel.Reader = cur.Next;

            //        if (cur == null)
            //        {
            //            cur = channel.Reader = channel.Head;
            //        }
            //    } 
            //    while (cur != start);

            value = default;

            return false;
            //}
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
                newChannel.Storage[i] = new ThreadData(_capacity);
            }

            _channel = newChannel;

            // unlock
            channel.SyncChannel = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ThreadData GetStorage(out ChannelData channel)
        {
            unchecked
            {
                channel = _channel;

                var id      = Thread.CurrentThread.ManagedThreadId;
                var hash    = id % channel.Size;
                var thread  = channel.Storage[hash];

                if (thread.Id == id)
                {
                    return thread;
                }

                return SetupThread(id, ref channel);
            }
        }

        private ThreadData SetupThread(int id, ref ChannelData channel)
        {
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

                    len = ((max % THREADS_STORAGE_SIZE) + 1) * THREADS_STORAGE_SIZE * 2;

                    current = new ChannelData(len);

                    // Move data to new channel
                    for (var i = 0; i < channel.Size; ++i)
                    {
                        var item = channel.Storage[i];

                        current.Storage[item.Id] = item;
                    }

                    // Fill with empty Cabinets
                    for (var i = 0; i < len; ++i)
                    {
                        if (current.Storage[i] == null)
                        {
                            current.Storage[i] = new ThreadData(_capacity);
                        }
                    }

                    current.Head = channel.Head;
                }

                var thread = current.Storage[id];

                thread.Id    = id;
                thread.Next  = current.Head;
                current.Head = thread;

                if (thread.Next == null)
                {
                    current.ReaderLastId = id;
                    current.WriterLastId = id;
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
                Storage = new ThreadData[capacity];

                Size    = capacity;
            }

            // Head of linked list for reader
            public ThreadData Head;
            private long _empty00;
            private long _empty01;
            private long _empty02;
            private long _empty03;
            private long _empty04;
            private long _empty05;
            private long _empty06;
            private long _empty07;

            // Current reader operation number
            public uint ReaderOperation;
            private long _empty08;
            private long _empty09;
            private long _empty10;
            private long _empty11;
            private long _empty12;
            private long _empty13;
            private long _empty14;
            private long _empty15;

            // The writer id was reading in last time
            public int ReaderLastId;
            private long _empty16;
            private long _empty17;
            private long _empty18;
            private long _empty19;
            private long _empty20;
            private long _empty21;
            private long _empty22;
            private long _empty23;

            // To synchronize threads when expanding the storage or setup new thread
            public int SyncChannel;
            private long _empty24;
            private long _empty25;
            private long _empty26;
            private long _empty27;
            private long _empty28;
            private long _empty29;
            private long _empty30;
            private long _empty31;

            // For writers
            public readonly ThreadData[] Storage;
            private long _empty32;
            private long _empty33;
            private long _empty34;
            private long _empty35;
            private long _empty36;
            private long _empty37;
            private long _empty38;
            private long _empty39;

            // Hash divider
            public readonly int Size;
            private long _empty40;
            private long _empty41;
            private long _empty42;
            private long _empty43;
            private long _empty44;
            private long _empty45;
            private long _empty46;
            private long _empty47;

            // Current writer operation number
            public int WriterOperation;
            private long _empty48;
            private long _empty49;
            private long _empty50;
            private long _empty51;
            private long _empty52;
            private long _empty53;
            private long _empty54;
            private long _empty55;

            // The writer id was writing in last time
            public int WriterLastId;
            private long _empty56;
            private long _empty57;
            private long _empty58;
            private long _empty59;
            private long _empty60;
            private long _empty61;
            private long _empty62;
            private long _empty63;
        }

        [DebuggerDisplay("Id:{Id}")]
        private sealed class ThreadData
        {
            public ThreadData(int capacity)
            {
                Id   = 0;
                Data = new CycleBufferMPOC(capacity);
            }

            // Owner id
            public int Id;
            private long _empty00;
            private long _empty01;
            private long _empty02;
            private long _empty03;
            private long _empty04;
            private long _empty05;
            private long _empty06;
            private long _empty07;

            // Channel data
            public readonly CycleBufferMPOC Data;
            private long _empty08;
            private long _empty09;
            private long _empty10;
            private long _empty11;
            private long _empty12;
            private long _empty13;
            private long _empty14;
            private long _empty15;

            // Next thread data segment
            public ThreadData Next;
            private long _empty16;
            private long _empty17;
            private long _empty18;
            private long _empty19;
            private long _empty20;
            private long _empty21;
            private long _empty22;
            private long _empty23;
        }

        private sealed class CycleBufferMPOC
        {
            public CycleBufferMPOC(int capacity)
            {
                var seg = new CycleBufferSegmentMPOC(capacity);

                Head   = seg;
                Reader = seg;
                Writer = seg;
            }

            // Head segment
            public CycleBufferSegmentMPOC Head;
            private long _empty00;
            private long _empty01;
            private long _empty02;
            private long _empty03;
            private long _empty04;
            private long _empty05;
            private long _empty06;
            private long _empty07;

            // Current reader segment
            public CycleBufferSegmentMPOC Reader;
            private long _empty08;
            private long _empty09;
            private long _empty10;
            private long _empty11;
            private long _empty12;
            private long _empty13;
            private long _empty14;
            private long _empty15;

            // Current writer segment
            public CycleBufferSegmentMPOC Writer;
            private long _empty16;
            private long _empty17;
            private long _empty18;
            private long _empty19;
            private long _empty20;
            private long _empty21;
            private long _empty22;
            private long _empty23;
        }

        [DebuggerDisplay("Reader:{ReaderPosition}, Writer:{WriterPosition}")]
        private sealed class CycleBufferSegmentMPOC
        {
            public CycleBufferSegmentMPOC(int capacity)
            {
                ReaderMessages = new MessageItem[capacity];
                WriterMessages = ReaderMessages;
            }

            // Reading thread position
            public int ReaderPosition;
            private long _empty00;
            private long _empty01;
            private long _empty02;
            private long _empty03;
            private long _empty04;
            private long _empty05;
            private long _empty06;
            private long _empty07;

            public readonly MessageItem[] ReaderMessages;
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

            public readonly MessageItem[] WriterMessages;
            private long _empty24;
            private long _empty25;
            private long _empty26;
            private long _empty27;
            private long _empty28;
            private long _empty29;
            private long _empty30;
            private long _empty31;

            // Next segment
            public CycleBufferSegmentMPOC Next;
            private long _empty32;
            private long _empty33;
            private long _empty34;
            private long _empty35;
            private long _empty36;
            private long _empty37;
            private long _empty38;
            private long _empty39;

            // Back segment
            public CycleBufferSegmentMPOC Back;
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

        [DebuggerDisplay("Value:{Value}, Operation:{Operation}, Next:{Next}")]
        private struct MessageItem
        {
            public T Value;
            public uint Operation;
            // Thread id that write next value
            public int Next;
        }

        #endregion
    }
}