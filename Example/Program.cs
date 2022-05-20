// Maksim Burtsev https://github.com/MBurtsev
// Licensed under the MIT license.

using DataflowChannel;
using System.Diagnostics;

namespace Example
{
    public class Test
    {
        static void Main()
        {
            Sample();
            //MeasureConsumer();

            Console.ReadLine();
        }

        static void Sample()
        {
            var producers = 1;
            var consumers = 0;
            var threads = producers + consumers;
            var ready = 0;
            var count = 100_000_000;
            var sw = Stopwatch.StartNew();

            // Attention: No more than one consumer and one producer
            //var channel = new ChannelOPOC<int>();

            // Attention: No more than one consumer
            //var channel = new ChannelMPOCnoOrder<int>();

            // No limits
            var channel = new ChannelMPMC<int>();
            //var channel = new DataflowChannel_B1.ChannelMPMC<int>();
            //var channel = new DataflowChannel_B0.ChannelMPMC<int>();
            //var channel = new DataflowChannel_B11.ChannelMPMC<int>();
            //var channel = new DataflowChannel_B2.ChannelMPMC<int>();
            //var channel = new DataflowChannel_A.ChannelMPMC<int>();

            if (producers == 0)
            {
                for (int i = 0; i < count; i++)
                {
                    channel.Write(i);
                }

                channel.Write(-1);
            }

            // Jit
            channel.Write(-1);
            channel.TryRead(out _);

            // Run producers
            for (var n = 0; n < producers; n++)
            {
                Task.Factory.StartNew(() =>
                {
                    Interlocked.Add(ref ready, 1);

                    while (Volatile.Read(ref ready) < threads)
                    {
                    }

                    var start = sw.ElapsedMilliseconds;

                    for (int i = 0; i < count; i++)
                    {
                        channel.Write(i);
                    }

                    channel.Write(-1);

                    Console.WriteLine($"Producer time:{sw.ElapsedMilliseconds - start}, thread:{Thread.CurrentThread.ManagedThreadId}");

                }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }

            // Run consumers
            for (var n = 0; n < consumers; n++)
            {
                Task.Factory.StartNew(() =>
                {
                    Interlocked.Add(ref ready, 1);

                    while (Volatile.Read(ref ready) < threads)
                    {
                    }

                    var sum   = 0;
                    var empty = 0;
                    var exit  = 0;
                    var start = sw.ElapsedMilliseconds;

                    while (true)
                    {
                        if (channel.TryRead(out var num))
                        {
                            if (num == -1)
                            {
                                exit++;

                                if (exit == producers || producers == 0)
                                {
                                    break;
                                }

                                continue;
                            }
                            else if (num != sum)
                            {
                                var bp = 0;
                            }

                            sum++;
                        }
                        else
                        {
                            empty++;
                        }
                    }

                    Console.WriteLine($"Consumer time:{sw.ElapsedMilliseconds - start}, thread:{Thread.CurrentThread.ManagedThreadId}, sum:{sum}, empty reads:{empty}");

                }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }
        }

        static void MeasureConsumer()
        {
            var consumers = 1;
            var threads = consumers;
            var ready = 0;
            var count = 100_000_000;
            var sw = Stopwatch.StartNew();

            //var channel = new ChannelOPOC<int>();
            var channel = new ChannelMPOCnoOrder<int>();
            //var channel = new DataflowChannelA.ChannelMPOC<int>();

            // Prepare data
            for (int i = 0; i < count * threads; i++)
            {
                channel.Write(i);
            }

            channel.Write(-1);

            // Run consumers
            for (var n = 0; n < consumers; n++)
            {
                Task.Factory.StartNew(() =>
                {
                    Interlocked.Add(ref ready, 1);

                    while (Volatile.Read(ref ready) < threads)
                    {
                    }

                    var sum   = 0;
                    var empty = 0;
                    var exit  = 0;
                    var start = sw.ElapsedMilliseconds;

                    while (true)
                    {
                        if (channel.TryRead(out var num))
                        {
                            if (num == -1)
                            {
                                exit++;

                                if (exit == threads)
                                {
                                    break;
                                }

                                continue;
                            }

                            sum++;
                        }
                        else
                        {
                            empty++;
                        }
                    }

                    Console.WriteLine($"Consumer time:{sw.ElapsedMilliseconds - start}, thread:{Thread.CurrentThread.ManagedThreadId}, sum:{sum}, empty reads:{empty}");

                }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }
        }

        static void Heap()
        {
            var channel = new ChannelOPOC<int>();

            for (var i = 0; i < 1000000; i++)
            {
                channel.Write(i);
            }

            for (var i = 0; i < 200001; i++)
            {
                if (channel.TryRead(out var tmp))
                {
                    if (tmp != i)
                    {
                        var bp = 0;
                    }
                }
                else
                {
                    var bp = 0;
                }
            }

            var c = channel.Count;
        }
    }
}