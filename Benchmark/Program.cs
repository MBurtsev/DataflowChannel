using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using System;

namespace Benchmark
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // For debug
            //BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, new DebugInProcessConfig());

            // ChannelOPOC
            BenchmarkRunner.Run<OPOCBench>();

            // ConcurrentQueue
            //BenchmarkRunner.Run<ConcurrentQueueBench>();

            Console.WriteLine("Press any key for exit");
            Console.ReadKey();
        }
    }
}
