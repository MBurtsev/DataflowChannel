﻿// Maksim Burtsev https://github.com/MBurtsev
// Licensed under the MIT license.

using BenchmarkDotNet.Attributes;
using DataflowBench.Helper;
using DataflowChannel;

namespace DataflowBench.MPOC
{
    [Config(typeof(BenchConfigWithTotal))]
    public class MPOCWrite
    {
        private const int COUNT = 100_000_000;
        private MultiThreadHelper _helper;
        private ChannelMPOC<int> _channel;

        [Params(1, 1, 1)]
        //[Params(1, 2, 4, 8)]
        public int Threads { get; set; }

        [IterationSetup(Target = nameof(Write))]
        public void WriteSetup()
        {
            _helper = new MultiThreadHelper();
            _channel = new ChannelMPOC<int>();

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
