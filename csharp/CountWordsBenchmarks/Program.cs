using BenchmarkDotNet.Running;

namespace CountWordsBenchmarks
{
    class Program
    {
        public static void Main(string[] _) =>
            BenchmarkRunner.Run<CountWords>();
    }
}
