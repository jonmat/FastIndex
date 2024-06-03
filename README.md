# FastIndex
Leverage FastFilter (XOR filter) algorithm to create an index, resulting in a solution for generating a [Perfect Hash](https://en.wikipedia.org/wiki/Perfect_hash_function)

# Modifications
This is a C# solution. The modifications required to obtain the index can be seen by reviewing [this commit](https://github.com/jonmat/FastIndex/commit/b88096205ad7aed47c66f27980174785644786b5).

# Main benefit
The solution has all the characteristics of XOR Filters, and as [expressed by researchers](https://arxiv.org/abs/1912.08258), the index has the benefit of fitting into a compact memory space.

# Lookup Performance
No fancy charts: On a 10th gen Intel I5 laptop, with 100 million keys, the average lookup time observed was 31ns, similar to Dictionary data structure lookups with hashing.

# Improvements
The main property leveraged to compute the index relies on the design of XOR filters making use of 3 constant size array segments, which lends to the simplicity of computing the index.

Computing the index requires knowing which of the 3 array segments—that make up the XOR filters used to generate a fingerprint—is the primary segment, and saving that off. The presented solution stores the primary segment's index value in a lookaside Byte array. But since it only requires 2 bits to store a value between 0 and 2—representing array segments 0, 1, or 2—an improved solution for storage efficiency would be to multiplex those 2 bits onto the fingerprint. The downside of course, is that due to the fact that XOR filters are an example of a [probabilistic lookup strategy](https://medium.com/hyperblogblog/probabilistic-data-structure-use-cases-b414574b8961), loosing 2 bits from the fingerprint means that you increase the likelihood of a hash collision--but, note that multiplexing means that the computation for the index is nearly identical to that of a contains.

# Generation Improvements
A simple solution for decreasing the amount of time to construct the filter is to make use of all of the processing cores available, and run the algorithm in parallel on subsets of the key space, by partitioning the key space into distinct subsets, and creating a separate XOR filter for each distinct subset. Of course, doing this implies that the complexity of managing multiple XOR filters for a given key space goes up.

Another approach to speed up construction that was recently published, is to [leverage a GPU](https://dash.harvard.edu/bitstream/handle/1/37375028/CHUA-DOCUMENT-2023.pdf?sequence=1).

Since the use case for this generally would be to lookup data that may not change a lot, a simple approach would be to save off the data of the completed XOR filter to a persistent store, and load it back in to main memory when needed. 

# References
1) [FastFilter](https://github.com/FastFilter/xorfilter)
2) [Xor Filters: Faster and Smaller Than Bloom and Cuckoo Filters authored by Thomas Mueller Graf, Daniel Lemire](https://arxiv.org/abs/1912.08258)

# How-To use FastIndex
The best way to learn how to use this class is to load the solution into visual studio and run and examine the tests. The test data used uses a practical real-world scenario around ["faked" telephone numbers](https://github.com/jonmat/FastIndex/blob/main/FastIndex/FastIndex.Tests/FastIndexTestHelper.cs#L35).

## How-To highlights
- Xor-Filter requires a 64bit hash of each data item. [XXHash](https://github.com/jonmat/FastIndex/blob/main/FastIndex/FastIndex.Tests/TestData.cs#L20) is a good choice for generating this:
- [FastFilter generic class definition](https://github.com/jonmat/FastIndex/blob/main/FastIndex/FastIndex.Tests/FastIndexTestHelper.cs#L45) has a triple <MarkerType, FingerCalc being used which is either 8, 16, or 32, and the chosen FingerCalc's respective matching base data type that represents the resulting FastFilter storage type>.
- Assuming there is a need to map the resulting FastFilter index into an array of indexed items, you first need to [allocate an array of the correct size](https://github.com/jonmat/FastIndex/blob/main/FastIndex/FastIndex.Tests/FastIndexTestHelper.cs#L56), and for each data item look up the index and [store it in the array](https://github.com/jonmat/FastIndex/blob/main/FastIndex/FastIndex.Tests/FastIndexTestHelper.cs#L61).
- Validation Required!! Since FastFilter is a probabilistic contains lookup, this means that any random data item can potential end up with a 64bit hash value that matches something stored in the array, known commonly as a [Hash Collision](https://en.wikipedia.org/wiki/Hash_collision). So, once the item has been identified, The data item's [Equals](https://github.com/jonmat/FastIndex/blob/main/FastIndex/FastIndex.Tests/TestData.cs#L23) needs to return true to ensure you have an actual match.

# Benchmarks
```bash
dotnet run -c Release --project BenchMark
```
```bash
// * Summary *

BenchmarkDotNet v0.13.12, Windows 10 (10.0.19045.3803/22H2/2022Update)
Intel Core i5-10210U CPU 1.60GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 7.0.203
  [Host]     : .NET 6.0.25 (6.0.2523.51912), X64 RyuJIT AVX2
  DefaultJob : .NET 6.0.25 (6.0.2523.51912), X64 RyuJIT AVX2


| Method                                | Mean       | Error    | StdDev   |
|-------------------------------------- |-----------:|---------:|---------:|
| Construct_FastFilter_10_Million_Items | 6,364.8 ms | 85.34 ms | 75.65 ms |
| Contains_1_Million_Lookups            |   381.3 ms |  4.96 ms |  4.64 ms |
| Index_1_Million_Lookups               |   436.2 ms |  7.30 ms | 10.23 ms |

// * Hints *
Outliers
  BenchMarkTests.Construct_FastFilter_10_Million_Items: Default -> 1 outlier  was  removed (6.77 s)
  BenchMarkTests.Index_1_Million_Lookups: Default               -> 6 outliers were removed (485.89 ms..539.99 ms)

// * Legends *
  Mean   : Arithmetic mean of all measurements
  Error  : Half of 99.9% confidence interval
  StdDev : Standard deviation of all measurements
  1 ms   : 1 Millisecond (0.001 sec)

// ***** BenchmarkRunner: End *****
Run time: 00:07:27 (447.39 sec), executed benchmarks: 3

Global total time: 00:07:32 (452.21 sec), executed benchmarks: 3

```



