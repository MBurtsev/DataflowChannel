﻿// Maksim Burtsev https://github.com/MBurtsev
// Licensed under the MIT license.

using BenchmarkDotNet.Attributes;
using DataflowBench.Helper;
using DataflowChannel;

namespace DataflowBench.MPMC
{
    [Config(typeof(BenchConfigWithTotal))]
    public class MPMCReadWrite
    {
        private const int COUNT = 100_000_000;
        private MultiThreadHelper _helper;
        private ChannelMPMC<int> _channel;

        [Params(1, 2, 4, 8)]
        public int Threads { get; set; }

        [IterationSetup(Target = nameof(ReadWrite))]
        public void ReadWriteSetup()
        {
            _helper = new MultiThreadHelper();
            _channel = new ChannelMPMC<int>();

            for (var i = 0; i < Threads; i++)
            {
                _helper.AddJob(WriteJob);
                _helper.AddJob(ReadJob);
            }

            _helper.WaitReady();
        }

        [Benchmark(OperationsPerInvoke = COUNT * 2)]
        public void ReadWrite()
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
            for (var i = 0; i < COUNT; i++)
            {
                _channel.TryRead(out _);
            }
        }
    }
}