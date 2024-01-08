// See https://aka.ms/new-console-template for more information
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using FastIndex.Tests;

namespace BenchMark // Note: actual namespace depends on the project name.
{
    public class Program
    {
        static void Main(string[] args)
        {
            // see https://benchmarkdotnet.org/articles/guides/getting-started.html#step-4-run-benchmarks
            //  dotnet run -c Release --project ./BenchMark
            var summary = BenchmarkRunner.Run<BenchMarkTests>();
        }
    }

    public class BenchMarkTests
    {
        private FastIndexTestFixture _testFixture;

        public BenchMarkTests()
        {
            _testFixture = new FastIndexTestFixture(10000000);
        }

        [Benchmark]
        public void Construct_FastFilter_10_Million_Items()
        {
            var fastFilter = _testFixture.TestHelper?.ConstructFastFilterForTestData();
        }

        [Benchmark]
        public void Contains_1_Million_Lookups()
        {
            _testFixture.TestHelper?.PositiveContainsLookups(_testFixture, 1000000);
        }

        [Benchmark]
        public void Index_1_Million_Lookups()
        {
            _testFixture.TestHelper?.PositiveIndexLookups(_testFixture, 1000000);
        }
    }
}