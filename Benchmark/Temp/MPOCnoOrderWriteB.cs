// Maksim Burtsev https://github.com/MBurtsev
// Licensed under the MIT license.

using BenchmarkDotNet.Attributes;
using DataflowBench.Helper;
using DataflowChannel;

namespace DataflowBench.Temp
{
    [Config(typeof(BenchConfigWithTotal))]
    public class MPOCnoOrderWriteB
    {
        private const int COUNT = 100_000_000;
        private ChannelMPOCnoOrder<int> _channel;
        private MultiThreadHelper _helper;

        [Params(1, 2, 4, 8)]
        public int Threads { get; set; }

        [IterationSetup(Target = nameof(Write))]
        public void WriteSetup()
        {
            _helper = new MultiThreadHelper();
            _channel = new ChannelMPOCnoOrder<int>();

            for (var i = 0; i < Threads; i++)
            {
                _helper.AddJob(WriteJob);
            }
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
