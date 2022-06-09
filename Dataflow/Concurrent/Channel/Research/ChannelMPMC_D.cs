// Maksim Burtsev https://github.com/MBurtsev
// Licensed under the MIT license.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Dataflow.Concurrent.Channel_D
{
    /// <summary>
    /// MPOC - Multiple Producer One Consumer.
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
            _channel = new ChannelData();

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
                //var cur = _channel.Head;

                //while (cur != null)
                //{

                //    if (          cur.Reader.Messages != cur.Reader.WriterLink.WriterMessages
                //                                      ||
                //            cur.Reader.ReaderPosition != cur.Reader.WriterLink.WriterPosition
                //        )
                //    {
                //        return false;
                //    }

                //    cur = cur.Next;
                //}

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

                //    count += cur.Reader.WriterLink.WriterPosition - cur.Reader.ReaderPosition;

                //    seg = seg.Next;

                //    while (seg != null)
                //    {
                //        count += seg.WriterLink.WriterPosition;

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
                var channel = _channel;
                var id      = Interlocked.Add(ref channel.WriterOperation, 1);
                var buffer  = channel.WriterLinks[id % THREADS_STORAGE_SIZE];

                var pos = buffer.WriterPosition;

                if (pos == SEGMENT_CAPACITY)
                {
                    SetNextSegment(channel, id, value);

                    return;
                }

                buffer.WriterMessages[pos] = value;
                buffer.WriterPosition = pos + 1;
            }
        }

        private void SetNextSegment(ChannelData channel, int id, T value)
        {
            unchecked
            {
                var hash = id % THREADS_STORAGE_SIZE;
                var buffer  = channel.Storage[hash];
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
                var operation = Interlocked.Add(ref channel.ReaderOperation, 1);
                ref var data = ref channel.Storage[operation % THREADS_STORAGE_SIZE];
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
                if (pos != seg.WriterLink.WriterPosition)
                {
                    value = seg.Messages[pos];

                    seg.ReaderPosition = pos + 1;

                    return true;
                }

                value = default;

                return false;
            }
        }

        public void Clear()
        {

        }

        #region ' Structures '

        private sealed class ChannelData
        {
            public ChannelData()
            {
                Storage = new CycleBuffer[THREADS_STORAGE_SIZE];
                WriterLinks = new WriterLink[THREADS_STORAGE_SIZE];
            }

            // For writers
            public readonly CycleBuffer[] Storage;
            //
            public readonly WriterLink[] WriterLinks;
            // Current writer operation number
            public int WriterOperation;
            // Current reader operation number
            public int ReaderOperation;
        }

        private sealed class CycleBuffer
        {
            public CycleBuffer()
            {
                var seg = new CycleBufferSegment();

                Head = seg;
                Reader = seg;
                Writer = seg;
                Next = null;
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

                Messages = messages;
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

            // Writing thread position
            public int WriterPosition;
            // Current writer segment
            public readonly T[] WriterMessages;
        }

        #endregion
    }
}