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
    /// Producers use spin lock for initialization thread only once. All
    /// write operations fully lock-free\wait-free.
    /// Customer fully lock-free\wait-free.
    /// No order means that read order is not equal to write order.
    /// </summary>
    public partial class ChannelMPOCnoOrder<T>
    {
        private const int THREADS_STORAGE_SIZE = 32;
        // The default value that is used if the user has not specified a capacity.
        private const int DEFAULT_CAPACITY = 32 * 1024;
        // Current segment size
        private readonly int _capacity;
        // Channel data
        private ChannelData _channel;

        public ChannelMPOCnoOrder() : this(DEFAULT_CAPACITY)
        { 
        }

        public ChannelMPOCnoOrder(int capacity)
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
                var data = GetStorage().Data;
                var seg = data.Writer;

                if (seg.WriterPosition == _capacity)
                {
                    CycleBufferSegment next;

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

                seg.WriterMessages[seg.WriterPosition] = value;
                seg.WriterPosition++;
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
                    var seg = cur.Data.Reader;

                    if (seg.ReaderPosition == _capacity)
                    {
                        if (seg == cur.Data.Writer)
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
                            next = cur.Data.Head;
                        }

                        next.ReaderPosition = 0;

                        seg = next;

                        cur.Data.Reader = seg;
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
                newChannel.Storage[i] = new ThreadData(_capacity);
            }

            _channel = newChannel;

            // unlock
            channel.SyncChannel = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ThreadData GetStorage()
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

        private ThreadData SetupThread(int id)
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
                //// check the element in the correct place
                //else if (channel.Storage[hash].Id != 0 && channel.Storage[hash].Id != hash)
                //{
                //    var tmp = channel.Storage[hash];

                //    channel.Storage[tmp.Id] = tmp;
                //    channel.Storage[hash]   = new ThreadData(_capacity);
                //}

                var thread = current.Storage[id];

                thread.Id    = id;
                thread.Next  = current.Head;
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

        //// -----------------------------

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //private ThreadData GetStorage4(ChannelData data)
        //{
        //    unchecked
        //    {
        //        //var id = t_id;
        //        var id = Thread.CurrentThread.ManagedThreadId;
        //        var thread = data.Storage[id];

        //        if (thread.IsReady)
        //        {
        //            return thread;
        //        }

        //        return GetStorage(data, id);
        //    }
        //}

        //private ThreadData GetStorageEx4(ChannelData data, int id)
        //{
        //    unchecked
        //    {
        //        var storage = data.Storage;

        //        if (id >= storage.Length)
        //        {
        //            storage = ResizeStorage(data, id);
        //        }

        //        var thread  = storage[id];

        //        if (!thread.IsReady)
        //        {
        //            if (id == 0)
        //            {
        //                id = t_id = Interlocked.Add(ref t_ids, 1);

        //                thread = storage[id];
        //            }

        //            thread.Next = data.Head;

        //            while (Interlocked.CompareExchange(ref data.Head, thread, thread.Next) != thread.Next)
        //            {
        //                thread.Next = Volatile.Read(ref data.Head);
        //            }

        //            thread.IsReady = true;
        //        }

        //        return thread;
        //    }
        //}

        //private ThreadData[] ResizeStorage4(ChannelData data, int size)
        //{
        //    // set lock
        //    while (Interlocked.CompareExchange(ref data.SyncStorage, 1, 0) != 0)
        //    {
        //    }

        //    try
        //    {
        //        var head    = Volatile.Read(ref data.Head);
        //        var storage = Volatile.Read(ref data.Storage);

        //        var len = storage.Length;

        //        if (size < len)
        //        {
        //            return storage;
        //        }

        //        len = ((size % THREADS_STORAGE_SIZE) + 1) * THREADS_STORAGE_SIZE * 2;

        //        var newStorage = new ThreadData[len];

        //        Array.Copy(storage, newStorage, storage.Length);

        //        // fill with empty Cabinets
        //        for (var i = storage.Length; i < newStorage.Length; ++i)
        //        {
        //            newStorage[i] = new ThreadData(/*i,*/ _capacity);
        //        }

        //        // write new link
        //        Volatile.Write(ref data.Storage, newStorage);

        //        return newStorage;
        //    }
        //    finally
        //    {
        //        // unlock
        //        data.SyncStorage = 0;
        //    }
        //}

        //// ------------------------------

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //private ThreadData GetStorage3(ChannelData data)
        //{
        //    //return data.Storage[0];
        //    unchecked
        //    {
        //        var id = t_id;
        //        var storage = data.Storage;
        //        var thread = storage[id];

        //        if (!thread.IsReady)
        //        {
        //            if (id == 0)
        //            {
        //                id = t_id = Interlocked.Add(ref t_ids, 1);

        //                thread = storage[id];
        //            }

        //            if (id >= storage.Length)
        //            {
        //                storage = ResizeStorage(data, id);
        //            }

        //            thread.Next = data.Head;

        //            while (Interlocked.CompareExchange(ref data.Head, thread, thread.Next) != thread.Next)
        //            {
        //                thread.Next = Volatile.Read(ref data.Head);
        //            }

        //            thread.IsReady = true;
        //        }

        //        return thread;
        //    }
        //}

        //// -------------------------------

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //private ThreadData GetStorage2(ChannelData data)
        //{
        //    //var id = Thread.GetCurrentProcessorId();
        //    var id = t_id;
        //    var storage = data.Storage;

        //    if (id >= storage.Length || storage[id] == null)
        //    {
        //        if (id == 0)
        //        {
        //            id = t_id = Interlocked.Add(ref t_ids, 1);
        //        }

        //        storage = ResizeStorage2(data, new ThreadData(/*id, */_capacity));
        //    }

        //    return storage[id];
        //}

        //// 
        //private ThreadData[] ResizeStorage2(ChannelData data, ThreadData thread)
        //{
        //    // set lock
        //    while (Interlocked.CompareExchange(ref data.SyncStorage, 1, 0) != 0)
        //    {
        //    }

        //    try
        //    {
        //        var head    = Volatile.Read(ref data.Head);
        //        var storage = Volatile.Read(ref data.Storage);

        //        thread.Next = head;
        //        data.Head   = thread;

        //        var id = thread.Id;
        //        var len = storage.Length;

        //        if (id < len)
        //        {
        //            storage[id] = thread;

        //            return storage;
        //        }

        //        if (id > len)
        //        {
        //            len = id;
        //        }

        //        len = ((len % THREADS_STORAGE_SIZE) + 1) * THREADS_STORAGE_SIZE * 2;

        //        var newStorage = new ThreadData[len];

        //        Array.Copy(storage, newStorage, storage.Length);

        //        // setup new thread
        //        newStorage[id] = thread;

        //        // write new link
        //        Volatile.Write(ref data.Storage, newStorage);

        //        return newStorage;
        //    }
        //    finally
        //    {
        //        // unlock
        //        data.SyncStorage = 0;
        //    }
        //}

        #region ' Structures '

        private sealed class ChannelData
        {
            public ChannelData(int capacity)
            {
                Storage = new ThreadData[capacity];
                Head    = null;
                Reader  = null;
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

            // Current reader position
            public ThreadData Reader;
            private long _empty08;
            private long _empty09;
            private long _empty10;
            private long _empty11;
            private long _empty12;
            private long _empty13;
            private long _empty14;
            private long _empty15;

            // To synchronize threads when expanding the storage or setup new thread
            public int SyncChannel;
            private long _empty16;
            private long _empty17;
            private long _empty18;
            private long _empty19;
            private long _empty20;
            private long _empty21;
            private long _empty22;
            private long _empty23;

            // For writers
            public readonly ThreadData[] Storage;
            private long _empty24;
            private long _empty25;
            private long _empty26;
            private long _empty27;
            private long _empty28;
            private long _empty29;
            private long _empty30;
            private long _empty31;

            // Hash divider
            public readonly int Size;
            private long _empty32;
            private long _empty33;
            private long _empty34;
            private long _empty35;
            private long _empty36;
            private long _empty37;
            private long _empty38;
            private long _empty39;
        }

        [DebuggerDisplay("Id:{Id}")]
        private sealed class ThreadData
        {
            public ThreadData(int capacity)
            {
                Id   = 0;
                Data = new CycleBuffer(capacity);
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
            public readonly CycleBuffer Data;
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

        private sealed class CycleBuffer
        {
            public CycleBuffer(int capacity)
            {
                var seg = new CycleBufferSegment(capacity);

                Head   = seg;
                Reader = seg;
                Writer = seg;
            }

            // Head segment
            public CycleBufferSegment Head;
            private long _empty00;
            private long _empty01;
            private long _empty02;
            private long _empty03;
            private long _empty04;
            private long _empty05;
            private long _empty06;
            private long _empty07;

            // Current reader segment
            public CycleBufferSegment Reader;
            private long _empty08;
            private long _empty09;
            private long _empty10;
            private long _empty11;
            private long _empty12;
            private long _empty13;
            private long _empty14;
            private long _empty15;

            // Current writer segment
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

        [DebuggerDisplay("Reader:{ReaderPosition}, Writer:{WriterPosition}")]
        private sealed class CycleBufferSegment
        {
            public CycleBufferSegment(int capacity)
            {
                ReaderMessages = new T[capacity];
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

            public readonly T[] ReaderMessages;
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

            public readonly T[] WriterMessages;
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