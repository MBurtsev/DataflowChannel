// Maksim Burtsev https://github.com/MBurtsev
// Licensed under the MIT license.

using BenchmarkDotNet.Attributes;
using DataflowBench.Helper;
using DataflowChannel;

namespace DataflowBench.MPMC
{
    [Config(typeof(BenchConfigWithTotal))]
    public class MPMCWrite
    {
        private const int COUNT = 10_000_000;
        private MultiThreadHelper _helper;
        private ChannelMPMC<int> _channel;

        [Params(1, 2, 4, 8)]
        public int Threads { get; set; }

        [IterationSetup(Target = nameof(Write))]
        public void WriteSetup()
        {
            _helper = new MultiThreadHelper();
            _channel = new ChannelMPMC<int>();

            for (var i = 0; i < Threads; i++)
            {
                _helper.AddJob(WriteJob);
            }

            _helper.WaitReady();
        }

        [Benchmark(OperationsPerInvoke = COUNT)]
        public void Write()
        {
            _helper.Start();
        }

        public void WriteJob()
        {
            for (var i = 0; i < COUNT; i++)
            {
                _channel.Write(1);
            }
        }
    }
}
