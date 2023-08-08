using System.Diagnostics;

namespace ShapeMaker;

public class Program {
    public const string FILE_PATH = "/Users/hafthor/dev/ShapeMaker/";
    public const string FILE_EXT = ".bin";
    public const string FILE_COMPLETE = "_COMPLETE";

    /*
        Results and timing (in seconds) from 14" 2023 MacBook Pro w/ 96GB 12-core M2 Max, .NET 7 in Release mode
        n=2, shapes: 1 time: 0.0669563, chiral shapes: 1 time: 0.0008728
        n=3, shapes: 2 time: 0.002236, chiral shapes: 2 time: 0.025654
        n=4, shapes: 8 time: 0.0779982, chiral shapes: 7 time: 0.0815542
        n=5, shapes: 29 time: 0.0410423, chiral shapes: 23 time: 0.0287943
        n=6, shapes: 166 time: 0.0917942, chiral shapes: 112 time: 0.032371
        n=7, shapes: 1,023 time: 0.1034941, chiral shapes: 607 time: 0.0179211
        n=8, shapes: 6,922 time: 0.1850789, chiral shapes: 3,811 time: 0.3223144
        n=9, shapes: 48,311 time: 0.3982609, chiral shapes: 25,413 time: 0.40237
        n=10, shapes: 346,543 time: 2.1523452, chiral shapes: 178,083 time: 1.0884017
        n=11, shapes: 2,522,522 time: 13.9907285, chiral shapes: 1,279,537 time: 7.7011648
        n=12, shapes: 18,598,427 time: 115.7188455, chiral shapes: 9,371,094 time: 63.3039254
        n=13, shapes: 138,462,649 time: 1022.8139417, chiral shapes: 69,513,546 time: 560.3390394
        n=14, shapes: 1,039,496,297 time: 9123.6015745, chiral shapes: 520,878,101 time: 5067.8353845
        Peak memory usage: ~10GB
     */

    // Potential Optimizations / Enhancements:
    // * It would be nice if we could have a % complete rather than just current count showing progress.

    // Potential Features:
    // * Make a 4-D version?

    static void Main(string[] args) {
        bool recompute = true;

        BitShape shape1 = new BitShape("1,1,1,*");
        FileWriter.Clear(1);
        using (var fw = new FileWriter(1, 1, 1, 1))
            fw.Write(shape1.bytes);
        MarkNComplete(1, "n=1, shapes: 1, chiral shapes: 1 time: 0");

        for (byte n = 2; n < 20; n++) {
            string s = NCompleteString(n);
            if (recompute || s == null) {
                FileWriter.Clear(n);
                s = "n=" + n + ", shapes: ";
                Console.Write(s);
                Stopwatch sw = Stopwatch.StartNew();
                var list = new FileScanner((byte)(n - 1)).List;
                var targetSizes = ShapeSizesFromExtendingShapes(list);
                long shapeCount = 0, chiralCount = 0;
                foreach (var sz in targetSizes) {
                    using (var fw = new FileWriter(n, sz.w, sz.h, sz.d)) {
                        var counts = ShapesFromExtendingShapes(list, fw, sz.w, sz.h, sz.d);
                        shapeCount += counts.shapeCount;
                        chiralCount += counts.chiralCount;
                        var sss = " [" + shapeCount.ToString("N0") + ", " + chiralCount.ToString("N0") + ", " + sw.Elapsed.TotalSeconds.ToString("N0") + "s]";
                        Console.Write(sss + new string('\b', sss.Length));
                    }
                }
                sw.Stop();
                string ss = shapeCount.ToString("N0") + ", chiral shapes: " + chiralCount.ToString("N0") + " time: " + sw.Elapsed.TotalSeconds;
                s += ss;
                Console.Write(ss);

                Console.WriteLine();
                MarkNComplete(n, s);
            } else {
                Console.WriteLine(s);
            }
        }
    }

    private static string NCompleteString(int n) {
        return File.Exists(Program.FILE_PATH + n + "/" + Program.FILE_COMPLETE) ? File.ReadAllText(Program.FILE_PATH + n + "/" + Program.FILE_COMPLETE) : null;
    }

    private static void MarkNComplete(int n, string s) {
        File.WriteAllText(Program.FILE_PATH + n + "/" + Program.FILE_COMPLETE, s);
    }

    private static HashSet<byte[]> LoadShapesHashSet(int n, int w, int h, int d) {
        var hash = new HashSet<byte[]>(ByteArrayEqualityComparer.Instance);
        if (FileReader.FileExists(n, w, h, d))
            foreach (var shape in LoadShapes(n, w, h, d))
                hash.Add(shape);
        return hash;
    }

    private static IEnumerable<byte[]> LoadShapes(int n, int w, int h, int d) {
        using (var fr = new FileReader(n, w, h, d))
            for (; ; ) {
                var l = fr.Read();
                if (l == null) break;
                yield return l;
            }
    }

    public static IEnumerable<(byte w, byte h, byte d)> ShapeSizesFromExtendingShapes(IEnumerable<FileScanner.Results> filelist) {
        var hash = new HashSet<int>();
        foreach (var r in filelist)
            foreach (var sz in ShapeSizesFromExtendingShapes(r))
                hash.Add(sz.w * 65536 + sz.h * 256 + sz.d);
        return hash.OrderBy(i => i).Select(i => ((byte)((i >> 16) & 0xFF), (byte)((i >> 8) & 0xFF), (byte)(i & 0xFF)));
    }

    public static IEnumerable<(byte w, byte h, byte d)> ShapeSizesFromExtendingShapes(FileScanner.Results file) {
        byte n = file.n, w = file.w, h = file.h, d = file.d;
        if (n < w * h * d) yield return (w, h, d);
        yield return MinRotation((byte)(w + 1), h, d);
        yield return MinRotation(w, (byte)(h + 1), d);
        yield return MinRotation(w, h, (byte)(d + 1));
    }

    public static (byte w, byte h, byte d) MinRotation(byte w, byte h, byte d) {
        if (w <= h && h <= d)
            return (w, h, d); // 1,2,3 - no rotation
        else if (w <= h && w <= d)
            return (w, d, h); // 1,3,2 - x
        else if (d < h && h < w)
            return (d, h, w); // 3,2,1 - y
        else if (d < h && d < w)
            return (d, w, h); // 3,1,2 - xy
        else if (w <= d)
            return (h, w, d); // 2,1,3 - z
        else
            return (h, d, w); // 2,3,1 - yx
    }

    // for each shape in parallel, try to add cube to it
    // first does by adding cube to the shape in its current size
    // then tries padding each of the 6 faces of the shape and adding a cube there
    public static (long shapeCount, long chiralCount) ShapesFromExtendingShapes(IEnumerable<FileScanner.Results> filelist, FileWriter fw, byte w, byte h, byte d) {
        var newShapes = new HashSet<byte[]>(ByteArrayEqualityComparer.Instance);
        foreach (var r in filelist)
            ShapesFromExtendingShapes(r, fw, newShapes, w, h, d);

        Console.Write("$");
        long chiralCount = 0;
        Parallel.ForEach(newShapes, (shapeBytes) => {
            var shape = new BitShape(w, h, d, shapeBytes);
            var newShape = new BitShape(shape).MinChiralRotation();
            if (shape.Equals(newShape)) Interlocked.Increment(ref chiralCount);
        });
        Console.Write("\b");
        return (newShapes.LongCount(), chiralCount);
    }

    public static void ShapesFromExtendingShapes(FileScanner.Results file, FileWriter fw, HashSet<byte[]> newShapes, byte tw, byte th, byte td) {
        byte n = file.n, w = file.w, h = file.h, d = file.d;

        if (w == tw && h == th && d == td) {
            Console.Write("|\b");
            Parallel.ForEach(LoadShapes(n, w, h, d), (shapeBytes) => {
                AddShapes(newShapes, fw, new BitShape(w, h, d, shapeBytes), 0, w, 0, h, 0, d); // unpadded
            });
        }

        var (ww, hh, dd) = MinRotation((byte)(w + 1), h, d);
        if (ww == tw && hh == th && dd == td) {
            Console.Write("/\b");
            Parallel.ForEach(LoadShapes(n, w, h, d), (shapeBytes) => {
                BitShape shape = new BitShape(w, h, d, shapeBytes);
                AddShapes(newShapes, fw, shape.PadLeft(), 0, 1, 0, h, 0, d);
                AddShapes(newShapes, fw, shape.PadRight(), w, w + 1, 0, h, 0, d);
            });
        }

        (ww, hh, dd) = MinRotation(w, (byte)(h + 1), d);
        if (ww == tw && hh == th && dd == td) {
            Console.Write("-\b");
            Parallel.ForEach(LoadShapes(n, w, h, d), (shapeBytes) => {
                BitShape shape = new BitShape(w, h, d, shapeBytes);
                AddShapes(newShapes, fw, shape.PadTop(), 0, w, 0, 1, 0, d);
                AddShapes(newShapes, fw, shape.PadBottom(), 0, w, h, h + 1, 0, d);
            });
        }

        (ww, hh, dd) = MinRotation(w, h, (byte)(d + 1));
        if (ww == tw && hh == th && dd == td) {
            Console.Write("\\\b");
            Parallel.ForEach(LoadShapes(n, w, h, d), (shapeBytes) => {
                BitShape shape = new BitShape(w, h, d, shapeBytes);
                AddShapes(newShapes, fw, shape.PadFront(), 0, w, 0, h, 0, 1);
                AddShapes(newShapes, fw, shape.PadBack(), 0, w, 0, h, d, d + 1);
            });
        }
    }

    // for each blank cube from x0 to w, y0 to h, z0 to d, if it has an adjacent neighbor
    // add that cube, find the minimum rotation, and add to the newShapes hash set (under
    // lock since we could be doing this in parallel.)
    private static void AddShapes(HashSet<byte[]> newShapes, FileWriter fw, BitShape shape, int x0, int w, int y0, int h, int z0, int d) {
        for (var x = x0; x < w; x++)
            for (var y = y0; y < h; y++)
                for (var z = z0; z < d; z++)
                    if (!shape.Get(x, y, z))
                        if (shape.HasSetNeighbor(x, y, z)) {
                            var newShape = new BitShape(shape);
                            newShape.Set(x, y, z, true);
                            var s = newShape.MinRotation();
                            bool writeShape = false;
                            lock (newShapes) {
                                writeShape = newShapes.Add(s.bytes);
                            }
                            if (writeShape)
                                lock (fw)
                                    fw.Write(s.bytes);
                        }
    }
}

public class ByteArrayEqualityComparer : IEqualityComparer<byte[]> {
    public static readonly ByteArrayEqualityComparer Instance = new ByteArrayEqualityComparer();

    bool IEqualityComparer<byte[]>.Equals(byte[]? x, byte[]? y) {
        return x is not null && y is not null && x.SequenceEqual(y);
    }

    int IEqualityComparer<byte[]>.GetHashCode(byte[] obj) {
        unchecked { // Modified FNV Hash
            const int p = 16777619;
            int hash = (int)2166136261;

            for (int i = 0, l = obj.Length; i < l; i++)
                hash = (hash ^ obj[i]) * p;

            return hash;
        }
    }
}

public class FileScanner {
    public class Results {
        public byte w, h, d, n;
        public string filepath => Program.FILE_PATH + n + "/" + w + "," + h + "," + d + Program.FILE_EXT;
    }

    public readonly List<Results> List = new();

    public FileScanner(byte n) {
        var di = new DirectoryInfo(Program.FILE_PATH + n);
        var files = di.GetFiles("*" + Program.FILE_EXT).OrderBy(f => f.Length);
        foreach (var file in files) {
            if (file.Name.EndsWith(Program.FILE_EXT)) {
                var dim = file.Name.Substring(0, file.Name.Length - Program.FILE_EXT.Length).Split(',');
                if (dim.Length != 3) continue;
                if (!byte.TryParse(dim[0], out var w) || w < 1 || w > n) continue;
                if (!byte.TryParse(dim[1], out var h) || h < 1 || h > n) continue;
                if (!byte.TryParse(dim[2], out var d) || d < 1 || d > n) continue;
                List.Add(new Results() { n = n, w = w, h = h, d = d });
            }
        }
    }
}

public class FileWriter : IDisposable {
    private FileStream fs = null;
    private readonly int length;
    private readonly string path;

    public static void Clear(byte n) {
        var di = new DirectoryInfo(Program.FILE_PATH + n);
        if (!di.Exists)
            di.Create();
        else {
            var list = new FileScanner(n).List;
            foreach (var f in list)
                File.Delete(f.filepath);
            if (File.Exists(Program.FILE_PATH + n + "/" + Program.FILE_COMPLETE))
                File.Delete(Program.FILE_PATH + n + "/" + Program.FILE_COMPLETE);
        }
    }

    public FileWriter(int n, int w, int h, int d) {
        path = Program.FILE_PATH + n + "/" + w + "," + h + "," + d + Program.FILE_EXT;
        length = (w * h * d + 7) / 8;
    }

    public void Write(byte[] shape) {
        if (shape.Length != length) throw new ArgumentOutOfRangeException(nameof(shape), shape.Length, "unexpected shape length - should be " + length);
        if (fs == null)
            fs = File.Open(path, FileMode.Append);
        fs.Write(shape);
    }

    public void Dispose() {
        fs?.Dispose();
    }
}

public class FileReader : IDisposable {
    private readonly FileStream fs;
    private readonly int length;

    public static bool FileExists(int n, int w, int h, int d) => File.Exists(Program.FILE_PATH + n + "/" + w + "," + h + "," + d + Program.FILE_EXT);

    public FileReader(int n, int w, int h, int d) {
        fs = File.OpenRead(Program.FILE_PATH + n + "/" + w + "," + h + "," + d + Program.FILE_EXT);
        length = (w * h * d + 7) / 8;
    }

    public byte[] Read() {
        byte[] bytes = new byte[length];
        if (fs.Read(bytes) < length) return null;
        return bytes;
    }

    public void Dispose() {
        fs.Dispose();
    }
}
