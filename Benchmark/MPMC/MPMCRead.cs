// Maksim Burtsev https://github.com/MBurtsev
// Licensed under the MIT license.

using BenchmarkDotNet.Attributes;
using Dataflow.Concurrent.Channel_B2;
using DataflowBench.Helper;

namespace DataflowBench.MPMC
{
    [Config(typeof(BenchConfigWithTotal))]
    public class MPMCRead
    {
        private const int COUNT = 100_000_000;
        private MultiThreadHelper _helper;
        private ChannelMPMC<int> _channel;

        [Params(1)]
        //[Params(1, 2, 4, 8)]
        public int Threads { get; set; }
        
        [IterationSetup(Target = nameof(Read))]
        public void ReadSetup()
        {
            _helper = new MultiThreadHelper();
            _channel = new ChannelMPMC<int>();

            for (var i = 0; i < Threads; i++)
            {
                _helper.AddJob(ReadJob);
            }

            // prepere data for read
            for (var i = 0; i < COUNT * Threads; i++)
            {
                _channel.Write(1);
            }

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
