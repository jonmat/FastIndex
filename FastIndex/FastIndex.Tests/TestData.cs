using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO.Hashing;
using System.Text;

namespace FastIndex.Tests
{
    public class TestData : IEquatable<TestData>
    {
        public string Message { get; }
        public UInt32 HashKey { get; }


        public TestData(string message)
        {
            Message = message;
            HashKey = XxHash32.HashToUInt32(Encoding.UTF8.GetBytes(message));
        }

        public bool Equals(TestData? other)
        {
            if (other == null)
            {
                return false;
            }

            return Message.Equals(other.Message);
        }
    }

    public class TestDataEqualityComparer : IEqualityComparer<TestData>
    {
        public bool Equals(TestData? x, TestData? y)
        {
            if (x == null && y == null)
                return true;
            else if (x == null || y == null)
                return false;

            return x.Equals(y);
        }

        public int GetHashCode([DisallowNull] TestData obj) => (int)obj.HashKey;

        public static TestDataEqualityComparer Instance { get; } = new TestDataEqualityComparer();
    }
}
