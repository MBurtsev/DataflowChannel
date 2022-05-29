// Maksim Burtsev https://github.com/MBurtsev
// Licensed under the MIT license.

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using DataflowBench.ConcurrentQueue;
using DataflowBench.MPMC;
using DataflowBench.MPOC;
using DataflowBench.MPOCnoOrder;
using DataflowBench.Temp;
using System;

namespace DataflowBench
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // For debug
            //BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, new DebugInProcessConfig());

            // Test MultiThreadHelper vs Task.Run
            //BenchmarkRunner.Run<MPOCnoOrderWriteA>();
            //BenchmarkRunner.Run<MPOCnoOrderWriteB>();

            // ChannelOPOC
            //BenchmarkRunner.Run<OPOCBench>();

            // ChannelMPOC
            //BenchmarkRunner.Run<MPOCRead>();
            BenchmarkRunner.Run<MPOCWrite>();
            //BenchmarkRunner.Run<MPOCWriteWithReader>();

            // ChannelMPOCnoOrder
            //BenchmarkRunner.Run<MPOCnoOrderRead>();
            //BenchmarkRunner.Run<MPOCnoOrderWrite>();
            //BenchmarkRunner.Run<MPOCnoOrderWriteWithReader>();

            // ChannelMPMC
            //BenchmarkRunner.Run<MPMCRead>();
            //BenchmarkRunner.Run<MPMCWrite>();
            //BenchmarkRunner.Run<MPMCWriteWithReader>();

            // ConcurrentQueue
            //BenchmarkRunner.Run<ConcurrentQueueWrite>();

            Console.WriteLine("Press any key for exit");
            Console.ReadKey();
        }
    }
}
