using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using ShapeMaker;

namespace ShapeMakerBenchmark;

public class Program {
    static void Main(string[] args) {
        BenchmarkRunner.Run<Program>();
    }

    private readonly BitShape shape555;

    public Program() {
        var r = new Random(420);
        shape555 = new BitShape(5, 5, 5); // 5x5x5=125 bits
        for (int i = 0; i < shape555.bytes.Length; i++)
            shape555.bytes[i] = (byte)r.Next(256);
        shape555.bytes[^1] &= 0xF8;
    }

    // Benchmarks   M2Max    Intel
    [Benchmark] //  76.49ns  107.91ns
    public void RotateX2() => shape555.RotateX2();

    [Benchmark] //  64.45ns   93.42ns
    public void MirrorX() => shape555.MirrorX();

    [Benchmark] //  71.57ns  109.72ns
    public void MirrorY() => shape555.MirrorY();

    [Benchmark] // 102.80ns  146.59ns
    public void MirrorZ() => shape555.MirrorZ();

    [Benchmark] //  98.06ns  117.55ns
    public void CornerEdgeFaceCount() => shape555.CornerEdgeFaceCount();

    [Benchmark] //  49.12ns   56.67ns
    public void CornerEdgeCount() => shape555.CornerEdgeCount();

    [Benchmark] //   9.95ns   14.25ns
    public void CornerCount() => shape555.CornerCount();
}
