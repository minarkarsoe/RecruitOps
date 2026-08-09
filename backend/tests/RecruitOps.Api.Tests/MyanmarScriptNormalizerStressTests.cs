using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RecruitOps.Application.Interfaces;
using RecruitOps.Infrastructure.Services.MyanmarScript;
using Xunit;
using Xunit.Abstractions;

namespace RecruitOps.Api.Tests;

public class MyanmarScriptNormalizerStressTests
{
    private readonly ITestOutputHelper _output;
    private readonly IMyanmarScriptNormalizer _normalizer = new MyanmarScriptNormalizer();

    public MyanmarScriptNormalizerStressTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void StressTest_ThreadSafety_ParallelCallsProduceDeterministicResults()
    {
        // Arrange
        const int numThreads = 50;
        const int iterationsPerThread = 500;
        const int totalCalls = numThreads * iterationsPerThread;

        string[] sampleInputs = new[]
        {
            "မ\u1004\u1062လာပါ", // Zawgyi Mingalarpar
            "\u1019\u1004\u1039\u1002\u101C\u102C\u1015\u102B", // Unicode Mingalarpar
            "Candidate Name: \u1031\u1021\u102B\u1004\u103A\u1031\u1021\u102B\u1004\u103A, Role: Dev", // Mixed Zawgyi
            "English Text Only without Myanmar Script 12345!@#$%", // Non-Myanmar
            "ကျွန်းတော်သည် Software Engineer ရာထူးဖြင့် လုပ်ငန်းအတွေ့အကြုံ ၅ နှစ်ရှိပါသည်။" // Burmese Unicode sentence
        };

        // Expected outputs calculated sequentially
        var expectedOutputs = sampleInputs.Select(input => _normalizer.Normalize(input)).ToArray();

        var exceptions = new ConcurrentQueue<Exception>();
        var results = new ConcurrentBag<(int index, MyanmarScriptNormalizationResult result)>();

        // Act: Parallel execution across 50 threads
        Parallel.For(0, totalCalls, new ParallelOptions { MaxDegreeOfParallelism = numThreads }, i =>
        {
            try
            {
                int sampleIndex = i % sampleInputs.Length;
                string input = sampleInputs[sampleIndex];
                var res = _normalizer.Normalize(input);
                results.Add((sampleIndex, res));
            }
            catch (Exception ex)
            {
                exceptions.Enqueue(ex);
            }
        });

        // Assert: Zero exceptions during concurrent access
        Assert.Empty(exceptions);
        Assert.Equal(totalCalls, results.Count);

        // Verify that every parallel output matches sequential result exactly
        foreach (var (sampleIndex, result) in results)
        {
            Assert.Equal(expectedOutputs[sampleIndex].NormalizedText, result.NormalizedText);
            Assert.Equal(expectedOutputs[sampleIndex].IsZawgyiDetected, result.IsZawgyiDetected);
            Assert.Equal(expectedOutputs[sampleIndex].DetectedEncoding, result.DetectedEncoding);
        }

        _output.WriteLine($"Thread safety test passed with {totalCalls} parallel calls across {numThreads} threads.");
    }

    [Fact]
    public void StressTest_ExecutionThroughput_MeasuresOpsPerSecond()
    {
        // Arrange
        const int iterations = 10_000;
        string zawgyiSample = "မ\u1004\u1062လာပါ ကျွန်းတော်သည် Software Engineer ရာထူးဖြင့် လုပ်ငန်းအတွေ့အကြုံ ၅ နှစ်ရှိပါသည်။";
        string unicodeSample = "\u1019\u1004\u1039\u1002\u101C\u102C\u1015\u102B ကျွန်တော်သည် Software Engineer ရာထူးဖြင့် လုပ်ငန်းအတွေ့အကြုံ ၅ နှစ်ရှိပါသည်။";
        string nonMyanmarSample = "Senior Full Stack Software Engineer with 8 years experience in .NET Clean Architecture, C#, PostgreSQL, React, and TypeScript.";

        // Warmup
        _normalizer.Normalize(zawgyiSample);
        _normalizer.Normalize(unicodeSample);
        _normalizer.Normalize(nonMyanmarSample);

        // Benchmark Zawgyi
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            _normalizer.Normalize(zawgyiSample);
        }
        sw.Stop();
        double zawgyiElapsedMs = sw.Elapsed.TotalMilliseconds;
        double zawgyiOpsPerSec = (iterations / zawgyiElapsedMs) * 1000;

        // Benchmark Unicode
        sw.Restart();
        for (int i = 0; i < iterations; i++)
        {
            _normalizer.Normalize(unicodeSample);
        }
        sw.Stop();
        double unicodeElapsedMs = sw.Elapsed.TotalMilliseconds;
        double unicodeOpsPerSec = (iterations / unicodeElapsedMs) * 1000;

        // Benchmark Non-Myanmar
        sw.Restart();
        for (int i = 0; i < iterations; i++)
        {
            _normalizer.Normalize(nonMyanmarSample);
        }
        sw.Stop();
        double nonMyanmarElapsedMs = sw.Elapsed.TotalMilliseconds;
        double nonMyanmarOpsPerSec = (iterations / nonMyanmarElapsedMs) * 1000;

        _output.WriteLine($"[Throughput Benchmark ({iterations} ops)]");
        _output.WriteLine($"Zawgyi Input:      {zawgyiElapsedMs:F2} ms total ({zawgyiOpsPerSec:F0} ops/sec, {zawgyiElapsedMs / iterations * 1000:F2} µs/op)");
        _output.WriteLine($"Unicode Input:     {unicodeElapsedMs:F2} ms total ({unicodeOpsPerSec:F0} ops/sec, {unicodeElapsedMs / iterations * 1000:F2} µs/op)");
        _output.WriteLine($"Non-Myanmar Input: {nonMyanmarElapsedMs:F2} ms total ({nonMyanmarOpsPerSec:F0} ops/sec, {nonMyanmarElapsedMs / iterations * 1000:F2} µs/op)");

        // Minimum throughput assertion (at least 1,000 ops/sec even for Zawgyi)
        Assert.True(zawgyiOpsPerSec > 1_000, $"Zawgyi throughput too low: {zawgyiOpsPerSec} ops/sec");
        Assert.True(unicodeOpsPerSec > 2_000, $"Unicode throughput too low: {unicodeOpsPerSec} ops/sec");
        Assert.True(nonMyanmarOpsPerSec > 20_000, $"Non-Myanmar throughput too low: {nonMyanmarOpsPerSec} ops/sec");
    }

    [Fact]
    public void StressTest_MemoryAllocationOverhead_MeasuresAllocations()
    {
        // Arrange
        const int iterations = 1_000;
        string zawgyiSample = "မ\u1004\u1062လာပါ ကျွန်းတော်သည် Software Engineer ရာထူးဖြင့် လုပ်ငန်းအတွေ့အကြုံ ၅ နှစ်ရှိပါသည်။";
        string nonMyanmarSample = "Senior Full Stack Software Engineer with 8 years experience in .NET Clean Architecture.";

        // Warmup GC
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long beforeAllocNonMyanmar = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < iterations; i++)
        {
            _normalizer.Normalize(nonMyanmarSample);
        }
        long afterAllocNonMyanmar = GC.GetAllocatedBytesForCurrentThread();
        long nonMyanmarAllocPerOp = (afterAllocNonMyanmar - beforeAllocNonMyanmar) / iterations;

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long beforeAllocZawgyi = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < iterations; i++)
        {
            _normalizer.Normalize(zawgyiSample);
        }
        long afterAllocZawgyi = GC.GetAllocatedBytesForCurrentThread();
        long zawgyiAllocPerOp = (afterAllocZawgyi - beforeAllocZawgyi) / iterations;

        _output.WriteLine($"[Memory Allocation Benchmark]");
        _output.WriteLine($"Non-Myanmar Allocation: {nonMyanmarAllocPerOp} bytes/op");
        _output.WriteLine($"Zawgyi Allocation:      {zawgyiAllocPerOp} bytes/op");

        // Non-Myanmar fast path should allocate minimal memory (just the result record + string)
        Assert.True(nonMyanmarAllocPerOp < 500, $"Non-Myanmar memory allocation too high: {nonMyanmarAllocPerOp} bytes/op");
    }

    [Fact]
    public void StressTest_LargeDocumentPayload_CompletesWithinTimeLimit()
    {
        // Arrange: Generate a 1 MB CV text payload with mixed English and Zawgyi Burmese content
        var sb = new StringBuilder();
        string paragraph = "Candidate CV Section: \u1031\u1021\u102B\u1004\u103A\u1031\u1021\u102B\u1004\u103A လုပ်ငန်းအတွေ့အကြုံ မ\u1004\u1062လာပါ Experience with .NET 10 LTS and React. ";
        while (sb.Length < 1_000_000)
        {
            sb.Append(paragraph);
        }
        string largePayload = sb.ToString();

        // Act: Normalize 1 MB document
        var sw = Stopwatch.StartNew();
        var result = _normalizer.Normalize(largePayload);
        sw.Stop();

        _output.WriteLine($"[Large Payload Benchmark (1 MB document)]");
        _output.WriteLine($"Execution Time: {sw.ElapsedMilliseconds} ms");
        _output.WriteLine($"Input Length:   {largePayload.Length:N0} chars");
        _output.WriteLine($"Output Length:  {result.NormalizedText.Length:N0} chars");

        // Assert
        Assert.True(result.IsZawgyiDetected);
        Assert.NotNull(result.NormalizedText);
        // 1MB conversion should finish within reasonable SLA (< 2000 ms)
        Assert.True(sw.ElapsedMilliseconds < 2000, $"Large payload processing took too long: {sw.ElapsedMilliseconds} ms");
    }
}
