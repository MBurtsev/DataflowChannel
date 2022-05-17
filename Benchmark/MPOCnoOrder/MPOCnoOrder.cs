using Benchmark;
using BenchmarkDotNet.Attributes;
using DataflowChannel;
using System.Threading;
using System.Threading.Tasks;

namespace DataflowBench.MPOCnoOrder
{
    [Config(typeof(BenchConfig))]
    public class MPOCnoOrderWrite
    {
        private const int COUNT = 100_000_000;
        private ChannelMPOCnoOrder<int> _channel;

        [Params(1,2,4)]
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

            while (Volatile.Read(ref ready) < Threads)
            {
                Thread.Yield();
            }
        }
    }
}
