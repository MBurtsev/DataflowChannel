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
            ChannelOPOC_Sample();

            Console.ReadLine();
        }

        static void ChannelOPOC_Sample()
        {
            var threads = 2;
            var ready = 0;
            var count = 100_000_000;
            var sw = Stopwatch.StartNew();
            
            var channel = new ChannelOPOC<int>();

            // Run Producer
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

            // Run Consumer
            Task.Factory.StartNew(() =>
            {
                Interlocked.Add(ref ready, 1);

                while (Volatile.Read(ref ready) < threads)
                {
                }

                var sum = 0;
                var empty = 0;
                var start = sw.ElapsedMilliseconds;

                while (true)
                {
                    if (channel.TryRead(out var num))
                    {
                        if (num == -1)
                        {
                            break;
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

        static void Heap()
        {
            //var channel = new ChannelOPOC<int>();

            //for (var i = 0; i < 1000000; i++)
            //{
            //    channel.Write(i);
            //}

            //for (var i = 0; i < 200001; i++)
            //{
            //    if (channel.TryRead(out var tmp))
            //    {
            //        if (tmp != i)
            //        {
            //            var bp = 0;
            //        }
            //    }
            //    else
            //    {
            //        var bp = 0;
            //    }
            //}

            //var c = channel.Count;
        }
    }
}