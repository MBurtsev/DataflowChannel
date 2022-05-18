// Maksim Burtsev https://github.com/MBurtsev
// Licensed under the MIT license.

using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Validators;

namespace DataflowBench.Helper
{
    public class BenchConfigWithTotal : ManualConfig
    {
        public BenchConfigWithTotal()
        {
            AddJob(Job.MediumRun
                .WithLaunchCount(1)
                .WithWarmupCount(3)
                .WithIterationCount(5)
                .WithInvocationCount(1)
                .WithUnrollFactor(1)
                );

            AddColumn(StatisticColumn.Min);
            AddColumn(StatisticColumn.Max);
            AddColumn(StatisticColumn.OperationsPerSecond);
            AddColumn(new TotalOpColumn());

            AddExporter(HtmlExporter.Default);
            AddExporter(MarkdownExporter.GitHub);
            AddExporter(PlainExporter.Default);

            AddValidator(JitOptimizationsValidator.FailOnError);
        }
    }
}
