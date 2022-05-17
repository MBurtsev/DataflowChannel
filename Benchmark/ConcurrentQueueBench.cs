using BenchmarkDotNet.Attributes;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Benchmark
{
    [Config(typeof(BenchConfig))]
    public class ConcurrentQueueBench
    {
        private const int COUNT = 100_000_000;
        private const int THREADS = 2;
        private ConcurrentQueue<int> _queue;

        [IterationSetup(Target = nameof(Enqueue))]
        public void EnqueueSetup()
        {
            _queue = new ConcurrentQueue<int>();
        }

        [Benchmark(OperationsPerInvoke = COUNT * THREADS)]
        public void Enqueue()
        {
            var ready = 0;

            for (var n = 0; n < THREADS; n++)
            {
                Task.Factory.StartNew(() =>
                {
                    for (var i = 0; i < COUNT; i++)
                    {
                        _queue.Enqueue(1);
                    }

                    Interlocked.Increment(ref ready);
                }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }

            while(Volatile.Read(ref ready) < THREADS)
            {
                Thread.Yield();
            }
        }
    }
}
