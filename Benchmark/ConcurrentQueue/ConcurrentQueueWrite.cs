// Maksim Burtsev https://github.com/MBurtsev
// Licensed under the MIT license.

using BenchmarkDotNet.Attributes;
using DataflowBench.Helper;
using System.Collections.Concurrent;

namespace DataflowBench.ConcurrentQueue
{
    [Config(typeof(BenchConfigWithTotal))]
    public class ConcurrentQueueWrite
    {
        private const int COUNT = 10_000_000;
        private MultiThreadHelper _helper;
        private ConcurrentQueue<int> _queue;

        //[Params(1, 1, 1, 1)]
        [Params(1, 2, 4)]
        public int Threads { get; set; }

        [IterationSetup(Target = nameof(Write))]
        public void WriteSetup()
        {
            _helper = new MultiThreadHelper();
            _queue = new ConcurrentQueue<int>();

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
                _queue.Enqueue(1);
            }
        }
    }
}
