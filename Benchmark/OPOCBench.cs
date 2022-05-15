using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using DataflowChannel;

namespace Benchmark
{
    [Config(typeof(BenchConfig))]
    //[DisassemblyDiagnoser(printSource: true)]
    //[HardwareCounters(HardwareCounter.BranchMispredictions, HardwareCounter.BranchInstructions)]
    public class OPOCBench
    {
        private const int COUNT = 100_000_000;
        private MultiThreadBench _bench;
        private ChannelOPOC<int> _channel;

        [IterationSetup(Target = nameof(Write))]
        public void WriteSetup()
        {
            Setup(1, 0);
        }

        [Benchmark(OperationsPerInvoke = COUNT)]
        public void Write()
        {
            _bench.Start();
        }

        [IterationSetup(Target = nameof(Read))]
        public void ReadSetup()
        {
            Setup(0, 1);
        }

        [Benchmark(OperationsPerInvoke = COUNT)]
        public void Read()
        {
            _bench.Start();
        }

        [IterationSetup(Target = nameof(ReadWrite))]
        public void ReadWriteSetup()
        {
            Setup(1, 1);
        }

        [Benchmark(OperationsPerInvoke = COUNT)]
        public void ReadWrite()
        {
            _bench.Start();
        }

        private void WriteJob()
        {
            var channel = _channel;

            for (int i = 0; i < COUNT; i++)
            {
                channel.Write(1);
            }
        }

        private void ReadJob()
        {
            var channel = _channel;

            for (int i = 0; i < COUNT; i++)
            {
                channel.TryRead(out _);
            }
        }

        #region ' Helper '

        void Setup(int producers, int consumers)
        {
            _bench = new MultiThreadBench();
            _channel = new ChannelOPOC<int>();

            var jobs = producers + consumers;

            if (producers == 0)
            {
                for (var i = 0; i < consumers * jobs; i++)
                {
                    _channel.Write(1);
                }
            }

            // Run producers
            for (var n = 0; n < producers; n++)
            {
                _bench.AddJob(WriteJob);
            }

            // Run consumers
            for (var n = 0; n < consumers; n++)
            {
                _bench.AddJob(ReadJob);
            }

            _bench.WaitReady();
        }

        #endregion
    }
}
