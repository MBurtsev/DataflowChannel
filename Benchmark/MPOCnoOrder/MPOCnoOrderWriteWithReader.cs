// Maksim Burtsev https://github.com/MBurtsev
// Licensed under the MIT license.

using BenchmarkDotNet.Attributes;
using DataflowBench.Helper;
using DataflowChannel;

namespace DataflowBench.MPOCnoOrder
{
    [Config(typeof(BenchConfigWithTotal))]
    public class MPOCnoOrderWriteWithReader
    {
        private const int COUNT = 100_000_000;
        private MultiThreadHelper _helper;
        private ChannelMPOCnoOrder<int> _channel;

        //[Params(1, 1, 1, 1)]
        [Params(1, 2, 4, 8)]
        public int Threads { get; set; }

        [IterationSetup(Target = nameof(WriteWithReader))]
        public void WriteSetup()
        {
            _helper = new MultiThreadHelper();
            _channel = new ChannelMPOCnoOrder<int>();

            for (var i = 0; i < Threads; i++)
            {
                _helper.AddJob(WriteJob);
            }

            _helper.AddBackgroundJob(ReadJob);
            _helper.WaitReady();
        }

        [Benchmark(OperationsPerInvoke = COUNT)]
        public void WriteWithReader()
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

        public void ReadJob()
        {
            _channel.TryRead(out _);
        }
    }
}
