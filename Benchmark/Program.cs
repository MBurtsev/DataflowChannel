// Maksim Burtsev https://github.com/MBurtsev
// Licensed under the MIT license.

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using DataflowBench.MPMC;
using DataflowBench.MPOCnoOrder;
using DataflowBench.Temp;
using System;

namespace DataflowBench
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Temp
            //BenchmarkRunner.Run<MPOCnoOrderWriteA>();
            //BenchmarkRunner.Run<MPOCnoOrderWriteB>();

            // For debug
            //BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, new DebugInProcessConfig());

            // ChannelOPOC
            //BenchmarkRunner.Run<OPOCBench>();

            // ChannelMPOCnoOrder
            //BenchmarkRunner.Run<MPOCnoOrderRead>();
            //BenchmarkRunner.Run<MPOCnoOrderWrite>();
            BenchmarkRunner.Run<MPOCnoOrderWriteWithReader>();

            // ChannelMPMC
            //BenchmarkRunner.Run<MPMCRead>();
            //BenchmarkRunner.Run<MPMCWrite>();
            //BenchmarkRunner.Run<MPMCReadWrite>();

            // ConcurrentQueue
            //BenchmarkRunner.Run<ConcurrentQueueBench>();

            Console.WriteLine("Press any key for exit");
            Console.ReadKey();
        }
    }
}
