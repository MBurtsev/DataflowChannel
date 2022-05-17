// Maksim Burtsev https://github.com/MBurtsev
// Licensed under the MIT license.

using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Validators;
using System.Globalization;

namespace Benchmark
{
    // Settings for all multi-threaded tests
    public class BenchConfig : ManualConfig
    {
        //The number of iterations of the test method. This number greatly affects the 
        //accuracy of the results. The bigger it is, the better.
        public const int OperationsCount = 10_000_000;

        // Number of threads
        public static int[] Threads = new[]
        {
            /**/01, 02, 03, 04, 05, 06, 07, 08, 09, 10, 11, 12, 13, 14, 15, 16/**/
        };
        public BenchConfig()
        {
            //AddJob(Job.MediumRun
            //    .WithLaunchCount(1)
            //    .WithWarmupCount(3)
            //    .WithIterationCount(5)
            //    .WithInvocationCount(1)
            //    .WithUnrollFactor(1)
            //    );

            //AddColumn(StatisticColumn.Min);
            //AddColumn(StatisticColumn.Max);
            //AddColumn(StatisticColumn.OperationsPerSecond);

            //AddExporter(HtmlExporter.Default);
            //AddExporter(MarkdownExporter.GitHub);
            //AddExporter(PlainExporter.Default);

            //AddValidator(JitOptimizationsValidator.FailOnError);

            Add(Job.MediumRun
                .WithLaunchCount(1)
                .WithWarmupCount(10)
                .WithIterationCount(10)
                .WithInvocationCount(1)
                .WithUnrollFactor(1)
                );

            Add(StatisticColumn.Min);
            Add(StatisticColumn.Max);
            Add(StatisticColumn.OperationsPerSecond);

            Add(HtmlExporter.Default);
            Add(MarkdownExporter.GitHub);
            Add(PlainExporter.Default);

            Add(JitOptimizationsValidator.FailOnError);
        }
    }
}
