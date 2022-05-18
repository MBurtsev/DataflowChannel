using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using DataflowBench.MPOCnoOrder;
using System;

namespace Benchmark
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Demonstrate BDN freeze problem
            //BenchmarkRunner.Run<Benchmark.Temp.OPOCBench>();
            BenchmarkRunner.Run<Benchmark.Temp.OPOCBenchWrite>();
            // But bench code works fine
            //var ben = new Benchmark.Temp.OPOCBench();
            //ben.ReadSetup();
            //ben.Read();

            //var ben = new Benchmark.Temp.OPOCBenchWrite();
            //ben.Write();


            // For debug
            //BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, new DebugInProcessConfig());

            // ChannelOPOC
            //BenchmarkRunner.Run<OPOCBench>();

            // ChannelMPOCnoOrder
            //BenchmarkRunner.Run<MPOCnoOrderWrite>();

            // ConcurrentQueue
            //BenchmarkRunner.Run<ConcurrentQueueBench>();

            Console.WriteLine("Press any key for exit");
            Console.ReadKey();
        }
    }
}
