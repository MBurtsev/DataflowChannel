// Maksim Burtsev https://github.com/MBurtsev
// Licensed under the MIT license.

using BenchmarkDotNet.Attributes;
using DataflowBench.Helper;
using DataflowChannel;
using System.Threading;
using System.Threading.Tasks;

namespace DataflowBench.Temp
{
    [Config(typeof(BenchConfigWithTotal))]
    public class MPOCnoOrderWriteA
    {
        private const int COUNT = 100_000_000;
        private ChannelMPOCnoOrder<int> _channel;

        [Params(1, 2, 4, 8)]
        public int Threads { get; set; }

        [IterationSetup(Target = nameof(Write))]
        public void WriteSetup()
        {
            _channel = new ChannelMPOCnoOrder<int>();
        }

        [Benchmark(OperationsPerInvoke = COUNT)]
        public void Write()
        {
            var ready = 0;

            // Launch consumers
            for (var n = 0; n < Threads; n++)
            {
                Task.Factory.StartNew(() =>
                {
                    for (var i = 0; i < COUNT; i++)
                    {
                        _channel.Write(1);
                    }

                    Interlocked.Increment(ref ready);
                }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }

            // Wait until all write operations are completed
            while (Volatile.Read(ref ready) < Threads)
            {
                Thread.Yield();
            }
        }
    }
}
