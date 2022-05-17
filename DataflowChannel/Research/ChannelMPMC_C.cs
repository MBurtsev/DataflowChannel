// Maksim Burtsev https://github.com/MBurtsev
// Licensed under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace DataflowChannel_C
{
    /// <summary>
    /// MPOC - Multiple Producer Multiple Consumer.
    /// At the core a cycle buffer that implements a producer-consumer pattern. 
    /// Producers use spin lock for initialization thread only once. All
    /// write operations fully lock-free\wait-free.
    /// Customer fully lock-free\wait-free.
    /// No order means that read order is not equal to write order.
    /// </summary>
    public partial class ChannelMPMC<T>
    {
        private const int DATA_CAPACITY = 1024;
        // Current segment size
        private ChannelData _channel;
        private int _val;

        public ChannelMPMC() 
        { 
            _channel = new ChannelData(DATA_CAPACITY);
        }

        public bool IsEmpty
        {
            get
            {


                return true;
            }
        }

        public int Count
        {
            get
            {
                var count = 0;

                return count;
            }
        }

        public void Write(T value)
        {
            unchecked
            {
                var channel = _channel;
                var opr = Interlocked.Add(ref channel.Operation, 1);
                var ind = opr % DATA_CAPACITY;
                //var cur = channel.Storage[ind];

                channel.Storage[ind] = value;
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

        }

        #region ' Structures '

        private sealed class ChannelData
        {
            public ChannelData(int capacity)
            {
                Storage = new T[capacity];
            }

            public int Operation;
            public readonly T[] Storage;
        }

        private sealed class Node
        {
            public T Value;
            public Node Next;
        }

        #endregion
    }
}