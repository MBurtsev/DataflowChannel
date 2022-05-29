// Maksim Burtsev https://github.com/MBurtsev
// Licensed under the MIT license.

using BenchmarkDotNet.Attributes;
using DataflowBench.Helper;
using DataflowChannel;

namespace DataflowBench.MPOC
{
    [Config(typeof(BenchConfigWithTotal))]
    public class MPOCRead
    {
        private const int COUNT = 100_000_000;
        private MultiThreadHelper _helper;
        private ChannelMPOC<int> _channel;

        // Attention: only one thread for read possible
        [Params(1, 1, 1, 1)]
        public int Threads { get; set; }

        [IterationSetup(Target = nameof(Read))]
        public void ReadSetup()
        {
            _helper = new MultiThreadHelper();
            _channel = new ChannelMPOC<int>();

            // prepere data for read
            for (var i = 0; i < COUNT; i++)
            {
                _channel.Write(1);
            }

            _helper.AddJob(ReadJob);
            _helper.WaitReady();
        }

        [Benchmark(OperationsPerInvoke = COUNT)]
        public void Read()
        {
            _helper.Start();
        }

        public void ReadJob()
        {
            for (var i = 0; i < COUNT; i++)
            {
                _channel.TryRead(out _);
            }
        }
    }
}
