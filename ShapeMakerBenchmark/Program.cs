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
    [Benchmark] // 159.4ns  180.76ns
    public void RotateX2Org() => shape555.RotateX2Org();

    [Benchmark] // 155.0ns  107.91ns <= best Intel
    public void RotateX2Opt() => shape555.RotateX2Opt();

    [Benchmark] // 144.9ns  108.13ns
    public void RotateX2Opt1() => shape555.RotateX2Opt1();

    [Benchmark] // 122.0ns  140.56ns
    public void RotateX2Opt2() => shape555.RotateX2Opt2();

    [Benchmark] // 122.5ns  140.60ns
    public void RotateX2Opt3() => shape555.RotateX2Opt3();

    [Benchmark] // 119.2ns  142.00ns <= best M2Max
    public void RotateX2Opt4() => shape555.RotateX2();

    [Benchmark] //
    public void RotateX2Opt5() => shape555.RotateX2Opt5();

    [Benchmark] //
    public void RotateX2Opt6() => shape555.RotateX2Opt6();

    [Benchmark] //
    public void RotateX2Opt7() => shape555.RotateX2Opt7();

    [Benchmark] // 110.78ns  127.15ns
    public void MirrorXOrg() => shape555.MirrorXOrg();

    [Benchmark] // 128.04ns   93.42ns <= best Intel
    public void MirrorXOpt() => shape555.MirrorXOpt();

    [Benchmark] //  82.78ns  115.81ns <= best M2Max
    public void MirrorXOpt2() => shape555.MirrorX();


    [Benchmark] // 107.82ns  155.28ns <= best M2Max
    public void MirrorYOrg() => shape555.MirrorY();

    [Benchmark] // 127.97ns  109.72ns <= best Intel
    public void MirrorYOpt() => shape555.MirrorYOpt();

    [Benchmark] // 114.8ns   124.18ns
    public void MirrorYOpt2() => shape555.MirrorYOpt2();


    [Benchmark] // 156.74ns  158.32ns
    public void MirrorZOrg() => shape555.MirrorZOrg();

    [Benchmark] // 149.78ns  146.59ns <= best M2Max/Intel
    public void MirrorZOpt() => shape555.MirrorZ();

    [Benchmark] // 154.8ns   149.30ns
    public void MirrorZOpt2() => shape555.MirrorZOpt2();


    [Benchmark] // 221.0ns   236.48ns
    public void CornerEdgeFaceCount() => shape555.CornerEdgeFaceCount();

    [Benchmark] // 290.0ns   298.65ns
    public void CornerEdgeFaceCountOrg() => shape555.CornerEdgeFaceCountOrg();

    [Benchmark] //           117.55ns <= best M2Max/Intel
    public void CornerEdgeFaceCountOpt() => shape555.CornerEdgeFaceCountOpt();

    [Benchmark] //            14.25ns
    public void CornerCount() => shape555.CornerCount();
}
