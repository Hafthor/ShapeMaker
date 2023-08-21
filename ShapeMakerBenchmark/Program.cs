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

    [Benchmark]
    public void RotateX2() => shape555.RotateX2();

    [Benchmark]
    public void RotateX2Opt() => shape555.RotateX2Opt();

    [Benchmark]
    public void MirrorX() => shape555.MirrorX();

    [Benchmark]
    public void MirrorXOpt() => shape555.MirrorXOpt();

    [Benchmark]
    public void MirrorY() => shape555.MirrorY();

    [Benchmark]
    public void MirrorYOpt() => shape555.MirrorYOpt();

    [Benchmark]
    public void MirrorZ() => shape555.MirrorZ();

    [Benchmark]
    public void MirrorZOpt() => shape555.MirrorZOpt();
}

