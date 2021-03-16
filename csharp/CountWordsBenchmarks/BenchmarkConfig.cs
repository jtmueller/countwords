using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;

namespace CountWordsBenchmarks
{
    internal class BenchmarkConfig : ManualConfig
    {
        public BenchmarkConfig()
        {
            AddJob(Job.Default
                .WithRuntime(CoreRuntime.Core50)
                .WithPlatform(Platform.X64)
                .WithJit(Jit.RyuJit));
            AddDiagnoser(MemoryDiagnoser.Default);
            AddExporter(MarkdownExporter.GitHub);
            AddLogger(ConsoleLogger.Default);
        }
    }
}
