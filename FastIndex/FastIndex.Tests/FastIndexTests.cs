using Bogus;
using FluentAssertions;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Xunit;
using Xunit.Abstractions;

namespace FastIndex.Tests
{
    public class FastIndexTests : IClassFixture<FastIndexTestFixture>
    {
        public ITestOutputHelper OutputHelper { get; }

        private readonly Faker _faker;
        private readonly FastIndexTestFixture _fixture;

        public FastIndexTests(FastIndexTestFixture fixture, ITestOutputHelper testOutputHelper)
        {

            OutputHelper = testOutputHelper;
            _fixture = fixture;
            _faker = _fixture.Faker;
        }

        [Fact]
        public void Generate_Thousand_RandomDataItems_Succeeds()
        {
            // Arrange
            var testHelper = new FastIndexTestHelper(1000);

            // Act
            testHelper.GenerateRandomTestData(_faker);

            // Assert
            foreach(var randomData in testHelper.TestData)
            {
                randomData.Should().NotBeNull($"{nameof(testHelper.TestData)} should be set");
            }
        }

        [Fact]
        public void Generate_Thousand_RandomDataItems_FastFilter_Succeeds()
        {
            // Arrange
            var testHelper = new FastIndexTestHelper(1000);
            testHelper.GenerateRandomTestData(_faker);

            // Act
            var fastFilter = testHelper.ConstructFastFilterForTestData();

            // Assert
            foreach (var randomData in testHelper.TestData)
            {
                var contains = fastFilter.Contains(randomData.HashKey);
                contains.Should().BeTrue($"{nameof(testHelper.TestData)} should exist");
            }
        }

        [Fact]
        public void Generate_Thousand_RandomDataItems_FastFilter_IndexLookup_Succeeds()
        {
            // Arrange
            var testHelper = new FastIndexTestHelper(1000);
            testHelper.GenerateRandomTestData(_faker);
            var fastFilter = testHelper.ConstructFastFilterForTestData();
            var maxIndex = FFHelpers.CalcFingerprintArraySize(testHelper.Count, 3);

            // Act
            var indexedData = testHelper.MapTestDataToIndexedTestData(fastFilter);

            // Assert
            foreach (var randomData in testHelper.TestData)
            {
                var index = fastFilter.Index(randomData.HashKey);
                index.Should().BeGreaterThanOrEqualTo(0, $"index should exist and be in valid range 0-{maxIndex-1}");
                index.Should().BeLessThan(maxIndex, $"index should exist and be in valid range 0-{maxIndex-1}");
                randomData.Should().BeEquivalentTo(indexedData[index], "randomData lookup by its index should equal item pointed to by index");
            }
        }

        [Fact]
        public void Thousand_Random_Negative_Lookups_From_Thousand_RandomDataItems_FastFilter_Should_Not_Have_An_Index_Succeeds()
        {
            // Arrange
            var testHelper = new FastIndexTestHelper(1000);
            testHelper.GenerateRandomTestData(_faker);
            var fastFilter = testHelper.ConstructFastFilterForTestData();

            for (var i=0; i<1000; i++)
            {
                // Act
                var nonIndexedRandomData = new TestData(_faker.Phone.PhoneNumber());
                var index = fastFilter.Index(nonIndexedRandomData.HashKey);

                // Assert
                index.Should().BeLessThan(0, $"random data should not exist in the indexed TestData set");

            }
        }

        [Fact
#if DEBUG
            (Skip="Only run Performance Test in Release Build")
#endif  
        ]

        public void OneMillion_IndexLookups_Against_10Million_RandomDataItems_Succeeds()
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();

            // Arrange
            var totalLookups = 1000000;

            // Act
            var totalSuccessfullLookups = 0;

            if (FixtureNullCheck(_fixture.TestHelper))
            {
                totalSuccessfullLookups = _fixture.TestHelper.PositiveIndexLookups(_fixture, totalLookups);
            }

            // Assert
            totalSuccessfullLookups.Should().Be(totalLookups, "index lookups should match ");


            stopWatch.Stop();
            OutputHelper.WriteLine($"Timeing: {stopWatch.Elapsed}");
        }

        [Fact
#if DEBUG
            (Skip="Only run Performance Test in Release Build")
#endif
]
        public void OneMillion_ContainsLookups_Against_10Million_RandomDataItems_Succeeds()
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();

            // Arrange
            var totalLookups = 1000000;

            // Act
            var totalSuccessfullLookups = 0;

            if (FixtureNullCheck(_fixture.TestHelper))
            {
                totalSuccessfullLookups = _fixture.TestHelper.PositiveContainsLookups(_fixture, totalLookups);
            }

            // Assert
            totalSuccessfullLookups.Should().Be(totalLookups, "index lookups should match ");

            stopWatch.Stop();
            OutputHelper.WriteLine($"Timeing: {stopWatch.Elapsed}");
        }


        private static bool FixtureNullCheck([NotNullWhen(true)] FastIndexTestHelper? testHelper)
        {
            return testHelper != null;
        }
    }
}