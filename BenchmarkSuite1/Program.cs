using BenchmarkDotNet.Running;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;

namespace BenchmarkSuite1
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var config = ManualConfig.Create(DefaultConfig.Instance)
                .WithOption(ConfigOptions.DisableOptimizationsValidator, true);
            var _ = BenchmarkRunner.Run(typeof(Program).Assembly, config);
        }
    }
}
