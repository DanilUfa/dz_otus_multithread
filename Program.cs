using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ArraySumBenchmark;

/// <summary>Домашнее задание: сумма элементов массива int тремя способами + замер времени.</summary>
internal static class Program
{
    private static readonly int Threads = Environment.ProcessorCount;

    private static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine("Сумма массива int: последовательно / Thread / PLINQ");
        Console.WriteLine($"ОС: {RuntimeInformation.OSDescription}");
        Console.WriteLine($"Логических процессоров: {Environment.ProcessorCount}");
        Console.WriteLine();

        Console.WriteLine($"{"Размер",12} | {"Последовательно",14} | {"Thread",9} | {"PLINQ",9} | Совпадает");
        Console.WriteLine(new string('-', 62));

        foreach (int n in new[] { 100_000, 1_000_000, 10_000_000 })
        {
            int[] a = Enumerable.Range(0, n).Select(i => i % 137).ToArray();

            Warmup(a); // прогрев (JIT)

            double seq = MeasureBest(() => Sum(a));
            double par = MeasureBest(() => SumParallel(a));
            double linq = MeasureBest(() => SumLinq(a));

            bool ok = Sum(a) == SumParallel(a) && SumParallel(a) == SumLinq(a);

            Console.WriteLine($"{n,12:N0} | {seq,12:F3} ms | {par,7:F3} ms | {linq,7:F3} ms | {(ok ? "да" : "НЕТ")}");
        }
    }

    // 1. Последовательно
    private static long Sum(int[] a)
    {
        long sum = 0;
        foreach (int x in a)
            sum += x;
        return sum;
    }

    // 2. Параллельно на потоках (массив делится на части по числу логических процессоров)
    private static long SumParallel(int[] a)
    {
        int t = Math.Min(Threads, Math.Max(1, a.Length));
        int chunk = (a.Length + t - 1) / t;
        var partials = new long[t];
        var threads = new Thread[t];

        for (int i = 0; i < t; i++)
        {
            int idx = i;
            int start = idx * chunk;
            int end = Math.Min(start + chunk, a.Length);
            threads[idx] = new Thread(() =>
            {
                long s = 0;
                for (int j = start; j < end; j++)
                    s += a[j];
                partials[idx] = s;
            });
            threads[idx].Start();
        }

        long sum = 0;
        for (int i = 0; i < t; i++)
        {
            threads[i].Join();
            sum += partials[i];
        }
        return sum;
    }

    // 3. Параллельно через LINQ (PLINQ)
    private static long SumLinq(int[] a) => a.AsParallel().Sum(x => (long)x);

    private static void Warmup(int[] a)
    {
        Sum(a);
        SumParallel(a);
        SumLinq(a);
    }

    // Минимум из 5 измерений
    private static double MeasureBest(Func<long> action)
    {
        double best = double.MaxValue;
        for (int i = 0; i < 5; i++)
        {
            var sw = Stopwatch.StartNew();
            long v = action();
            sw.Stop();
            GC.KeepAlive(v);
            best = Math.Min(best, sw.Elapsed.TotalMilliseconds);
        }
        return best;
    }


}
