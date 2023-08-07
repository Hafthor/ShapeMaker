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

    // Potential Optimizations:
    // * Avoid hash reloading by rereading previous results instead. We could start by figuring out what target
    //   files we plan to make, then for each output, check to see if an input can add to that output and if so
    //   let it do so. Hash reloading is a comparitively small amount of the time taken, but it would mean that
    //   we could potentially calculate for one n greater than we have disk space for since we could skip storing
    //   on that final n and still end up with an accurate count... we wouldn't be able to compute chiral count.
    // * Compute chiral count inline - especially easy if we do the above optimization. While hash still in
    //   memory, we just count the number of shapes that are already at their minimal chiral rotation.

    // Potential Features:
    // * Make a 4-D version?

    static void Main(string[] args) {
        bool recompute = true;
        bool doChiral = true;

        BitShape shape1 = new BitShape("1,1,1,*");
        FileWriter.Clear(1);
        using (var fw = new FileWriter(1, 1, 1, 1))
            fw.Write(shape1.bytes);
        MarkNComplete(1, "n=1, shapes: 1 time: 0, chiral shapes: 1 time: 0");

        for (byte n = 2; n < 20; n++) {
            string s = NCompleteString(n);
            if (recompute || s == null) {
                FileWriter.Clear(n);
                s = "n=" + n + ", shapes: ";
                Console.Write(s);
                Stopwatch sw = Stopwatch.StartNew();
                var list = new FileScanner((byte)(n - 1)).List;
                long shapeCount = ShapesFromExtendingShapes(list, sw);
                sw.Stop();
                string ss = shapeCount.ToString("N0") + " time: " + sw.Elapsed.TotalSeconds;
                s += ss;
                Console.Write(ss);

                if (doChiral) {
                    ss = ", chiral shapes: ";
                    s += ss;
                    Console.Write(ss);
                    Stopwatch sw2 = Stopwatch.StartNew();
                    var list2 = new FileScanner(n).List;
                    long chiralShapeCount = ChiralShapes(list2, sw2);
                    sw2.Stop();
                    ss = chiralShapeCount.ToString("N0") + " time: " + sw2.Elapsed.TotalSeconds;
                    s += ss;
                    Console.Write(ss);
                }

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

    // for each shape in parallel, try to add cube to it
    // first does by adding cube to the shape in its current size
    // then tries padding each of the 6 faces of the shape and adding a cube there
    public static long ShapesFromExtendingShapes(IEnumerable<FileScanner.Results> filelist, Stopwatch sw) {
        long shapeCount = 0;
        foreach (var r in filelist)
            ShapesFromExtendingShapes(r, sw, ref shapeCount);
        return shapeCount;
    }

    public static (int w, int h, int d) MinRotation(int w, int h, int d) {
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

    public static void ShapesFromExtendingShapes(FileScanner.Results file, Stopwatch sw, ref long shapeCount) {
        byte n = file.n, w = file.w, h = file.h, d = file.d;

        Console.Write("|");
        var newShapes = LoadShapesHashSet(n + 1, w, h, d);
        Console.Write("\b");
        using (var fw = new FileWriter(n + 1, w, h, d)) {
            long addShapeCount = 0;
            Parallel.ForEach(LoadShapes(n, w, h, d), (shapeBytes) => {
                Interlocked.Add(ref addShapeCount, AddShapes(newShapes, fw, new BitShape(w, h, d, shapeBytes), 0, w, 0, h, 0, d)); // unpadded
            });
            shapeCount += addShapeCount;
            var ss = " [" + shapeCount + "," + sw.Elapsed.TotalSeconds.ToString("0") + "s]";
            Console.Write(ss + new string('\b', ss.Length));
        }

        Console.Write("/");
        newShapes.Clear();
        var (ww, hh, dd) = MinRotation(w + 1, h, d);
        newShapes = LoadShapesHashSet(n + 1, ww, hh, dd);
        Console.Write("\b");
        using (var fw = new FileWriter(n + 1, ww, hh, dd)) {
            long addShapeCount = 0;
            Parallel.ForEach(LoadShapes(n, w, h, d), (shapeBytes) => {
                BitShape shape = new BitShape(w, h, d, shapeBytes);
                long add1 = AddShapes(newShapes, fw, shape.PadLeft(), 0, 1, 0, h, 0, d);
                long add2 = AddShapes(newShapes, fw, shape.PadRight(), w, w + 1, 0, h, 0, d);
                Interlocked.Add(ref addShapeCount, add1 + add2);
            });
            shapeCount += addShapeCount;
            var ss = " [" + shapeCount + "," + sw.Elapsed.TotalSeconds.ToString("0") + "s]";
            Console.Write(ss + new string('\b', ss.Length));
        }

        Console.Write("-");
        newShapes.Clear();
        (ww, hh, dd) = MinRotation(w, h + 1, d);
        newShapes = LoadShapesHashSet(n + 1, ww, hh, dd);
        Console.Write("\b");
        using (var fw = new FileWriter(n + 1, ww, hh, dd)) {
            long addShapeCount = 0;
            Parallel.ForEach(LoadShapes(n, w, h, d), (shapeBytes) => {
                BitShape shape = new BitShape(w, h, d, shapeBytes);
                long add1 = AddShapes(newShapes, fw, shape.PadTop(), 0, w, 0, 1, 0, d);
                long add2 = AddShapes(newShapes, fw, shape.PadBottom(), 0, w, h, h + 1, 0, d);
                Interlocked.Add(ref addShapeCount, add1 + add2);
            });
            shapeCount += addShapeCount;
            var ss = " [" + shapeCount + "," + sw.Elapsed.TotalSeconds.ToString("0") + "s]";
            Console.Write(ss + new string('\b', ss.Length));
        }

        Console.Write("\\");
        newShapes.Clear();
        (ww, hh, dd) = MinRotation(w, h, d + 1);
        newShapes = LoadShapesHashSet(n + 1, ww, hh, dd);
        Console.Write("\b");
        using (var fw = new FileWriter(n + 1, ww, hh, dd)) {
            long addShapeCount = 0;
            Parallel.ForEach(LoadShapes(n, w, h, d), (shapeBytes) => {
                BitShape shape = new BitShape(w, h, d, shapeBytes);
                long add1 = AddShapes(newShapes, fw, shape.PadFront(), 0, w, 0, h, 0, 1);
                long add2 = AddShapes(newShapes, fw, shape.PadBack(), 0, w, 0, h, d, d + 1);
                Interlocked.Add(ref addShapeCount, add1 + add2);
            });
            shapeCount += addShapeCount;
            var ss = " [" + shapeCount + "," + sw.Elapsed.TotalSeconds.ToString("0") + "s]";
            Console.Write(ss + new string('\b', ss.Length));
        }
    }

    // for each blank cube from x0 to w, y0 to h, z0 to d, if it has an adjacent neighbor
    // add that cube, find the minimum rotation, and add to the newShapes hash set (under
    // lock since we could be doing this in parallel.)
    private static long AddShapes(HashSet<byte[]> newShapes, FileWriter fw, BitShape shape, int x0, int w, int y0, int h, int z0, int d) {
        long shapeCount = 0;
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
                                if (writeShape) shapeCount++;
                            }
                            if (writeShape)
                                lock (fw)
                                    fw.Write(s.bytes);
                        }
        return shapeCount;
    }

    public static long ChiralShapes(IEnumerable<FileScanner.Results> filelist, Stopwatch sw) {
        long shapeCount = 0;
        foreach (var r in filelist) {
            shapeCount += ChiralShapes(r);
            var ss = "[" + shapeCount + "," + sw.Elapsed.TotalSeconds.ToString("0") + "s]";
            Console.Write(ss + new string('\b', ss.Length));
        }
        return shapeCount;
    }

    // for each shape in parallel, get its minimum chiral rotation and compare it to
    // what we started with to see if it should be counted.
    public static long ChiralShapes(FileScanner.Results file) {
        long count = 0;
        Parallel.ForEach(LoadShapes(file.n, file.w, file.h, file.d), (shapeBytes) => {
            var shape = new BitShape(file.w, file.h, file.d, shapeBytes);
            var newShape = new BitShape(shape).MinChiralRotation();
            if (shape.Equals(newShape)) Interlocked.Increment(ref count);
        });
        return count;
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
