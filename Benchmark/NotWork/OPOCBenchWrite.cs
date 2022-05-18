﻿using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using DataflowChannel;
using System;

namespace Benchmark.Temp
{
    [Config(typeof(BenchConfig))]
    //[DisassemblyDiagnoser(printSource: true)]
    //[HardwareCounters(HardwareCounter.BranchMispredictions, HardwareCounter.BranchInstructions)]
    public class OPOCBenchWrite
    {
        private const int COUNT = 100_000_000;
        private MultiThreadHelper _bench;
        private ChannelOPOC<int> _channel;

        [IterationSetup(Target = nameof(Write))]
        public void WriteSetup()
        {
            Console.WriteLine($"Setup begin");
            Setup(1, 0);
            Console.WriteLine($"Setup done");
        }

        [Benchmark(OperationsPerInvoke = COUNT)]
        public void Write()
        {
            Console.WriteLine($"Write begin - never start");
            int b = 0;
            int a = 100 / b;
            _bench.Start();
        }
                
        private void WriteJob(ChannelOPOC<int> channel)
        {
            //var channel = _channel;

            for (int i = 0; i < COUNT; i++)
            {
                channel.Write(1);
            }
        }

        private void ReadJob(ChannelOPOC<int> channel)
        {
            //var channel = _channel;

            for (int i = 0; i < COUNT; i++)
            {
                channel.TryRead(out _);
            }
        }

        #region ' Helper '

        void Setup(int producers, int consumers)
        {
            if (_bench != null)
            {
                _bench.Stop();
            }

            _bench = new MultiThreadHelper();
            _channel = new ChannelOPOC<int>();

            var jobs = producers + consumers;

            if (producers == 0)
            {
                for (var i = 0; i < consumers * COUNT; i++)
                {
                    _channel.Write(1);
                }
            }

            // Run producers
            for (var n = 0; n < producers; n++)
            {
                _bench.AddJob(()=> WriteJob(_channel));
            }

            // Run consumers
            for (var n = 0; n < consumers; n++)
            {
                _bench.AddJob(() => ReadJob(_channel));
            }

            _bench.WaitReady();
        }

        #endregion
    }
}
