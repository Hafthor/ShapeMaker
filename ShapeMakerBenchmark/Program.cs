using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using ShapeMaker;

namespace ShapeMakerBenchmark;

public class Program {
    static void Main(string[] args) {
        BenchmarkRunner.Run<Program>();
    }

    private BitShape shape435, shape444;
    public Program() {
        Random r = new Random(420);
        shape435 = new BitShape(4, 3, 5); // 4x3x5=60 bits
        for (int i = 0; i < shape435.bytes.Length; i++)
            shape435.bytes[i] = (byte)r.Next(256);
        shape435.bytes[shape435.bytes.Length - 1] &= 0xF0;

        shape444 = new BitShape(4, 4, 4); // 4x4x4=64 bits
        for (int i = 0; i < shape444.bytes.Length; i++)
            shape444.bytes[i] = (byte)r.Next(256);
    }
}

