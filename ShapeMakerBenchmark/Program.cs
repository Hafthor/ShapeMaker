using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using ShapeMaker;

namespace ShapeMakerBenchmark;

[MemoryDiagnoser]
public class Program {
    public static void Main() {
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
    [Benchmark] //  78.75ns  107.91ns
    public void RotateX2() => shape555.RotateX2();

    [Benchmark] //  63.43ns   93.42ns
    public void MirrorX() => shape555.MirrorX();

    [Benchmark] //  67.867ns  109.72ns
    public void MirrorY() => shape555.MirrorY();

    [Benchmark] //  96.87ns  146.59ns
    public void MirrorZ() => shape555.MirrorZ();

    [Benchmark] //  98.71ns  117.55ns
    public void CornerEdgeFaceCount() => shape555.CornerEdgeFaceCount();

    [Benchmark] //  41.43ns   56.67ns
    public void CornerEdgeCount() => shape555.CornerEdgeCount();

    [Benchmark] //   4.72ns   14.25ns
    public void CornerCount() => shape555.CornerCount();
    
    [Benchmark] // 112.83ns
    public void RotateX() => shape555.RotateX();
    
    [Benchmark] // 104.87ns
    public void RotateY() => shape555.RotateY();
    
    [Benchmark] // 108.88ns
    public void RotateZ() => shape555.RotateZ();
    
    [Benchmark] //  98.32ns
    public void RotateY2() => shape555.RotateY2();
    
    [Benchmark] // 111.18ns
    public void RotateZ2() => shape555.RotateZ2();
}
