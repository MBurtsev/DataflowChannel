using BenchmarkDotNet.Running;
using System;

namespace Benchmark
{
    public class Program
    {
        public static void Main(string[] args)
        {
            //var ben = new OPOCBench();
            //ben.ReadSetup();
            //ben.Read();
            //ben.WriteSetup();
            //ben.Write();
            //ben.ReadWriteSetup();
            //ben.ReadWrite();

            BenchmarkRunner.Run<OPOCBench>();

            Console.WriteLine("Press any key for exit");
            Console.ReadKey();
        }
    }
}
