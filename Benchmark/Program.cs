using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Management;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

class CPUBenchmark
{
    static void Main(string[] args)
    {
        int iterations = 1000000;
        int logicalProcessorCount = Environment.ProcessorCount;
        int coreCount = 0;

        Console.WriteLine("Starting CPU and Memory Benchmark (V0.3.0)...\n");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            foreach (var item in new ManagementObjectSearcher("Select * from Win32_Processor").Get())
            {
                coreCount += int.Parse(item["NumberOfCores"].ToString());
            }
            Process currentProcess = Process.GetCurrentProcess();
            currentProcess.PriorityClass = ProcessPriorityClass.AboveNormal;
            currentProcess.PriorityBoostEnabled = true;

            ManagementObjectSearcher searcher = new ManagementObjectSearcher("select Name from Win32_Processor");
            foreach (ManagementObject obj in searcher.Get())
            {
                Console.WriteLine("Processor: " + obj["Name"] + "\n");
            }
        }

        Console.WriteLine("Memory Performance:\n");

        int l3CacheSize = 16;
        int l2CacheSize = 256;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            l3CacheSize = GetL3CacheSize();
            l2CacheSize = (GetL2CacheSize() * 1024) / coreCount;
        }

        var l1CachePerformance = BenchmarkMemorySubsystem(32 * 1024);
        var l1CacheScore = Math.Round(CalculateScore(l1CachePerformance.TotalMilliseconds, logicalProcessorCount, false));
        Console.WriteLine($"L1 Cache Performance: {l1CachePerformance.TotalMilliseconds} ms, Score: {l1CacheScore}");

        var l2CachePerformance = BenchmarkMemorySubsystem(l2CacheSize * (1024));
        var l2CacheScore = Math.Round(CalculateScore(l2CachePerformance.TotalMilliseconds, logicalProcessorCount, false));
        Console.WriteLine($"L2 Cache Performance: {l2CachePerformance.TotalMilliseconds} ms, Score: {l2CacheScore}");

        var l3CachePerformance = BenchmarkMemorySubsystem(l3CacheSize * (1024 * 1024));
        var l3CacheScore = Math.Round(CalculateScore(l3CachePerformance.TotalMilliseconds, logicalProcessorCount, false));
        Console.WriteLine($"L3 Cache Performance: {l3CachePerformance.TotalMilliseconds} ms, Score: {l3CacheScore}");

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) l3CacheSize = 128;
        var ramPerformance = BenchmarkMemorySubsystem((l3CacheSize * (1024 * 1024)) * 8);
        var ramScore = Math.Round(CalculateScore(ramPerformance.TotalMilliseconds, logicalProcessorCount, false));
        Console.WriteLine($"RAM Performance: {ramPerformance.TotalMilliseconds} ms, Score: {ramScore}");

        Console.WriteLine("\nSingle-Core Performance:\n");

        var intArithmeticSingleCore = BenchmarkIntegerArithmeticOperations(iterations, false, 1);
        var intArithmeticScoreSingle = CalculateScore(intArithmeticSingleCore.TotalMilliseconds, logicalProcessorCount, false);
        Console.WriteLine($"Integer Arithmetic: {intArithmeticSingleCore.TotalMilliseconds} ms, Score: {intArithmeticScoreSingle}");

        var fpArithmeticSingleCore = BenchmarkFloatingPointOperations(iterations, false, 1);
        var fpArithmeticScoreSingle = CalculateScore(fpArithmeticSingleCore.TotalMilliseconds, logicalProcessorCount, false);
        Console.WriteLine($"Floating Point: {fpArithmeticSingleCore.TotalMilliseconds} ms, Score: {fpArithmeticScoreSingle}");

        double avx2ScoreSingle = 0;
        if (Avx2.IsSupported)
        {
            var avx2SingleCore = BenchmarkAVX2Operations(iterations, false, 1);
            avx2ScoreSingle = CalculateScore(avx2SingleCore.TotalMilliseconds, logicalProcessorCount, false);
            Console.WriteLine($"AVX2: {avx2SingleCore.TotalMilliseconds} ms, Score: {avx2ScoreSingle}");
        }
        else
        {
            avx2ScoreSingle = 0;
            Console.WriteLine("AVX2: Not supported on this CPU, Score: 0");
        }

        double avx512ScoreSingle = 0;
        if (Avx512F.IsSupported && Avx512BW.IsSupported && Avx512CD.IsSupported && Avx512DQ.IsSupported && Avx512Vbmi.IsSupported)
        {
            var avx512SingleCore = BenchmarkAVX512Operations(iterations, false, 1);
            avx512ScoreSingle = CalculateScore(avx512SingleCore.TotalMilliseconds, logicalProcessorCount, false);
            Console.WriteLine($"AVX512: {avx512SingleCore.TotalMilliseconds} ms, Score: {avx512ScoreSingle}");
        }
        else
        {
            avx512ScoreSingle = 0;
            Console.WriteLine("AVX512: Not supported on this CPU, Score: 0");
        }

        var fibonacciSingleCore = BenchmarkFibonacci(iterations * 8, false, 1);
        var fibonacciScoreSingle = CalculateScore(fibonacciSingleCore.TotalMilliseconds, logicalProcessorCount, false);
        Console.WriteLine($"Fibonacci: {fibonacciSingleCore.TotalMilliseconds} ms, Score: {fibonacciScoreSingle}");

        var primeNumberSingleCore = BenchmarkPrimeNumbers(iterations / 2, false, 1);
        var primeNumberScoreSingle = CalculateScore(primeNumberSingleCore.TotalMilliseconds, logicalProcessorCount, false);
        Console.WriteLine($"Prime Numbers: {primeNumberSingleCore.TotalMilliseconds} ms, Score: {primeNumberScoreSingle}");

        var quickSortSingleCore = BenchmarkQuickSort(iterations / 150, false, 1);
        var quickSortScoreSingle = CalculateScore(quickSortSingleCore.TotalMilliseconds, logicalProcessorCount, false);
        Console.WriteLine($"QuickSort: {quickSortSingleCore.TotalMilliseconds} ms, Score: {quickSortScoreSingle}");

        var sha256SingleCore = BenchmarkSHA256(iterations, false, 1);
        var sha256ScoreSingle = CalculateScore(sha256SingleCore.TotalMilliseconds, logicalProcessorCount, false);
        Console.WriteLine($"SHA-256: {sha256SingleCore.TotalMilliseconds} ms, Score: {sha256ScoreSingle}");

        var matrixMultiplicationSingleCore = BenchmarkMatrixMultiplication(iterations / 1000, false, 1);
        var matrixMultiplicationScoreSingle = CalculateScore(matrixMultiplicationSingleCore.TotalMilliseconds, logicalProcessorCount, false);
        Console.WriteLine($"Matrix Multiplication: {matrixMultiplicationSingleCore.TotalMilliseconds} ms, Score: {matrixMultiplicationScoreSingle}");

        var randomNumberGenerationSingleCore = BenchmarkRandomNumberGeneration(iterations * 16, false, 1);
        var randomNumberScoreSingle = CalculateScore(randomNumberGenerationSingleCore.TotalMilliseconds, logicalProcessorCount, false);
        Console.WriteLine($"Random Number Generation: {randomNumberGenerationSingleCore.TotalMilliseconds} ms, Score: {randomNumberScoreSingle}");

        var branchPredictionSingleCorePredic = BenchmarkBranchPrediction(iterations, true, false, 1);
        var branchPredictionScoreSinglePredic = CalculateScore(branchPredictionSingleCorePredic.TotalMilliseconds, logicalProcessorCount, false);
        Console.WriteLine($"Branch Prediction (Predictable): {branchPredictionSingleCorePredic.TotalMilliseconds} ms, Score: {branchPredictionScoreSinglePredic}");

        var branchPredictionSingleCore = BenchmarkBranchPrediction(iterations, false, false, 1);
        var branchPredictionScoreSingle = CalculateScore(branchPredictionSingleCore.TotalMilliseconds, logicalProcessorCount, false);
        Console.WriteLine($"Branch Prediction (Unpredictable): {branchPredictionSingleCore.TotalMilliseconds} ms, Score: {branchPredictionScoreSingle}");

        var imgProcessingSingleCore = BenchmarkImageBlur(4, false, 1);
        var imgProcessingScoreSingle = CalculateScore(imgProcessingSingleCore.TotalMilliseconds, logicalProcessorCount, false);
        Console.WriteLine($"Image Processing: {imgProcessingSingleCore.TotalMilliseconds} ms, Score: {imgProcessingScoreSingle}");

        var codeComplieSingleCore = BenchmarkCodeCompilation(100, false, 1);
        var codeComplieSingleCoreScore = CalculateScore(codeComplieSingleCore.TotalMilliseconds, logicalProcessorCount, false);
        Console.WriteLine($"Code Compilation: {codeComplieSingleCore.TotalMilliseconds} ms, Score: {codeComplieSingleCoreScore}");

        var rtSingleCore = BenchmarkRayTracing(150, false, 1);
        var rtSingleCoreScore = CalculateScore(rtSingleCore.TotalMilliseconds, logicalProcessorCount, false);
        Console.WriteLine($"Ray Tracing: {rtSingleCore.TotalMilliseconds} ms, Score: {rtSingleCoreScore}");

        var compSingleCore = BenchmarkCompressionDecompression(1, false, 1);
        var compSingleCoreScore = CalculateScore(compSingleCore.TotalMilliseconds, logicalProcessorCount, false);
        Console.WriteLine($"File De/compression: {compSingleCore.TotalMilliseconds} ms, Score: {compSingleCoreScore}");

        var webSingleCore = BenchmarkHTML5Performance(iterations / 10, false, 1);
        var webSingleCoreScore = CalculateScore(webSingleCore.TotalMilliseconds, logicalProcessorCount, false);
        Console.WriteLine($"HTML5/JS/CSS: {webSingleCore.TotalMilliseconds} ms, Score: {webSingleCoreScore}");

        var mlSingleCore = BenchmarkNeuralNetworkProcessing(iterations / 100, false, 1);
        var mlSingleCoreScore = CalculateScore(mlSingleCore.TotalMilliseconds, logicalProcessorCount, false);
        Console.WriteLine($"Neural Network: {mlSingleCore.TotalMilliseconds} ms, Score: {mlSingleCoreScore}");

        var videoSingleCore = BenchmarkVideoTranscoding(2, false, 1);
        var videoSingleCoreScore = CalculateScore(videoSingleCore.TotalMilliseconds, logicalProcessorCount, false);
        Console.WriteLine($"Video Transcoding: {videoSingleCore.TotalMilliseconds} ms, Score: {videoSingleCoreScore}");

        Console.WriteLine("\nMulti-Thread Performance:\n");

        var intArithmeticMultiCore = BenchmarkIntegerArithmeticOperations(iterations, true, logicalProcessorCount);
        var intArithmeticScoreMulti = CalculateScore(intArithmeticMultiCore.TotalMilliseconds, logicalProcessorCount, true);
        Console.WriteLine($"Integer Arithmetic: {intArithmeticMultiCore.TotalMilliseconds} ms, Score: {intArithmeticScoreMulti}");

        var fpArithmeticMultiCore = BenchmarkFloatingPointOperations(iterations, true, logicalProcessorCount);
        var fpArithmeticScoreMulti = CalculateScore(fpArithmeticMultiCore.TotalMilliseconds, logicalProcessorCount, true);
        Console.WriteLine($"Floating Point: {fpArithmeticMultiCore.TotalMilliseconds} ms, Score: {fpArithmeticScoreMulti}");

        double avx2ScoreMulti = 0;
        if (Avx2.IsSupported)
        {
            var avx2MultiCore = BenchmarkAVX2Operations(iterations, true, logicalProcessorCount);
            avx2ScoreMulti = CalculateScore(avx2MultiCore.TotalMilliseconds, logicalProcessorCount, true);
            Console.WriteLine($"AVX2: {avx2MultiCore.TotalMilliseconds} ms, Score: {avx2ScoreMulti}");
        }
        else
        {
            avx2ScoreMulti = 0;
            Console.WriteLine("AVX2: Not supported on this CPU, Score: 0");
        }

        double avx512ScoreMulti = 0;
        if (Avx512F.IsSupported && Avx512BW.IsSupported && Avx512CD.IsSupported && Avx512DQ.IsSupported && Avx512Vbmi.IsSupported)
        {
            var avx512MultiCore = BenchmarkAVX512Operations(iterations, true, logicalProcessorCount);
            avx512ScoreMulti = CalculateScore(avx512MultiCore.TotalMilliseconds, logicalProcessorCount, true);
            Console.WriteLine($"AVX512: {avx512MultiCore.TotalMilliseconds} ms, Score: {avx512ScoreMulti}");
        }
        else
        {
            avx512ScoreMulti = 0;
            Console.WriteLine("AVX512: Not supported on this CPU, Score: 0");
        }

        var fibonacciMultiCore = BenchmarkFibonacci(iterations * 8, true, logicalProcessorCount);
        var fibonacciScoreMulti = CalculateScore(fibonacciMultiCore.TotalMilliseconds, logicalProcessorCount, true);
        Console.WriteLine($"Fibonacci: {fibonacciMultiCore.TotalMilliseconds} ms, Score: {fibonacciScoreMulti}");

        var primeNumberMultiCore = BenchmarkPrimeNumbers(iterations / 2, true, logicalProcessorCount);
        var primeNumberScoreMulti = CalculateScore(primeNumberMultiCore.TotalMilliseconds, logicalProcessorCount, true);
        Console.WriteLine($"Prime Numbers: {primeNumberMultiCore.TotalMilliseconds} ms, Score: {primeNumberScoreMulti}");

        var quickSortMultiCore = BenchmarkQuickSort(iterations / 150, true, logicalProcessorCount);
        var quickSortScoreMulti = CalculateScore(quickSortMultiCore.TotalMilliseconds, logicalProcessorCount, true);
        Console.WriteLine($"QuickSort: {quickSortMultiCore.TotalMilliseconds} ms, Score: {quickSortScoreMulti}");

        var sha256MultiCore = BenchmarkSHA256(iterations, true, logicalProcessorCount);
        var sha256ScoreMulti = CalculateScore(sha256MultiCore.TotalMilliseconds, logicalProcessorCount, true);
        Console.WriteLine($"SHA-256: {sha256MultiCore.TotalMilliseconds} ms, Score: {sha256ScoreMulti}");

        var matrixMultiplicationMultiCore = BenchmarkMatrixMultiplication(iterations / 1000, true, logicalProcessorCount);
        var matrixMultiplicationScoreMulti = CalculateScore(matrixMultiplicationMultiCore.TotalMilliseconds, logicalProcessorCount, true);
        Console.WriteLine($"Matrix Multiplication: {matrixMultiplicationMultiCore.TotalMilliseconds} ms, Score: {matrixMultiplicationScoreMulti}");

        var randomNumberGenerationMultiCore = BenchmarkRandomNumberGeneration(iterations * 16, true, logicalProcessorCount);
        var randomNumberScoreMulti = CalculateScore(randomNumberGenerationMultiCore.TotalMilliseconds, logicalProcessorCount, true);
        Console.WriteLine($"Random Number Generation: {randomNumberGenerationMultiCore.TotalMilliseconds} ms, Score: {randomNumberScoreMulti}");

        var branchPredictionMultiCorePredic = BenchmarkBranchPrediction(iterations, true, true, logicalProcessorCount);
        var branchPredictionMultiPredic = CalculateScore(branchPredictionMultiCorePredic.TotalMilliseconds, logicalProcessorCount, true);
        Console.WriteLine($"Branch Prediction (Predictable): {branchPredictionMultiCorePredic.TotalMilliseconds} ms, Score: {branchPredictionMultiPredic}");

        var branchPredictionMultiCore = BenchmarkBranchPrediction(iterations, false, true, logicalProcessorCount);
        var branchPredictionMulti = CalculateScore(branchPredictionMultiCore.TotalMilliseconds, logicalProcessorCount, true);
        Console.WriteLine($"Branch Prediction (Unpredictable): {branchPredictionMultiCore.TotalMilliseconds} ms, Score: {branchPredictionMulti}");

        var imgProcessingMultiCore = BenchmarkImageBlur(4, true, logicalProcessorCount);
        var imgProcessingMulti = CalculateScore(imgProcessingMultiCore.TotalMilliseconds, logicalProcessorCount, true);
        Console.WriteLine($"Image Processing: {imgProcessingMultiCore.TotalMilliseconds} ms, Score: {imgProcessingMulti}");

        var codeComplieMultiCore = BenchmarkCodeCompilation(100, true, logicalProcessorCount);
        var codeComplieMultiScore = CalculateScore(codeComplieMultiCore.TotalMilliseconds, logicalProcessorCount, true);
        Console.WriteLine($"Code Compilation: {codeComplieMultiCore.TotalMilliseconds} ms, Score: {codeComplieMultiScore}");

        var rtMultiCore = BenchmarkRayTracing(150, true, logicalProcessorCount);
        var rtMultiCoreScore = CalculateScore(rtMultiCore.TotalMilliseconds, logicalProcessorCount, true);
        Console.WriteLine($"Ray Tracing: {rtMultiCore.TotalMilliseconds} ms, Score: {rtMultiCoreScore}");

        var compMultiCore = BenchmarkCompressionDecompression(1, true, logicalProcessorCount);
        var compMultiCoreScore = CalculateScore(compMultiCore.TotalMilliseconds, logicalProcessorCount, true);
        Console.WriteLine($"File De/compression: {compMultiCore.TotalMilliseconds} ms, Score: {compMultiCoreScore}");

        var webMultiCore = BenchmarkHTML5Performance(iterations / 10, true, logicalProcessorCount);
        var webMultiCoreScore = CalculateScore(webMultiCore.TotalMilliseconds, logicalProcessorCount, true);
        Console.WriteLine($"HTML5/JS/CSS: {webMultiCore.TotalMilliseconds} ms, Score: {webMultiCoreScore}");

        var mlMultiCore = BenchmarkNeuralNetworkProcessing(iterations / 100, true, logicalProcessorCount);
        var mlMultiCoreScore = CalculateScore(mlMultiCore.TotalMilliseconds, logicalProcessorCount, true);
        Console.WriteLine($"Neural Network: {mlMultiCore.TotalMilliseconds} ms, Score: {mlMultiCoreScore}");

        var videoMultiCore = BenchmarkVideoTranscoding(2, true, logicalProcessorCount);
        var videoMultiCoreScore = CalculateScore(videoMultiCore.TotalMilliseconds, logicalProcessorCount, true);
        Console.WriteLine($"Video Transcoding: {videoMultiCore.TotalMilliseconds} ms, Score: {videoMultiCoreScore}");

        var comMultiCore = BenchmarkThreadCommunication(iterations, logicalProcessorCount);
        var comMultiCoreScore = CalculateScore(comMultiCore.TotalMilliseconds, logicalProcessorCount, true);
        Console.WriteLine($"Thread Communication: {comMultiCore.TotalMilliseconds} ms, Score: {comMultiCoreScore}");

        var memoryScore = Math.Round((l1CacheScore + l2CacheScore + l3CacheScore + ramScore) / 4);
        Console.WriteLine($"\nFinal Memory Score: {memoryScore}\n");

        int totalBenches = 19;
        int totalBenchesMT = totalBenches + 1;

        int singleCoreBenchCount = totalBenches - (avx2ScoreSingle == 0 ? 1 : 0) - (avx512ScoreSingle == 0 ? 1 : 0);
        int multiCoreBenchCount = totalBenchesMT - (avx2ScoreMulti == 0 ? 1 : 0) - (avx512ScoreMulti == 0 ? 1 : 0);

        double singleCoreScore = (
            intArithmeticScoreSingle + fpArithmeticScoreSingle + avx2ScoreSingle + avx512ScoreSingle + fibonacciScoreSingle +
            primeNumberScoreSingle + quickSortScoreSingle + sha256ScoreSingle + matrixMultiplicationScoreSingle + randomNumberScoreSingle +
            branchPredictionScoreSingle + imgProcessingScoreSingle + branchPredictionScoreSinglePredic + codeComplieSingleCoreScore +
            rtSingleCoreScore + compSingleCoreScore + webSingleCoreScore + mlSingleCoreScore + videoSingleCoreScore
        ) / singleCoreBenchCount;

        Console.WriteLine($"Final Single-Core Score: {Math.Round(singleCoreScore)}");

        double multiCoreScore = (
            intArithmeticScoreMulti + fpArithmeticScoreMulti + avx2ScoreMulti + avx512ScoreMulti + fibonacciScoreMulti +
            primeNumberScoreMulti + quickSortScoreMulti + sha256ScoreMulti + matrixMultiplicationScoreMulti + randomNumberScoreMulti +
            branchPredictionMulti + imgProcessingMulti + branchPredictionMultiPredic + codeComplieMultiScore +
            rtMultiCoreScore + comMultiCoreScore + compMultiCoreScore + webMultiCoreScore + mlMultiCoreScore + videoMultiCoreScore
        ) / multiCoreBenchCount;

        Console.WriteLine($"Final Multi-Thread Score: {Math.Round(multiCoreScore)}");


        Console.WriteLine("\nBenchmarking Completed.\nCreated by JamesCJ.");
        Console.ReadLine();
    }

    private static int GetL3CacheSize()
    {
        int l3CacheSize = 0;
        try
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_Processor");
            foreach (ManagementObject queryObj in searcher.Get())
            {
                l3CacheSize = Convert.ToInt32(queryObj["L3CacheSize"]) / 1024;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error retrieving L3 Cache size: {e.Message}");
        }
        return l3CacheSize;
    }

    private static int GetL2CacheSize()
    {
        int l2CacheSize = 0;
        try
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_Processor");
            foreach (ManagementObject queryObj in searcher.Get())
            {
                l2CacheSize = Convert.ToInt32(queryObj["L2CacheSize"]) / 1024;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error retrieving L2 Cache size: {e.Message}");
        }
        return l2CacheSize;
    }
    private static TimeSpan BenchmarkMemorySubsystem(int dataSize)
    {
        int elementCount = dataSize / sizeof(int);
        int[] array = new int[elementCount];
        Random rand = new Random();

        for (int i = 0; i < elementCount; i++)
        {
            array[i] = rand.Next();
        }

        Stopwatch stopwatch = Stopwatch.StartNew();

        for (int loop = 0; loop < 10; loop++)
        {
            int vectorizedCount = elementCount / Vector<int>.Count * Vector<int>.Count;
            var vec5 = new Vector<int>(5);

            for (int i = 0; i < vectorizedCount; i += Vector<int>.Count)
            {
                var vec = new Vector<int>(array, i);
                vec = (vec * 2) + (vec / 3) - (vec - (vec / vec5) * vec5);
                vec.CopyTo(array, i);
            }
            for (int i = vectorizedCount; i < elementCount; i++)
            {
                array[i] = (array[i] * 2) + (array[i] / 3) - (array[i] % 5);
            }

            for (int i = 0; i < elementCount; i += 4)
            {
                int reverseIndex1 = elementCount - 1 - i;
                int reverseIndex2 = elementCount - 2 - i;
                int reverseIndex3 = elementCount - 3 - i;
                int reverseIndex4 = elementCount - 4 - i;

                if (reverseIndex1 >= 0)
                    array[reverseIndex1] = (array[reverseIndex1] * 3) / 2 + (array[reverseIndex1] % 7);
                if (reverseIndex2 >= 0)
                    array[reverseIndex2] = (array[reverseIndex2] * 3) / 2 + (array[reverseIndex2] % 7);
                if (reverseIndex3 >= 0)
                    array[reverseIndex3] = (array[reverseIndex3] * 3) / 2 + (array[reverseIndex3] % 7);
                if (reverseIndex4 >= 0)
                    array[reverseIndex4] = (array[reverseIndex4] * 3) / 2 + (array[reverseIndex4] % 7);
            }

            for (int i = 0; i < elementCount; i++)
            {
                int index = rand.Next(elementCount);
                array[index] = (array[index] + rand.Next()) / 2;
            }

            for (int stride = 1; stride <= 16; stride *= 2)
            {
                for (int i = 0; i < elementCount; i += stride * 4)
                {
                    if (i < elementCount)
                        array[i] = (array[i] * stride) - (array[i] % 5);
                    if (i + stride < elementCount)
                        array[i + stride] = (array[i + stride] * stride) - (array[i + stride] % 5);
                    if (i + 2 * stride < elementCount)
                        array[i + 2 * stride] = (array[i + 2 * stride] * stride) - (array[i + 2 * stride] % 5);
                    if (i + 3 * stride < elementCount)
                        array[i + 3 * stride] = (array[i + 3 * stride] * stride) - (array[i + 3 * stride] % 5);
                }
            }

            for (int i = 0; i < elementCount; i += 128)
            {
                int idx1 = (i + 64) % elementCount;
                int idx2 = (i + 128) % elementCount;
                array[i] = array[idx1] + array[idx2];
            }
        }

        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    private static TimeSpan BenchmarkIntegerArithmeticOperations(int iterations, bool multiCore = false, int threads = 1)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        Action benchmarkAction = () =>
        {
            long result = 0;
            double[] smallArray = new double[10000];
            Random random = new Random();
            for (int j = 0; j < smallArray.Length; j++)
            {
                smallArray[j] = random.NextDouble();
            }

            double[] sinValues = new double[iterations];
            double[] cosValues = new double[500];
            for (int i = 0; i < iterations; i++)
            {
                sinValues[i] = Math.Sin(i * 0.01);
            }
            for (int j = 0; j < 500; j++)
            {
                cosValues[j] = Math.Cos(j * 0.01);
            }

            for (int i = 0; i < iterations; i++)
            {
                for (int j = 0; j < 500; j++)
                {
                    long temp = (i + j) * (i - j);
                    result += temp;
                    double complexCalc = sinValues[i] + cosValues[j];
                    result += (long)(complexCalc * 100);
                    int index = (i + j) % smallArray.Length;
                    result += (long)(smallArray[index] * 100);
                }
            }
        };

        if (multiCore)
        {
            Parallel.For(0, threads, new ParallelOptions { MaxDegreeOfParallelism = threads }, _ => benchmarkAction());
        }
        else
        {
            benchmarkAction();
        }

        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    private static TimeSpan BenchmarkFloatingPointOperations(int iterations, bool multiCore, int threads = 1)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        Action benchmarkAction = () =>
        {
            double result = 0.0;
            double[] sqrtCache = new double[iterations + 1000];
            for (int j = 0; j < iterations + 1000; j++)
            {
                sqrtCache[j] = Math.Sqrt(j);
            }

            for (int i = 0; i < iterations; i++)
            {
                for (int j = 0; j < 1000; j++)
                {
                    result += sqrtCache[i + j] * Math.Sin(i - j);
                }
            }
        };

        if (multiCore)
        {
            Parallel.For(0, threads, new ParallelOptions { MaxDegreeOfParallelism = threads }, _ => benchmarkAction());
        }
        else
        {
            benchmarkAction();
        }

        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    private static TimeSpan BenchmarkAVX2Operations(int iterations, bool multiCore, int threads = 1)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        Action avxOperation = () =>
        {
            Vector256<float> vec1 = Vector256.Create(1024.0f);
            Vector256<float> vec2 = Vector256.Create(1024.0f);
            Vector256<float> result = Avx2.Multiply(vec1, vec2);

            for (int j = 0; j < iterations; j++)
            {
                result = Avx2.Multiply(result, vec1);
                result = Avx2.Add(result, vec2);
                result = Avx2.Subtract(result, vec1);
                result = Avx2.Multiply(result, vec2);
                result = Avx2.Or(result, vec1);
                result = Avx2.Xor(result, vec2);
                result = Avx.Multiply(result, vec2);
                result = Avx.Reciprocal(result);
                result = Avx.ReciprocalSqrt(result);
                result = Avx.Sqrt(result);
                result = Avx.Subtract(result, vec1);
                result = Avx.Add(result, vec2);
                result = Avx.Divide(result, vec1);
                result = Avx.Multiply(result, vec2);
            }
        };

        int loops = 7;

        if (multiCore)
        {
            Parallel.For(0, threads, new ParallelOptions { MaxDegreeOfParallelism = threads }, _ =>
            {
                for (int loop = 0; loop < loops; loop++)
                {
                    avxOperation();
                }
            });
        }
        else
        {
            for (int loop = 0; loop < loops; loop++)
            {
                avxOperation();
            }
        }

        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    private static TimeSpan BenchmarkAVX512Operations(int iterations, bool multiCore, int threads = 1)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        Action avx512Operation = () =>
        {
            Vector512<float> vec1 = Vector512.Create(1024.0f);
            Vector512<float> vec2 = Vector512.Create(1024.0f);
            Vector512<float> result = Avx512F.Multiply(vec1, vec2);

            for (int j = 0; j < iterations; j++)
            {
                result = Avx512F.Multiply(result, vec1);
                result = Avx512F.Add(result, vec2);
                result = Avx512F.Subtract(result, vec1);
                result = Avx512F.Multiply(result, vec2);
                result = Avx512F.Multiply(result, result);
                result = Avx512F.Divide(result, vec2);

                result = Avx512Vbmi.Multiply(result, vec1);
                result = Avx512Vbmi.Add(result, vec2);
                result = Avx512Vbmi.Subtract(result, vec1);
                result = Avx512Vbmi.Multiply(result, vec2);
                result = Avx512Vbmi.Multiply(result, result);

                result = Avx512BW.Multiply(result, vec1);
                result = Avx512BW.Add(result, vec2);
                result = Avx512BW.Subtract(result, vec1);
                result = Avx512BW.Multiply(result, vec2);
                result = Avx512BW.Subtract(result, vec1);
                result = Avx512BW.Add(result, vec2);

                result = Avx512CD.Multiply(result, vec1);
                result = Avx512CD.Add(result, vec2);
                result = Avx512CD.Subtract(result, vec1);
                result = Avx512CD.Max(result, vec1);
                result = Avx512CD.Min(result, vec2);
                result = Avx512CD.Multiply(result, result);

                result = Avx512DQ.Multiply(result, vec1);
                result = Avx512DQ.Add(result, vec2);
                result = Avx512DQ.Subtract(result, vec1);
                result = Avx512DQ.Multiply(result, vec2);
                result = Avx512DQ.Multiply(result, result);
            }
        };

        int loops = 7;

        if (multiCore)
        {
            Parallel.For(0, threads, new ParallelOptions { MaxDegreeOfParallelism = threads }, _ =>
            {
                for (int loop = 0; loop < loops; loop++)
                {
                    avx512Operation();
                }
            });
        }
        else
        {
            for (int loop = 0; loop < loops; loop++)
            {
                avx512Operation();
            }
        }

        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    private static TimeSpan BenchmarkFibonacci(int iterations, bool multiCore, int threads = 1)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        Action fibonacciAction = () =>
        {
            for (int j = 0; j < iterations; j++)
            {
                Fibonacci(30);
            }
        };

        if (multiCore)
        {
            Parallel.For(0, threads, new ParallelOptions { MaxDegreeOfParallelism = threads }, _ => fibonacciAction());
        }
        else
        {
            fibonacciAction();
        }

        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    private static int Fibonacci(int n)
    {
        if (n <= 1) return n;

        int a = 0;
        int b = 1;

        for (int i = 2; i <= n; i++)
        {
            int temp = a + b;
            a = b;
            b = temp;
        }

        return b;
    }

    private static TimeSpan BenchmarkPrimeNumbers(int iterations, bool multiCore, int threads = 1)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        Action primeCalculationAction = () =>
        {
            for (int j = 0; j < iterations; j++)
            {
                SieveOfEratosthenes(10000);
            }
        };

        if (multiCore)
        {
            Parallel.For(0, threads, new ParallelOptions { MaxDegreeOfParallelism = threads }, _ => primeCalculationAction());
        }
        else
        {
            primeCalculationAction();
        }

        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    private static void SieveOfEratosthenes(int n)
    {
        bool[] prime = new bool[n + 1];
        Array.Fill(prime, true);

        prime[0] = prime[1] = false;

        int sqrtN = (int)Math.Sqrt(n);
        for (int p = 2; p <= sqrtN; p++)
        {
            if (prime[p])
            {
                for (int i = p * p; i <= n; i += p)
                    prime[i] = false;
            }
        }
    }

    private static TimeSpan BenchmarkQuickSort(int iterations, bool multiCore, int threads = 1)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        Action quickSortAction = () =>
        {
            Random rand = new Random();
            for (int j = 0; j < iterations; j++)
            {
                int[] array = new int[10000];
                for (int k = 0; k < array.Length; k++)
                {
                    array[k] = rand.Next();
                }
                QuickSort(array, 0, array.Length - 1);
            }
        };

        if (multiCore)
        {
            Parallel.For(0, threads, new ParallelOptions { MaxDegreeOfParallelism = threads }, _ => quickSortAction());
        }
        else
        {
            quickSortAction();
        }

        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    private static void QuickSort(int[] arr, int left, int right)
    {
        while (left < right)
        {
            int pivot = Partition(arr, left, right);

            if (pivot - left < right - pivot)
            {
                QuickSort(arr, left, pivot - 1);
                left = pivot + 1;
            }
            else
            {
                QuickSort(arr, pivot + 1, right);
                right = pivot - 1;
            }
        }
    }

    private static int Partition(int[] arr, int left, int right)
    {
        int pivot = arr[left];
        int l = left + 1;
        int r = right;

        while (true)
        {
            while (l <= r && arr[l] <= pivot) l++;
            while (r >= l && arr[r] >= pivot) r--;

            if (l >= r)
            {
                break;
            }

            int temp = arr[l];
            arr[l] = arr[r];
            arr[r] = temp;
        }

        arr[left] = arr[r];
        arr[r] = pivot;

        return r;
    }

    private static TimeSpan BenchmarkSHA256(int iterations, bool multiCore, int threads = 1)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        byte[] data = new byte[1024];

        void Sha256Action()
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                for (int j = 0; j < iterations; j++)
                {
                    sha256.ComputeHash(data);
                }
            }
        }

        if (multiCore)
        {
            Parallel.For(0, threads, new ParallelOptions { MaxDegreeOfParallelism = threads }, _ => Sha256Action());
        }
        else
        {
            Sha256Action();
        }

        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    private static TimeSpan BenchmarkMatrixMultiplication(int iterations, bool multiCore, int threads = 1)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        void MatrixMultiplicationTask()
        {
            int[,] matrixA = new int[100, 100];
            int[,] matrixB = new int[100, 100];
            int[,] matrixC = new int[100, 100];
            Random rand = new Random();

            for (int x = 0; x < 100; x++)
            {
                for (int y = 0; y < 100; y++)
                {
                    matrixA[x, y] = rand.Next();
                    matrixB[x, y] = rand.Next();
                }
            }

            for (int x = 0; x < 100; x++)
            {
                for (int y = 0; y < 100; y++)
                {
                    int sum = 0;
                    for (int z = 0; z < 100; z++)
                    {
                        sum += matrixA[x, z] * matrixB[z, y];
                    }
                    matrixC[x, y] = sum;
                }
            }
        }

        if (multiCore)
        {
            Parallel.For(0, threads, new ParallelOptions { MaxDegreeOfParallelism = threads }, _ =>
            {
                for (int j = 0; j < iterations; j++)
                {
                    MatrixMultiplicationTask();
                }
            });
        }
        else
        {
            for (int i = 0; i < iterations; i++)
            {
                MatrixMultiplicationTask();
            }
        }

        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    private static TimeSpan BenchmarkRandomNumberGeneration(int iterations, bool multiCore, int threads = 1)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        if (multiCore)
        {
            Parallel.For(0, threads, new ParallelOptions { MaxDegreeOfParallelism = threads }, _ =>
            {
                int seed = Environment.TickCount * Thread.CurrentThread.ManagedThreadId;
                Random rand = new Random(seed);
                for (int i = 0; i < iterations; i++)
                {
                    _ = rand.NextInt64();
                }
            });
        }
        else
        {
            int seed = Environment.TickCount;
            Random rand = new Random(seed);
            for (int i = 0; i < iterations; i++)
            {
                _ = rand.NextInt64();
            }
        }
        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    private static TimeSpan BenchmarkBranchPrediction(int iterations, bool predictable, bool multiCore, int threads = 1)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        if (multiCore)
        {
            Parallel.For(0, threads, new ParallelOptions { MaxDegreeOfParallelism = threads }, _ =>
            {
                long result = 0;
                int seed = Environment.TickCount * Thread.CurrentThread.ManagedThreadId;
                Random random = new Random(seed);

                for (int j = 0; j < iterations; j++)
                {
                    if (predictable)
                    {
                        for (int k = 0; k < 1000; k++)
                        {
                            result += (k % 2 == 0) ? k : -k;
                        }
                    }
                    else
                    {
                        for (int k = 0; k < 1000; k++)
                        {
                            result += (random.Next(0, 2) == 0) ? k : -k;
                        }
                    }
                }
            });
        }
        else
        {
            long result = 0;
            int seed = Environment.TickCount;
            Random random = new Random(seed);

            for (int i = 0; i < iterations; i++)
            {
                if (predictable)
                {
                    for (int k = 0; k < 1000; k++)
                    {
                        result += (k % 2 == 0) ? k : -k;
                    }
                }
                else
                {
                    for (int k = 0; k < 1000; k++)
                    {
                        result += (random.Next(0, 2) == 0) ? k : -k;
                    }
                }
            }
        }

        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    private static TimeSpan BenchmarkImageBlur(int iterations, bool multiCore, int threads = 1)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        int matrixWidth = 3840;
        int matrixHeight = 2160;
        int[,] image = new int[matrixWidth, matrixHeight];
        int[,] output = new int[matrixWidth, matrixHeight];

        Random rand = new Random();
        for (int i = 0; i < matrixWidth; i++)
        {
            for (int j = 0; j < matrixHeight; j++)
            {
                image[i, j] = rand.Next(256);
            }
        }

        if (multiCore)
        {
            Parallel.For(0, threads, new ParallelOptions { MaxDegreeOfParallelism = threads }, _ =>
            {
                for (int iter = 0; iter < iterations; iter++)
                {
                    ApplyBlur(matrixWidth, matrixHeight, image, output);
                }
            });
        }
        else
        {
            for (int iter = 0; iter < iterations; iter++)
            {
                ApplyBlur(matrixWidth, matrixHeight, image, output);
            }
        }

        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    private static void ApplyBlur(int width, int height, int[,] image, int[,] output)
    {
        for (int i = 1; i < width - 1; i++)
        {
            for (int j = 1; j < height - 1; j++)
            {
                int sum = 0;
                int count = 0;
                for (int ki = -1; ki <= 1; ki++)
                {
                    for (int kj = -1; kj <= 1; kj++)
                    {
                        sum += image[i + ki, j + kj];
                        count++;
                    }
                }
                output[i, j] = sum / count;
            }
        }
    }

    private static TimeSpan BenchmarkCodeCompilation(int iterations, bool multiCore, int threads = 1)
    {
        string code = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public int QuantityAvailable { get; set; }

        public Product(int id, string name, decimal price, int quantityAvailable)
        {
            Id = id;
            Name = name;
            Price = price;
            QuantityAvailable = quantityAvailable;
        }

        public override string ToString()
        {
            return $""{Id}: {Name} - {Price:C} (Available: {QuantityAvailable})"";
        }
    }

    public class Order
    {
        public int OrderId { get; set; }
        public List<OrderItem> Items { get; set; } = new List<OrderItem>();

        public void AddItem(Product product, int quantity)
        {
            if (product.QuantityAvailable >= quantity)
            {
                Items.Add(new OrderItem { Product = product, Quantity = quantity });
                product.QuantityAvailable -= quantity;
            }
            else
            {
                Console.WriteLine($""Insufficient stock for {product.Name}"");
            }
        }

        public decimal GetTotal()
        {
            return Items.Sum(item => item.Product.Price * item.Quantity);
        }

        public override string ToString()
        {
            var orderSummary = $""Order {OrderId}:\n"";
            foreach (var item in Items)
            {
                orderSummary += $""- {item.Product.Name} x {item.Quantity} = {item.Product.Price * item.Quantity:C}\n"";
            }
            orderSummary += $""Total: {GetTotal():C}"";
            return orderSummary;
        }
    }

    public class OrderItem
    {
        public Product Product { get; set; }
        public int Quantity { get; set; }
    }

    public class Program
    {
        private static List<Product> _products = new List<Product>
        {
            new Product(1, ""Laptop"", 1200.00m, 10),
            new Product(2, ""Smartphone"", 800.00m, 15),
            new Product(3, ""Tablet"", 300.00m, 20)
        };

        public static void Main(string[] args)
        {
            Console.WriteLine(""Welcome to the Console Ordering System"");

            var order = new Order { OrderId = 1 };
            bool ordering = true;

            while (ordering)
            {
                Console.WriteLine(""\nAvailable Products:"");
                foreach (var product in _products)
                {
                    Console.WriteLine(product);
                }

                Console.WriteLine(""\nEnter the product ID to add to your order (or type 'done' to finish):"");
                var input = Console.ReadLine();

                if (input.ToLower() == ""done"")
                {
                    ordering = false;
                }
                else if (int.TryParse(input, out int productId))
                {
                    var product = _products.FirstOrDefault(p => p.Id == productId);
                    if (product != null)
                    {
                        Console.WriteLine($""Enter quantity for {product.Name}:"");
                        if (int.TryParse(Console.ReadLine(), out int quantity))
                        {
                            order.AddItem(product, quantity);
                        }
                        else
                        {
                            Console.WriteLine(""Invalid quantity. Please try again."");
                        }
                    }
                    else
                    {
                        Console.WriteLine(""Invalid product ID. Please try again."");
                    }
                }
                else
                {
                    Console.WriteLine(""Invalid input. Please try again."");
                }
            }

            Console.WriteLine(""\nOrder Summary:"");
            Console.WriteLine(order);
        }
    }";

        Stopwatch stopwatch = Stopwatch.StartNew();

        Action compileAction = () =>
        {
            for (int i = 0; i < iterations; i++)
            {
                var syntaxTree = CSharpSyntaxTree.ParseText(code);
                var references = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                    .Select(a => MetadataReference.CreateFromFile(a.Location))
                    .Cast<MetadataReference>();

                var compilation = CSharpCompilation.Create(
                    "CompilationTest",
                    syntaxTrees: new[] { syntaxTree },
                    references: references,
                    options: new CSharpCompilationOptions(OutputKind.ConsoleApplication)
                );

                using (var ms = new MemoryStream())
                {
                    EmitResult result = compilation.Emit(ms);
                    if (!result.Success)
                    {
                        throw new Exception("Compilation failed");
                    }
                }
            }
        };

        if (multiCore)
        {
            Parallel.For(0, threads, new ParallelOptions { MaxDegreeOfParallelism = threads }, _ => compileAction());
        }
        else
        {
            compileAction();
        }

        stopwatch.Stop();
        return stopwatch.Elapsed;
    }


    private static TimeSpan BenchmarkRayTracing(int iterations, bool multiCore, int threads = 1)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        if (multiCore)
        {
            Parallel.For(0, threads, new ParallelOptions { MaxDegreeOfParallelism = threads }, t =>
            {
                RenderScene(iterations);
            });
        }
        else
        {
            RenderScene(iterations);
        }

        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    private static void RenderScene(int iterations)
    {
        const int width = 128;
        const int height = 72;
        const int maxDepth = 5;
        const int samples = 10;

        var spheres = new[]
        {
        new Sphere(new Vector3(0, 0, -1), 0.5f, new Vector3(0.8f, 0.3f, 0.3f)),
        new Sphere(new Vector3(0, -100.5f, -1), 100, new Vector3(0.8f, 0.8f, 0.0f)),
    };

        for (int i = 0; i < iterations; i++)
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Vector3 color = Vector3.Zero;

                    for (int s = 0; s < samples; s++)
                    {
                        float u = (x + RandomFloat()) / width;
                        float v = (y + RandomFloat()) / height;

                        var ray = new Ray(Vector3.Zero, new Vector3(u, v, -1.0f));
                        color += Trace(ray, spheres, maxDepth);
                    }

                    color /= samples;
                }
            }
        }
    }

    private static Vector3 Trace(Ray ray, Sphere[] spheres, int depth)
    {
        if (depth <= 0)
            return Vector3.Zero;

        HitRecord hit = default;

        if (HitSphere(ray, spheres, 0.001f, float.MaxValue, ref hit))
        {
            Vector3 target = hit.Point + hit.Normal + RandomInUnitSphere();
            return 0.5f * Trace(new Ray(hit.Point, target - hit.Point), spheres, depth - 1);
        }

        Vector3 unitDirection = Vector3.Normalize(ray.Direction);
        float t = 0.5f * (unitDirection.Y + 1.0f);
        return (1.0f - t) * Vector3.One + t * new Vector3(0.5f, 0.7f, 1.0f);
    }

    private static bool HitSphere(Ray ray, Sphere[] spheres, float tMin, float tMax, ref HitRecord hit)
    {
        HitRecord tempHit = default;
        bool hitAnything = false;
        float closestSoFar = tMax;

        foreach (var sphere in spheres)
        {
            if (sphere.Hit(ray, tMin, closestSoFar, ref tempHit))
            {
                hitAnything = true;
                closestSoFar = tempHit.T;
                hit = tempHit;
            }
        }

        return hitAnything;
    }

    private static Vector3 RandomInUnitSphere()
    {
        Random random = new Random();
        Vector3 p;
        do
        {
            p = 2.0f * new Vector3((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble()) - Vector3.One;
        } while (p.LengthSquared() >= 1.0f);

        return p;
    }

    private static float RandomFloat()
    {
        Random random = new Random();
        return (float)random.NextDouble();
    }

    private struct Ray
    {
        public Vector3 Origin;
        public Vector3 Direction;

        public Ray(Vector3 origin, Vector3 direction)
        {
            Origin = origin;
            Direction = direction;
        }

        public Vector3 At(float t)
        {
            return Origin + t * Direction;
        }
    }

    private struct HitRecord
    {
        public Vector3 Point;
        public Vector3 Normal;
        public float T;
    }

    private struct Sphere
    {
        public Vector3 Center;
        public float Radius;
        public Vector3 Albedo;

        public Sphere(Vector3 center, float radius, Vector3 albedo)
        {
            Center = center;
            Radius = radius;
            Albedo = albedo;
        }

        public bool Hit(Ray ray, float tMin, float tMax, ref HitRecord hit)
        {
            Vector3 oc = ray.Origin - Center;
            float a = Vector3.Dot(ray.Direction, ray.Direction);
            float b = Vector3.Dot(oc, ray.Direction);
            float c = Vector3.Dot(oc, oc) - Radius * Radius;
            float discriminant = b * b - a * c;

            if (discriminant > 0)
            {
                float temp = (-b - MathF.Sqrt(discriminant)) / a;
                if (temp < tMax && temp > tMin)
                {
                    hit.T = temp;
                    hit.Point = ray.At(hit.T);
                    hit.Normal = (hit.Point - Center) / Radius;
                    return true;
                }

                temp = (-b + MathF.Sqrt(discriminant)) / a;
                if (temp < tMax && temp > tMin)
                {
                    hit.T = temp;
                    hit.Point = ray.At(hit.T);
                    hit.Normal = (hit.Point - Center) / Radius;
                    return true;
                }
            }

            return false;
        }
    }

    private static TimeSpan BenchmarkCompressionDecompression(int iterations, bool multiCore, int threads = 1)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        int dataSize = 128 * 1024 * 1024;
        byte[] data = new byte[dataSize];
        Random rand = new Random();
        rand.NextBytes(data);

        if (multiCore)
        {
            Parallel.For(0, threads, new ParallelOptions { MaxDegreeOfParallelism = threads }, _ =>
            {
                for (int j = 0; j < iterations; j++)
                {
                    byte[] compressedData;
                    byte[] decompressedData;

                    using (MemoryStream ms = new MemoryStream())
                    {
                        using (GZipStream gzip = new GZipStream(ms, CompressionMode.Compress))
                        {
                            gzip.Write(data, 0, data.Length);
                        }
                        compressedData = ms.ToArray();
                    }

                    using (MemoryStream ms = new MemoryStream(compressedData))
                    {
                        using (GZipStream gzip = new GZipStream(ms, CompressionMode.Decompress))
                        {
                            using (MemoryStream decompressedStream = new MemoryStream())
                            {
                                gzip.CopyTo(decompressedStream);
                                decompressedData = decompressedStream.ToArray();
                            }
                        }
                    }

                    if (data.Length != decompressedData.Length)
                    {
                        throw new Exception("Decompressed data does not match original data.");
                    }
                }
            });
        }
        else
        {
            for (int i = 0; i < iterations; i++)
            {
                byte[] compressedData;
                byte[] decompressedData;

                using (MemoryStream ms = new MemoryStream())
                {
                    using (GZipStream gzip = new GZipStream(ms, CompressionMode.Compress))
                    {
                        gzip.Write(data, 0, data.Length);
                    }
                    compressedData = ms.ToArray();
                }

                using (MemoryStream ms = new MemoryStream(compressedData))
                {
                    using (GZipStream gzip = new GZipStream(ms, CompressionMode.Decompress))
                    {
                        using (MemoryStream decompressedStream = new MemoryStream())
                        {
                            gzip.CopyTo(decompressedStream);
                            decompressedData = decompressedStream.ToArray();
                        }
                    }
                }

                if (data.Length != decompressedData.Length)
                {
                    throw new Exception("Decompressed data does not match original data.");
                }
            }
        }

        data = null;
        GC.Collect();
        GC.WaitForPendingFinalizers();

        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    private static TimeSpan BenchmarkHTML5Performance(int iterations, bool multiCore, int threads = 1)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        if (multiCore)
        {
            Parallel.For(0, threads, new ParallelOptions { MaxDegreeOfParallelism = threads }, _ =>
            {
                for (int j = 0; j < iterations; j++)
                {
                    string html = GetHTMLContent();
                    string css = GetCSSContent();
                    string js = GetJSContent();

                    ParseHTML(html);
                    ParseCSS(css);
                    ExecuteJS(js);
                }
            });
        }
        else
        {
            for (int i = 0; i < iterations; i++)
            {
                string html = GetHTMLContent();
                string css = GetCSSContent();
                string js = GetJSContent();

                ParseHTML(html);
                ParseCSS(css);
                ExecuteJS(js);
            }
        }

        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    private static string GetHTMLContent()
    {
        return @"
<html>
<head>
    <title>My Blog</title>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <link rel='stylesheet' href='styles.css'>
</head>
<body>
    <header>
        <h1>Welcome to My Blog</h1>
        <nav>
            <ul>
                <li><a href='#home'>Home</a></li>
                <li><a href='#about'>About</a></li>
                <li><a href='#contact'>Contact</a></li>
            </ul>
        </nav>
    </header>
    <main>
        <section id='home'>
            <article>
                <h2>Blog Post 1</h2>
                <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Nullam non urna magna.</p>
                <button id='readMoreBtn'>Read More</button>
            </article>
            <article>
                <h2>Blog Post 2</h2>
                <p>Curabitur vitae nisi vehicula, ullamcorper lectus nec, sollicitudin lectus.</p>
            </article>
        </section>
        <section id='about'>
            <h2>About Me</h2>
            <p>This is a blog about web development and programming. Stay tuned for more updates!</p>
        </section>
        <section id='contact'>
            <h2>Contact</h2>
            <form id='contactForm'>
                <label for='name'>Name:</label>
                <input type='text' id='name' name='name' required>
                <label for='email'>Email:</label>
                <input type='email' id='email' name='email' required>
                <label for='message'>Message:</label>
                <textarea id='message' name='message' required></textarea>
                <button type='submit'>Send</button>
            </form>
        </section>
    </main>
    <footer>
        <p>&copy; 2024 My Blog. All rights reserved.</p>
    </footer>
</body>
</html>";
    }

    private static string GetCSSContent()
    {
        return @"
body {
    font-family: Arial, sans-serif;
    background-color: #f5f5f5;
    margin: 0;
    padding: 0;
}

header {
    background-color: #333;
    color: #fff;
    padding: 10px 0;
    text-align: center;
}

nav ul {
    list-style: none;
    padding: 0;
}

nav ul li {
    display: inline;
    margin: 0 10px;
}

nav ul li a {
    color: #fff;
    text-decoration: none;
}

main {
    padding: 20px;
}

article {
    margin-bottom: 20px;
    padding: 10px;
    background-color: #fff;
    border-radius: 5px;
    box-shadow: 0 0 10px rgba(0,0,0,0.1);
}

button {
    padding: 10px 20px;
    border: none;
    border-radius: 5px;
    background-color: #007bff;
    color: #fff;
    cursor: pointer;
}

button:hover {
    background-color: #0056b3;
}

footer {
    background-color: #333;
    color: #fff;
    text-align: center;
    padding: 10px 0;
    position: fixed;
    bottom: 0;
    width: 100%;
}

form {
    display: flex;
    flex-direction: column;
}

form label {
    margin: 5px 0;
}

form input, form textarea {
    margin-bottom: 10px;
    padding: 8px;
    border: 1px solid #ddd;
    border-radius: 5px;
}";
    }

    private static string GetJSContent()
    {
        return @"
document.addEventListener('DOMContentLoaded', function() {
    console.log('Page loaded');

    var readMoreBtn = document.getElementById('readMoreBtn');
    if (readMoreBtn) {
        readMoreBtn.addEventListener('click', function() {
            alert('Read More button clicked!');
        });
    }

    var contactForm = document.getElementById('contactForm');
    if (contactForm) {
        contactForm.addEventListener('submit', function(event) {
            event.preventDefault();
            alert('Form submitted');
        });
    }
});
";
    }

    static Dictionary<string, List<Dictionary<string, string>>> ParseHTML(string html)
    {
        var tagPattern = @"<(?<tag>\w+)(?<attributes>[^>]*)>";
        var matches = Regex.Matches(html, tagPattern);
        var parsedElements = new Dictionary<string, List<Dictionary<string, string>>>();
        var tagHierarchy = new Stack<string>();

        foreach (Match match in matches)
        {
            var tag = match.Groups["tag"].Value;
            var attributes = match.Groups["attributes"].Value;
            var attributeDetails = new Dictionary<string, string>();

            var attributePattern = @"(\w+)=['""]?([^'""]+)['""]?";
            var attributeMatches = Regex.Matches(attributes, attributePattern);
            foreach (Match attributeMatch in attributeMatches)
            {
                var attributeName = attributeMatch.Groups[1].Value;
                var attributeValue = attributeMatch.Groups[2].Value;
                attributeDetails[attributeName] = attributeValue;
            }

            if (!tag.StartsWith("/"))
            {
                if (!parsedElements.ContainsKey(tag))
                {
                    parsedElements[tag] = new List<Dictionary<string, string>>();
                }
                parsedElements[tag].Add(attributeDetails);
                tagHierarchy.Push(tag);
            }
            else
            {
                if (tagHierarchy.Count > 0 && tagHierarchy.Peek() == tag.TrimStart('/'))
                {
                    tagHierarchy.Pop();
                }
            }
        }

        return parsedElements;
    }

    static Dictionary<string, Dictionary<string, string>> ParseCSS(string css)
    {
        var cssPattern = @"(?<selector>[^\{\}]+)\{(?<properties>[^\{\}]+)\}";
        var matches = Regex.Matches(css, cssPattern);
        var parsedStyles = new Dictionary<string, Dictionary<string, string>>();

        foreach (Match match in matches)
        {
            var selector = match.Groups["selector"].Value.Trim();
            var properties = match.Groups["properties"].Value.Trim();
            var propertyDetails = new Dictionary<string, string>();

            var propertyPattern = @"(\w+[-\w]*)\s*:\s*([^;]+);?";
            var propertyMatches = Regex.Matches(properties, propertyPattern);
            foreach (Match propertyMatch in propertyMatches)
            {
                var propertyName = propertyMatch.Groups[1].Value;
                var propertyValue = propertyMatch.Groups[2].Value;
                propertyDetails[propertyName] = propertyValue;
            }

            parsedStyles[selector] = propertyDetails;
        }

        return parsedStyles;
    }

    static Dictionary<string, string> ExecuteJS(string js)
    {
        var functionPattern = @"function\s+(\w+)\s*\((.*?)\)\s*\{(.*?)\}";
        var matches = Regex.Matches(js, functionPattern, RegexOptions.Singleline);
        var functions = new Dictionary<string, string>();
        var eventHandlers = new Dictionary<string, string>();

        foreach (Match match in matches)
        {
            var functionName = match.Groups[1].Value;
            var body = match.Groups[3].Value;
            functions[functionName] = body;

            var eventPattern = @"document\.addEventListener\(['""](\w+)['""],\s*function\s*\(\)\s*\{(.*?)\}\);";
            var eventMatches = Regex.Matches(body, eventPattern, RegexOptions.Singleline);
            foreach (Match eventMatch in eventMatches)
            {
                var eventType = eventMatch.Groups[1].Value;
                var eventBody = eventMatch.Groups[2].Value;
                eventHandlers[eventType] = eventBody;
            }
        }

        if (eventHandlers.ContainsKey("DOMContentLoaded"))
        {
            string domContentLoadedHandler = eventHandlers["DOMContentLoaded"];
        }

        return eventHandlers;
    }

    private static TimeSpan BenchmarkNeuralNetworkProcessing(int iterations, bool multiCore, int threads = 1)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        if (multiCore)
        {
            Parallel.For(0, threads, new ParallelOptions { MaxDegreeOfParallelism = threads }, _ =>
            {
                for (int j = 0; j < iterations; j++)
                {
                    ExecuteNeuralNetworkIteration();
                }
            });
        }
        else
        {
            for (int i = 0; i < iterations; i++)
            {
                ExecuteNeuralNetworkIteration();
            }
        }

        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    private static void ExecuteNeuralNetworkIteration()
    {
        int inputSize = 256;
        int hiddenSize1 = 128;
        int hiddenSize2 = 64;
        int outputSize = 32;

        float[,] input = new float[inputSize, 1];
        float[,] hiddenWeights1 = new float[hiddenSize1, inputSize];
        float[,] hiddenWeights2 = new float[hiddenSize2, hiddenSize1];
        float[,] outputWeights = new float[outputSize, hiddenSize2];

        float[,] hiddenOutput1 = new float[hiddenSize1, 1];
        float[,] hiddenOutput2 = new float[hiddenSize2, 1];
        float[,] finalOutput = new float[outputSize, 1];

        Random rand = new Random();
        InitializeArray(input, rand);
        InitializeArray(hiddenWeights1, rand);
        InitializeArray(hiddenWeights2, rand);
        InitializeArray(outputWeights, rand);

        ComputeLayer(hiddenWeights1, input, hiddenOutput1, ReLU);
        ComputeLayer(hiddenWeights2, hiddenOutput1, hiddenOutput2, Sigmoid);
        ComputeFinalLayer(outputWeights, hiddenOutput2, finalOutput, outputSize);
    }

    private static void InitializeArray(float[,] array, Random rand)
    {
        for (int x = 0; x < array.GetLength(0); x++)
        {
            for (int y = 0; y < array.GetLength(1); y++)
            {
                array[x, y] = (float)rand.NextDouble();
            }
        }
    }

    private static void ComputeLayer(float[,] weights, float[,] input, float[,] output, Func<float, float> activation)
    {
        for (int x = 0; x < output.GetLength(0); x++)
        {
            output[x, 0] = 0;
            for (int y = 0; y < weights.GetLength(1); y++)
            {
                output[x, 0] += weights[x, y] * input[y, 0];
            }
            output[x, 0] = activation(output[x, 0]);
        }
    }

    private static void ComputeFinalLayer(float[,] weights, float[,] input, float[,] output, int outputSize)
    {
        for (int x = 0; x < output.GetLength(0); x++)
        {
            output[x, 0] = 0;
            for (int y = 0; y < weights.GetLength(1); y++)
            {
                output[x, 0] += weights[x, y] * input[y, 0];
            }
            output[x, 0] = Softmax(output, x, outputSize);
        }
    }

    static float ReLU(float x) => Math.Max(0, x);

    static float Sigmoid(float x) => 1 / (1 + MathF.Exp(-x));

    static float Softmax(float[,] output, int index, int outputSize)
    {
        float max = float.MinValue;
        for (int i = 0; i < outputSize; i++)
        {
            if (output[i, 0] > max) max = output[i, 0];
        }

        float sumExp = 0;
        for (int i = 0; i < outputSize; i++)
        {
            sumExp += MathF.Exp(output[i, 0] - max);
        }

        return MathF.Exp(output[index, 0] - max) / sumExp;
    }

    private static TimeSpan BenchmarkVideoTranscoding(int iterations, bool multiCore, int threads = 1)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        int width = 3840;
        int height = 2160;
        int frames = 60;

        if (multiCore)
        {
            Parallel.For(0, threads, new ParallelOptions { MaxDegreeOfParallelism = threads }, _ =>
            {
                for (int j = 0; j < iterations; j++)
                {
                    ProcessFrames(frames, width, height);
                }
            });
        }
        else
        {
            for (int i = 0; i < iterations; i++)
            {
                ProcessFrames(frames, width, height);
            }
        }

        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    private static void ProcessFrames(int frames, int width, int height)
    {
        for (int f = 0; f < frames; f++)
        {
            float[,] frame = new float[width, height];
            for (int i = 0; i < width; i++)
            {
                for (int k = 0; k < height; k++)
                {
                    float pixelValue = (i * k) % 255;
                    float filteredValue = ApplyFilters(pixelValue);
                    frame[i, k] = filteredValue;
                }
            }
        }
    }

    private static float ApplyFilters(float pixelValue)
    {
        pixelValue = MathF.Sqrt(pixelValue) * MathF.Sin(pixelValue) * MathF.Cos(pixelValue);
        pixelValue = (pixelValue * 2.0f) + MathF.Log(pixelValue + 1.0f);
        pixelValue = (pixelValue / 255.0f) * 1.5f - 0.5f;
        pixelValue = MathF.Max(0, MathF.Min(255, pixelValue * 255));
        return pixelValue;
    }

    private static TimeSpan BenchmarkThreadCommunication(int iterations, int threads = 1)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        object lockObject = new object();
        int[] sharedData = new int[threads];
        int sharedCounter = 0;

        Parallel.For(0, threads, new ParallelOptions { MaxDegreeOfParallelism = threads }, threadIndex =>
        {
            for (int j = 0; j < iterations; j++)
            {
                int localData;
                lock (lockObject)
                {
                    sharedCounter++;
                    sharedData[threadIndex] = sharedCounter;
                    localData = sharedData[(threadIndex + 1) % threads];
                }

                lock (lockObject)
                {
                    sharedData[threadIndex] += localData;
                }
            }
        });

        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    static double CalculateScore(double timeMilliseconds, int logicalProcessorCount, bool isMultiCore)
    {
        double score = 1000000 / timeMilliseconds;
        if (isMultiCore)
        {
            score *= logicalProcessorCount;
        }
        return Math.Round(score);
    }
}