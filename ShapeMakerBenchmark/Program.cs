using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using ShapeMaker;

namespace ShapeMakerBenchmark;

public class Program {
    static void Main(string[] args) {
        BenchmarkRunner.Run<Program>();
    }

    private BitShape shape435;
    private BitShape shape444;
    private BitShape shape555;

    public Program() {
        Random r = new Random(420);
        shape435 = new BitShape(4, 3, 5); // 4x3x5=60 bits
        for (int i = 0; i < shape435.bytes.Length; i++)
            shape435.bytes[i] = (byte)r.Next(256);
        shape435.bytes[shape435.bytes.Length - 1] &= 0xF0;

        shape444 = new BitShape(4, 4, 4); // 4x4x4=64 bits
        for (int i = 0; i < shape444.bytes.Length; i++)
            shape444.bytes[i] = (byte)r.Next(256);

        shape555 = new BitShape(5, 5, 5); // 5x5x5=125 bits
        for (int i = 0; i < shape555.bytes.Length; i++)
            shape555.bytes[i] = (byte)r.Next(256);
        shape555.bytes[shape555.bytes.Length - 1] &= 0xF8;
    }

    // Benchmarks   M2Max    Intel
    [Benchmark] //  98.87ns  180.76ns
    public void RotateX2Org() => shape555.RotateX2Org();

    [Benchmark] //  92.26ns  107.91ns <= best Intel
    public void RotateX2Opt() => shape555.RotateX2Opt();

    [Benchmark] //  92.07ns  108.13ns
    public void RotateX2Opt1() => shape555.RotateX2Opt1();

    [Benchmark] //  77.11ns  140.56ns
    public void RotateX2Opt2() => shape555.RotateX2Opt2();

    [Benchmark] //  77.11ns  140.60ns
    public void RotateX2Opt3() => shape555.RotateX2Opt3();

    [Benchmark] //  76.68ns  142.00ns
    public void RotateX2Opt4() => shape555.RotateX2();

    [Benchmark] //  76.49ns  134.09ns <= best M2Max
    public void RotateX2Opt5() => shape555.RotateX2Opt5();

    [Benchmark] // 110.18ns  180.48ns
    public void RotateX2Opt6() => shape555.RotateX2Opt6();

    [Benchmark] // 115.64ns  180.01ns
    public void RotateX2Opt7() => shape555.RotateX2Opt7();

    [Benchmark] //  73.53ns  127.15ns
    public void MirrorXOrg() => shape555.MirrorXOrg();

    [Benchmark] //  78.06ns   93.42ns <= best Intel
    public void MirrorXOpt() => shape555.MirrorXOpt();

    [Benchmark] //  64.45ns  115.81ns <= best M2Max
    public void MirrorXOpt2() => shape555.MirrorX();


    [Benchmark] //  85.09ns  155.28ns
    public void MirrorYOrg() => shape555.MirrorY();

    [Benchmark] //  90.86ns  109.72ns <= best Intel
    public void MirrorYOpt() => shape555.MirrorYOpt();

    [Benchmark] //  71.57ns  124.18ns <= best M2Max
    public void MirrorYOpt2() => shape555.MirrorYOpt2();


    [Benchmark] // 106.82ns  158.32ns
    public void MirrorZOrg() => shape555.MirrorZOrg();

    [Benchmark] // 102.80ns  146.59ns <= best M2Max/Intel
    public void MirrorZOpt() => shape555.MirrorZ();

    [Benchmark] // 107.96ns  149.30ns
    public void MirrorZOpt2() => shape555.MirrorZOpt2();


    [Benchmark] // 210.46ns  236.48ns
    public void CornerEdgeFaceCountOpt1() => shape555.CornerEdgeFaceCountOpt1();

    [Benchmark] // 275.07ns  298.65ns
    public void CornerEdgeFaceCountOrg() => shape555.CornerEdgeFaceCountOrg();

    [Benchmark] //  98.06ns  117.55ns <= best M2Max/Intel
    public void CornerEdgeFaceCountOpt() => shape555.CornerEdgeFaceCount();

    [Benchmark] //  49.12ns   56.67ns
    public void CornerEdgeCount() => shape555.CornerEdgeCount();

    [Benchmark] //   9.95ns   14.25ns
    public void CornerCount() => shape555.CornerCount();
}
