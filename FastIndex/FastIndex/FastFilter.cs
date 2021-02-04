///
/// Copyright (C) 2021 Jon Matousek, All Rights Reserverd, Worldwide
/// 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

using Light.GuardClauses;
using static FastIndex.FFHelpers;

namespace FastIndex
{
    public interface IContains<T> where T : class
    {
        bool Contains(ulong key);
    }


    /// <summary>
    /// This interface allows for working around C#'s current limitation for ad-hoc polymorphism, its inability to correctly leverage a number-type when used as a generic's parameter
    /// and then doing math operations with that generic's numeric-type paramater within code.
    /// The approach is borrowed from https://github.com/louthy/language-ext?#ad-hoc-polymorphism, see Num<A> example.
    /// </summary>
    /// <typeparam name="NUM">Should be one of the following numeric types: Byte, UInt16, UInt32. In practice, use one of the concrete Finger8, Finger16, or Finger32, defined below.</typeparam>
    public interface IFingerCalcs<NUM>
        where NUM : struct,
          IComparable,
          IComparable<NUM>,
          IConvertible,
          IEquatable<NUM>,
          IFormattable
    {
        NUM ToFingerPrintType(ulong hash);
        NUM XOR(NUM A, NUM B);
        bool Equals(NUM A, NUM B);
        int Bits();
    }

    public struct Finger8 : IFingerCalcs<Byte>
    {
        public Byte ToFingerPrintType(ulong hash) => (Byte)hash;
        public Byte XOR(Byte A, Byte B) => (Byte)(A ^ B);
        public bool Equals(Byte A, Byte B) => A == B;
        public int Bits() => sizeof(Byte) * 8;
    }

    public struct Finger16 : IFingerCalcs<UInt16>
    {
        public UInt16 ToFingerPrintType(ulong hash) => (UInt16)hash;
        public UInt16 XOR(UInt16 A, UInt16 B) => (UInt16)(A ^ B);
        public bool Equals(UInt16 A, UInt16 B) => A == B;
        public int Bits() => sizeof(UInt16) * 8;
    }

    public struct Finger32 : IFingerCalcs<UInt32>
    {
        public UInt32 ToFingerPrintType(ulong hash) => (UInt32)hash;
        public UInt32 XOR(UInt32 A, UInt32 B) => (UInt32)(A ^ B);
        public bool Equals(UInt32 A, UInt32 B) => A == B;
        public int Bits() => sizeof(UInt32) * 8;
    }

    /// <summary>
    /// FastFilter - Probabilistic datastructure from accademia, using carefully constructed Xor signature to validate a 64 bit key's likely existance
    /// code loosely based on https://github.com/FastFilter/xorfilter
    /// Academic paper - Xor Filters: Faster and Smaller Than Bloom and Cuckoo Filters
    /// authord by Thomas Mueller Graf, Daniel Lemire https://arxiv.org/abs/1912.08258
    /// </summary>
    /// <typeparam name="T">"Marker Type" - Generic type T serves the purpose of tagging this class with the logical-type of entity keys we're working with</typeparam>
    /// <typeparam name="IFinger">IFinger interface for required calculations made with NUM type</typeparam>
    /// <typeparam name="NUM">NUM type is the parametrized size of the finger, must be unsigned: Byte, UInt16, UInt32 - the more bits, the less false-positive collisions</typeparam>
    /// 
    public class FastFilter<T, IFinger, NUM> : IContains<T>
        where T : class

        // it really is a number, the following constraints work around lack of C# ad-hoc polymorphism
        where IFinger : struct, IFingerCalcs<NUM>
        where NUM : struct,
          IComparable,
          IComparable<NUM>,
          IConvertible,
          IEquatable<NUM>,
          IFormattable
    {
        public ulong Seed { get; }
        public NUM[] Data { get; }
        public int NumHashes { get; }

        private readonly static IFinger ICall = default;   // IFinger interface calls will be removed by the compiler's optimizer on release builds.

        private readonly Func<ulong, ulong> _hasher;

        private readonly int _xorHashSegmentSize;

        private readonly ArraySegment<NUM>[] _fingerprintXorHashSegment;

        public FastFilter(IList<NUM> fingerprints, ulong seed, int numHashes)
            : this(fingerprints, seed, numHashes, FNVHash)
        {
        }

        public FastFilter(IList<NUM> fingerprintXorHash, ulong seed, int numHashes, Func<ulong, ulong> hasher)
        {
            (typeof(NUM) == typeof(Byte) || typeof(NUM) == typeof(UInt16) || typeof(NUM) == typeof(UInt32)).MustBe(true);
            fingerprintXorHash.MustNotBeNull();
            hasher.MustNotBeNull();

            _hasher = hasher;

            Seed = seed;
            Data = fingerprintXorHash.GetType().IsArray ? (NUM[])fingerprintXorHash : fingerprintXorHash.ToArray();
            NumHashes = numHashes;

            var fingerprintArraySize = fingerprintXorHash.Count;
            _xorHashSegmentSize = fingerprintArraySize / numHashes;

            _fingerprintXorHashSegment = new ArraySegment<NUM>[numHashes];
            for (var i = 0; i < numHashes; i++)
            {
                _fingerprintXorHashSegment[i] = new ArraySegment<NUM>(Data, _xorHashSegmentSize * i, _xorHashSegmentSize);
            }

        }

        public int FingerprintBitSize { get; } = ICall.Bits();

        /// <summary>
        /// Probabilistic set containment, based on fingerprints.
        /// </summary>
        /// <param name="key"></param>
        /// <returns>true if the key was in the original set, has a known false positive rate</returns>
        public bool Contains(ulong key)
        {
            var hash = _hasher(key + Seed);

            var fingerprint = ICall.ToFingerPrintType(hash ^ (hash >> XorHashShift));

            // NOTE - only perf test this on a Release build!
            var fingerprintXor = _fingerprintXorHashSegment[0][XorHashIndexers[0](hash, _xorHashSegmentSize)];
            for (var i = 1; i < _fingerprintXorHashSegment.Length; i++)
            {
                fingerprintXor = ICall.XOR(fingerprintXor, _fingerprintXorHashSegment[i][XorHashIndexers[i](hash, _xorHashSegmentSize)]);
            }

            return ICall.Equals(fingerprint, fingerprintXor);
        }

    }

    /// <summary>
    /// FastFilterConstructor is a factory used to create a FastFilter from a given set of Keys.
    /// </summary>
    /// <typeparam name="T">"Marker Type" - Generic type T serves the purpose of tagging this class with the logical-type of entity keys we're working with</typeparam>
    /// <typeparam name="IFinger">IFinger interface for required math calculations made with NUM type</typeparam>
    /// <typeparam name="NUM">NUM type is the parametrized size of the finger, must be unsigned: Byte, UInt16, UInt32 - the more bits, the less false-positive collisions</typeparam>
    public class FastFilterConstructor<T, IFinger, NUM>
        where T : class

        // it really is a number, the following constraints work around lack of C# ad-hoc polymorphism
        where IFinger : struct, IFingerCalcs<NUM>
        where NUM : struct,
          IComparable,
          IComparable<NUM>,
          IConvertible,
          IEquatable<NUM>,
          IFormattable
    {
        private readonly static IFinger ICall = default;   // IFinger interface calls will be removed by the compiler's optimizer on release builds.

        private readonly Func<ulong, ulong> _hasher;

        private readonly Random Random = new Random();
        private Stack<(ulong keyHash, int absoluteIndex)> _discoveryStack = null;
        private HashKeyCounter[][] _keyCounters = null;
        private readonly Queue<(ulong, int)>[] _noHashCollisionQs = null;
        private readonly int _numHashes;

        private readonly int[,] QSegmentMap = null;

        public FastFilterConstructor(int expectedKeyLength, int numHashes)
            : this(expectedKeyLength, numHashes, FNVHash)
        {
        }

        public FastFilterConstructor(int expectedKeyLength, int numHashes, Func<ulong, ulong> hasher)
        {
            // code only supports these types
            (typeof(NUM) == typeof(Byte) || typeof(NUM) == typeof(UInt16) || typeof(NUM) == typeof(UInt32)).MustBe(true);

            expectedKeyLength.MustBeGreaterThan(0);
            numHashes.MustBeGreaterThan(1);
            _hasher = hasher;
            _hasher.MustNotBeNull();

            _numHashes = numHashes;
            var expectedFingerprintArraySize = CalcFingerprintArraySize(expectedKeyLength, _numHashes);
            int expectedXorHashSegmentSize = expectedFingerprintArraySize / _numHashes;

            // allocate once, this constructor is designed to be reused, if necessary.
            _discoveryStack = new Stack<(ulong keyHash, int absoluteIndex)>(expectedKeyLength);
            _keyCounters = new HashKeyCounter[_numHashes][];
            _noHashCollisionQs = new Queue<(ulong, int)>[_numHashes];
            for (var i = 0; i < _noHashCollisionQs.Length; i++)
            {
                _keyCounters[i] = new HashKeyCounter[expectedXorHashSegmentSize];
                _noHashCollisionQs[i] = new Queue<(ulong, int)>(expectedXorHashSegmentSize);
            }

            // QSegmentMap points to "other" queues used to make a particular fingerprint
            QSegmentMap = new int[_numHashes, _numHashes - 1];
            for (var i = 0; i < _numHashes; i++)
            {
                var k = 0;
                for (var j = 0; j < _numHashes; j++)
                {
                    if (i == j) continue;
                    QSegmentMap[i, k++] = j;
                }
            }
        }

        // Help GC along
        public void Clear()
        {
            _discoveryStack.Clear();
            _discoveryStack = null;
            for (var i = 0; i < _noHashCollisionQs.Length; i++)
            {
                _noHashCollisionQs[i].Clear();
                _noHashCollisionQs[i] = null;
            }
            _keyCounters = null;
        }

        // Algorithm 2 - Construction
        public FastFilter<T, IFinger, NUM> Construct(HashSet<ulong> keys /* HashSet guarantees uniqueness, a required property for Construction */)
        {
            var randomSeed = Random.NextULong();
            return Construct(keys, randomSeed);
        }

        public FastFilter<T, IFinger, NUM> Construct(HashSet<ulong> keys, ulong seed)
        {
            int fingerprintArraySize = CalcFingerprintArraySize(keys.Count, _numHashes);
            int xorHashSegmentSize = fingerprintArraySize / _numHashes;

            // we're going to need a bigger block?
            if (_keyCounters[0].Length < xorHashSegmentSize)
            {
                // random distribution typically results in < expectedXorHashArraySize, but if we hit this a lot may want to add buffer amount.
                for (var i = 0; i < _keyCounters.Length; i++)
                {
                    _keyCounters[i] = new HashKeyCounter[xorHashSegmentSize];
                }
            }

            bool success;
            do
            {
                seed = _hasher(seed);
                success = GeneratePerfectHash(keys, seed, xorHashSegmentSize);
            } while (!success);

            var fingerprints = GenerateKeyFingerprints(fingerprintArraySize, xorHashSegmentSize);

            return new FastFilter<T, IFinger, NUM>(fingerprints, seed, _numHashes);
        }

        private static HashKeyCounter EmptyKeyCount = new HashKeyCounter();

        private struct HashKeyCounter
        {
            // when total keys == 1, the multiplexed value, via Xor overlay, is the actual value of remaining keyHash.
            public ulong XorMultiplexKeys;
            public short TotalKeys;

            public void Remember(ulong keyHash)
            {
                TotalKeys++;
                XorMultiplexKeys ^= keyHash;
            }

            public void Forget(ulong keyHash)
            {
                TotalKeys--;
                XorMultiplexKeys ^= keyHash;
            }
        }

        // Algorithm 3 - Generate Perfect Hash
        private bool GeneratePerfectHash(HashSet<ulong> keys, ulong seed, int xorHashSegmentSize)
        {
            _discoveryStack.Clear();

            foreach (var q in _noHashCollisionQs)
            {
                q.Clear();
            }

            // reset key counters
            foreach (var keyCounter in _keyCounters)
            {
                Array.Fill(keyCounter, EmptyKeyCount, 0, xorHashSegmentSize);
            }

            // init all key counters with hash(key+seed).
            // add hashed keys to each xor Block Counter.
            foreach (var key in keys)
            {
                ulong keyHash = FNVHash(key + seed);
                for (var i=0; i < _keyCounters.Length; i++)
                {
                    _keyCounters[i][XorHashIndexers[i](keyHash, xorHashSegmentSize)].Remember(keyHash);
                }
            }
            
            // search for hashKeys that have no collisions implied by hashCounter.TotalKeys == 1, and remember their index position within the respective sub-block.
            for (var i = 0; i < xorHashSegmentSize; i++)
            {
                for (var iQ = 0; iQ < _noHashCollisionQs.Length; iQ++)
                {
                    HashKeyCounter hashCounter = _keyCounters[iQ][i];
                    if (hashCounter.TotalKeys == 1)
                    {
                        _noHashCollisionQs[iQ].Enqueue((hashCounter.XorMultiplexKeys, i));
                    }
                }
            }

            while (_noHashCollisionQs.Any(Q => Q.Count > 0))
            {
                // walk and drain the Qs onto the discoveryStack.
                for (int iQ=0; iQ < _noHashCollisionQs.Length; iQ++)
                {
                    var Q = _noHashCollisionQs[iQ];
                    while (Q.Count > 0)
                    {
                        (var keyHash, var index) = Q.Dequeue();

                        if (_keyCounters[iQ][index].TotalKeys == 0) continue;

                        _discoveryStack.Push((keyHash, index + iQ * xorHashSegmentSize));

                        for (var i = 0; i < QSegmentMap.GetLength(1); i++)
                        {
                            var iQMap = QSegmentMap[iQ, i];
                            var j = XorHashIndexers[iQMap](keyHash, xorHashSegmentSize);

                            _keyCounters[iQMap][j].Forget(keyHash);

                            if (_keyCounters[iQMap][j].TotalKeys == 1)
                            {
                                _noHashCollisionQs[iQMap].Enqueue((_keyCounters[iQMap][j].XorMultiplexKeys, j));
                            }
                        }
                    }
                }
            }

            // truth - success when all respective hashed keys have made their way into the discoveryStack.
            return _discoveryStack.Count == keys.Count;
        }

        /// Algorithm 4 - Generate Fingerprints
        private NUM[] GenerateKeyFingerprints(int fingerprintArraySize, int xorHashSegmentSize)
        {
            var fingerprintXorHash = new NUM[fingerprintArraySize];

            var fingerprintXorHashSegment = new ArraySegment<NUM>[_numHashes];
            for (var i = 0; i < _numHashes; i++)
            {
                fingerprintXorHashSegment[i] = new ArraySegment<NUM>(fingerprintXorHash, xorHashSegmentSize * i, xorHashSegmentSize);
            }

            foreach ((var keyHash, var absoluteIndex) in _discoveryStack)
            {
                // use absoluteIndex's value to identify from which QSegment the original "non-collision" hash value was saved in.
                int iQ;
                int xorHashSegmentCheck;
                for (iQ = 0, xorHashSegmentCheck = xorHashSegmentSize; iQ < QSegmentMap.GetLength(1); iQ++, xorHashSegmentCheck += xorHashSegmentSize)
                {
                    if (absoluteIndex < xorHashSegmentCheck)
                    {
                        break;
                    }
                }

                // create fingerprint and save
                var fingerprintXor = ICall.ToFingerPrintType(keyHash ^ (keyHash >> XorHashShift));
                var iQXor = iQ;
                for (var i = 0; i < QSegmentMap.GetLength(1); i++)
                {
                    var iQMap = QSegmentMap[iQ, i];
                    var hashIndex = XorHashIndexers[iQMap](keyHash, xorHashSegmentSize);
                    fingerprintXor = ICall.XOR(fingerprintXor, fingerprintXorHashSegment[iQMap][hashIndex]);
                }

                fingerprintXorHash[absoluteIndex] = fingerprintXor;
            }

            return fingerprintXorHash;
        }
    }

    public static partial class FFHelpers
    {
        public const int XorHashShift = 32;

        public const int DefaultNumHashes = 3;      // Original design uses 3 xor hashes. Anything outside of that becomes experimental.
        public const double ExpansionFactor = 1.23; // allotted fingerprint space needs to be sparser than key density, for algorithm 3 to succeed. Value was taken from the article.

        public static readonly Func<ulong, int, int>[] XorHashIndexers =
        {
            XorHashIndexNoRotate,
            (ulong hash, int xorHashSegmentSize) => XorHashIndex(hash, 21, xorHashSegmentSize),
            (ulong hash, int xorHashSegmentSize) => XorHashIndex(hash, 43, xorHashSegmentSize)
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong NextULong(this Random random) => (((ulong)(uint)random.Next()) << 32) | (uint)random.Next();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CalcFingerprintArraySize(int totalKeys, int numHashes) => (32 + (int)(ExpansionFactor * totalKeys + 0.99999)) / numHashes * numHashes;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int XorHashIndex(ulong hash, int rotateLeft, int xorHashSegmentSize) => hash.RotateLeft(rotateLeft).Modulo(xorHashSegmentSize);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int XorHashIndexNoRotate(ulong hash, int xorHashSegmentSize) => hash.Modulo(xorHashSegmentSize);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        // http://lemire.me/blog/2016/06/27/a-fast-alternative-to-the-modulo-reduction/
        public static int Modulo(this ulong hash, int n) => (int) (((uint) hash * (ulong) n) >> 32);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong RotateLeft(this ulong value, int count) => (value >> -count) | (value << count);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static UInt64 FNVHash(UInt64 s)
        {
            // 'FNV like' hash https://en.wikipedia.org/wiki/Fowler%E2%80%93Noll%E2%80%93Vo_hash_function
            // code for FNV, below, was grabbed and modified from SO answer
            const UInt64 p = 1099511628211;
            UInt64 hash = 14695981039346656037;

            var hashCode = s;
            hash = (hash ^ (hashCode & 0x00000ffffffffff)) * p;
            hashCode >>= 32;
            return (hash ^ hashCode) * p;
        }
    }
}
