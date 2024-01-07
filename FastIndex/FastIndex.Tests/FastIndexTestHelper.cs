using Bogus;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FastIndex.Tests
{
    public class FastIndexTestHelper
    {
        public int Count { get; }
        public TestData[] TestData { get; }

        public TestData[]? IndexedTestData { get; set; }

        private HashSet<TestData> _testDataHashSet;

        public FastIndexTestHelper(int count)
        {
            Count = count;
            TestData = new TestData[Count];
            _testDataHashSet = new HashSet<TestData>(TestDataEqualityComparer.Instance);
        }

        public void GenerateRandomTestData(Faker faker)
        {
            /// note: avoid duplicates, dedupe managed up front... as this is only for testing purposes.
            /// 
            /// but, for realworld scenarios with necessary possibility of duplicate hashing key, each TestData
            /// item instead would point to an array of data items that shared identical hash keys,
            /// and dedupe would occur after the FastFilter index lookup.

            int totalAdded = 0;
            while (totalAdded < Count)
            {
                var testData = new TestData(faker.Phone.PhoneNumber());
                if (_testDataHashSet.Add(testData))
                {
                    TestData[totalAdded++] = testData;
                }
            }
        }

        public FastFilterForTestData ConstructFastFilterForTestData()
        {
            var fastFilterConstructor = new FastFilterConstructor<TestData, Finger32, UInt32>(Count, 3);

            var hashKeys = _testDataHashSet.Select(x => (ulong) x.HashKey).ToHashSet();
            var fastFilter = fastFilterConstructor.Construct(hashKeys);

            // note, save these params to persistent store for quick reconstruction
            return new FastFilterForTestData(fastFilter.Data, fastFilter.Seed, fastFilter.NumHashes, fastFilter.IQIndexXor);
        }

        public TestData[] MapTestDataToIndexedTestData (FastFilterForTestData fastFilter)
        {
            var indexedTestData = new TestData[FFHelpers.CalcFingerprintArraySize(Count, 3)];

            foreach(var testData in TestData)
            {
                var i = fastFilter.Index(testData.HashKey);
                indexedTestData[i] = testData;
            }

            return indexedTestData;
        }

        public int PositiveIndexLookups(FastIndexTestFixture fixture, int totalLookups)
        {
            var fastFilter = fixture.FastFilterForTestData;
            var faker = fixture.Faker;

            var totalSuccessfullLookups = 0;

            for (var i = 0; i < totalLookups; i++)
            {
                var randomValidData = faker.PickRandom(TestData);
                if (fastFilter?.Index(randomValidData.HashKey) >= 0)
                {
                    totalSuccessfullLookups++;
                }
            }

            return totalSuccessfullLookups;
        }

        public int PositiveContainsLookups(FastIndexTestFixture fixture, int totalLookups)
        {
            var fastFilter = fixture.FastFilterForTestData;
            var faker = fixture.Faker;

            var totalSuccessfullLookups = 0;

            for (var i=0; i < totalLookups; i++)
            {
                var randomValidData = faker.PickRandom(TestData);
                if (fastFilter?.Contains(randomValidData.HashKey) ?? false)
                {
                    totalSuccessfullLookups++;
                }
            }

            return totalSuccessfullLookups;
        }
    }

    public class FastFilterForTestData : FastFilter<TestData, Finger32, UInt32>
    {
        public FastFilterForTestData(IList<uint> fingerprints, ulong seed, int numHashes, byte[] iQIndexXor)
            : base(fingerprints, seed, numHashes, iQIndexXor)
        {
        }
    }

    public class FastIndexTestFixture
    {
        public Faker Faker { get; } = new("en");

        public FastIndexTestHelper? TestHelper { get; }

        public FastFilterForTestData? FastFilterForTestData { get; }


        public FastIndexTestFixture()
        {

#if !DEBUG
            TestHelper = new FastIndexTestHelper(250000);
            TestHelper.GenerateRandomTestData(Faker);
            FastFilterForTestData = TestHelper.ConstructFastFilterForTestData();
#else
            TestHelper = null;
            FastFilterForTestData = null;
#endif

        }

        static FastIndexTestFixture()
        {
            // for reproducability, fix random seed for Bogus Faker global randomizer...
            Randomizer.Seed = new System.Random(1234567);
        }
    }
}
