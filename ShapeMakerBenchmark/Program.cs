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

    // Benchmarks   M2Max
    [Benchmark] // 159.4ns
    public void RotateX2() => shape555.RotateX2();

    [Benchmark] // 155.0ns
    public void RotateX2Opt() => shape555.RotateX2Opt();

    [Benchmark] // 144.9ns
    public void RotateX2Opt1() => shape555.RotateX2Opt1();

    [Benchmark] // 122.0ns
    public void RotateX2Opt2() => shape555.RotateX2Opt2();

    [Benchmark] // 122.5ns
    public void RotateX2Opt3() => shape555.RotateX2Opt3();

    [Benchmark] // 119.2ns
    public void RotateX2Opt4() => shape555.RotateX2Opt4();

    [Benchmark] // 110.78ns
    public void MirrorX() => shape555.MirrorX();

    [Benchmark] // 128.04ns
    public void MirrorXOpt() => shape555.MirrorXOpt();

    [Benchmark] //  82.78ns
    public void MirrorXOpt2() => shape555.MirrorXOpt2();

    [Benchmark] // 107.82ns
    public void MirrorY() => shape555.MirrorY();

    [Benchmark] // 127.97ns
    public void MirrorYOpt() => shape555.MirrorYOpt();

    [Benchmark] // 114.8ns
    public void MirrorYOpt2() => shape555.MirrorYOpt2();

    [Benchmark] // 156.74ns
    public void MirrorZ() => shape555.MirrorZ();

    [Benchmark] // 149.78ns
    public void MirrorZOpt() => shape555.MirrorZOpt();

    [Benchmark] // 154.8ns
    public void MirrorZOpt2() => shape555.MirrorZOpt2();
}

