using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using DataflowChannel;
using System.Threading;
using System.Threading.Tasks;

namespace Benchmark
{
    [Config(typeof(BenchConfig))]
    public class OPOCBench
    {
        private const int COUNT = 100_000_000;
        private const int PRODUCERS = 1;
        private const int CONSUMERS = 1;
        private const int THREADS = PRODUCERS + CONSUMERS;
        private ChannelOPOC<int> _channel;

        [IterationSetup(Target = nameof(Write))]
        public void WriteSetup()
        {
            _channel = new ChannelOPOC<int>();
        }
        [Benchmark(OperationsPerInvoke = COUNT)]
        public void Write()
        {
            var ready = 0;

            for (var n = 0; n < PRODUCERS; n++)
            {
                Task.Factory.StartNew(() =>
                {
                    for (var i = 0; i < COUNT; i++)
                    {
                        _channel.Write(1);
                    }

                    Interlocked.Increment(ref ready);
                }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }

            while (Volatile.Read(ref ready) < PRODUCERS)
            {
                Thread.Yield();
            }
        }


        [IterationSetup(Target = nameof(Read))]
        public void ReadSetup()
        {
            _channel = new ChannelOPOC<int>();

            for (var i = 0; i < COUNT; i++)
            {
                _channel.Write(1);
            }
        }
        [Benchmark(OperationsPerInvoke = COUNT)]
        public void Read()
        {
            var ready = 0;

            for (var n = 0; n < CONSUMERS; n++)
            {
                Task.Factory.StartNew(() =>
                {
                    for (var i = 0; i < COUNT; i++)
                    {
                        _channel.TryRead(out _);
                    }

                    Interlocked.Increment(ref ready);
                }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }

            while (Volatile.Read(ref ready) < CONSUMERS)
            {
                Thread.Yield();
            }
        }


        [IterationSetup(Target = nameof(ReadWrite))]
        public void ReadWriteSetup()
        {
            _channel = new ChannelOPOC<int>();
        }
        [Benchmark(OperationsPerInvoke = COUNT * THREADS)]
        public void ReadWrite()
        {
            var ready = 0;

            for (var n = 0; n < PRODUCERS; n++)
            {
                Task.Factory.StartNew(() =>
                {
                    for (var i = 0; i < COUNT; i++)
                    {
                        _channel.Write(1);
                    }

                    Interlocked.Increment(ref ready);
                }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }

            for (var n = 0; n < CONSUMERS; n++)
            {
                Task.Factory.StartNew(() =>
                {
                    for (var i = 0; i < COUNT; i++)
                    {
                        _channel.TryRead(out _);
                    }

                    Interlocked.Increment(ref ready);
                }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }

            while (Volatile.Read(ref ready) < THREADS)
            {
                Thread.Yield();
            }
        }
    }
}
