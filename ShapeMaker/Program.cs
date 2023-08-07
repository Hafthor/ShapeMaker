using System.Diagnostics;

namespace ShapeMaker;

public class Program {
    public const string FILE_PATH = "/Users/hafthor/dev/ShapeMaker/";
    public const string FILE_EXT = ".bin";
    public const string FILE_COMPLETE = "_COMPLETE";

    /*
        Timing (in seconds) from 14-inch 2023 MacBook Pro w/ 96GB 12-core M2 Max, .NET 7 in Release mode
        n=2, shapes: 1 time: 0.0661961, chiral shapes: n=2, shapes: 1 time: 0.0016484
        n=3, shapes: 2 time: 0.000109, chiral shapes: n=3, shapes: 2 time: 0.000765
        n=4, shapes: 8 time: 0.0001455, chiral shapes: n=4, shapes: 7 time: 0.0257676
        n=5, shapes: 29 time: 0.0260315, chiral shapes: n=5, shapes: 23 time: 0.0004452
        n=6, shapes: 166 time: 0.0263653, chiral shapes: n=6, shapes: 112 time: 0.0277249
        n=7, shapes: 1,023 time: 0.0581247, chiral shapes: n=7, shapes: 607 time: 0.0400251
        n=8, shapes: 6,922 time: 0.0565975, chiral shapes: n=8, shapes: 3,811 time: 0.1070216
        n=9, shapes: 48,311 time: 0.3342176, chiral shapes: n=9, shapes: 25,413 time: 0.4630857
        n=10, shapes: 346,543 time: 1.3597535, chiral shapes: n=10, shapes: 178,083 time: 1.9888612
        n=11, shapes: 2,522,522 time: 30.0762011, chiral shapes: n=11, shapes: 1,279,537 time: 16.0214537
        n=12, shapes: 18,598,427 time: 103.3524449, chiral shapes: n=12, shapes: 9,371,094 time: 132.8544995
        n=13, shapes: 138,462,649 time: 1146.536404, chiral shapes: n=13, shapes: 69,513,546 time: 1455.701472
        n=14, shapes: 1,039,496,297 time: 17422.1545339, chiral shapes: n=14, shapes: 520,878,101 time: 13816.830999
        Peak memory usage: 72.04GB
     */

    // Potential Optimizations:
    // * Minimize rotations - should only need to test all rotations when w==h==d.
    // * Avoid storing/hashing shape dimensions/length since we are always dealing with one known size at a time.

    // Potential Features:
    // * Make a 4-D version?

    // Minimize rotations:
    // example: w=2,h=3,d=4, only 8 rotations (this,X2,Y2,X2,Z2,X2,Y2,X2)
    // example: w=2,h=2,d=2, all 24 rotations
    // example: w=2,h=3,d=3, only 16 rotations (this,X,X,X, this.Y2,X,X,X, this.Z2,X,X,X, this.Y2Z2,X,X,X)
    // example: w=2,h=2,d=3, only 16 rotations (this,Z,Z,Z, this.Y2,Z,Z,Z, this.X2,Z,Z,Z, this.X2Y2,Z,Z,Z)
    // example: w=2,h=3,d=2, impossible

    static void Main(string[] args) {
        bool recompute = true;
        bool doChiral = true;

        byte[] shape1 = BitShape.Deserialize("1,1,1,*");
        FileWriter.Clear(1);
        using (var fw = new FileWriter(1, 1, 1, 1))
            fw.Write(shape1);

        for (var n = 2; n < 20; n++) {
            string s = CompleteString(n);
            if (recompute || s == null) {
                FileWriter.Clear(n);
                s = "n=" + n + ", shapes: ";
                Console.Write(s);
                Stopwatch sw = Stopwatch.StartNew();
                var list = new FileScanner(n - 1).List;
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
                MarkComplete(n, s);
            } else {
                Console.WriteLine(s);
            }
        }
    }

    private static string CompleteString(int n) {
        return File.Exists(Program.FILE_PATH + n + "/" + Program.FILE_COMPLETE) ? File.ReadAllText(Program.FILE_PATH + n + "/" + Program.FILE_COMPLETE) : null;
    }

    private static void MarkComplete(int n, string s) {
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
        foreach (var r in filelist) {
            shapeCount += ShapesFromExtendingShapes(r);
            var ss = " [" + shapeCount + "," + sw.Elapsed.TotalSeconds.ToString("0") + "s]";
            Console.Write(ss + new string('\b', ss.Length));
        }
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

    public static long ShapesFromExtendingShapes(FileScanner.Results file) {
        int n = file.n, w = file.w, h = file.h, d = file.d;
        long shapeCount = 0;

        Console.Write("|");
        var newShapes = LoadShapesHashSet(n + 1, w, h, d);
        Console.Write("\b");
        using (var fw = new FileWriter(n + 1, w, h, d)) {
            Parallel.ForEach(LoadShapes(n, w, h, d), (shape) => {
                Interlocked.Add(ref shapeCount, AddShapes(newShapes, fw, shape, 0, w, 0, h, 0, d)); // unpadded
            });
        }

        Console.Write("/");
        newShapes.Clear();
        var (ww, hh, dd) = MinRotation(w + 1, h, d);
        newShapes = LoadShapesHashSet(n + 1, ww, hh, dd);
        Console.Write("\b");
        using (var fw = new FileWriter(n + 1, ww, hh, dd)) {
            Parallel.ForEach(LoadShapes(n, w, h, d), (shape) => {
                Interlocked.Add(ref shapeCount, AddShapes(newShapes, fw, shape.PadLeft(), 0, 1, 0, h, 0, d));
                Interlocked.Add(ref shapeCount, AddShapes(newShapes, fw, shape.PadRight(), w, w + 1, 0, h, 0, d));
            });
        }

        Console.Write("-");
        newShapes.Clear();
        (ww, hh, dd) = MinRotation(w, h + 1, d);
        newShapes = LoadShapesHashSet(n + 1, ww, hh, dd);
        Console.Write("\b");
        using (var fw = new FileWriter(n + 1, ww, hh, dd)) {
            Parallel.ForEach(LoadShapes(n, w, h, d), (shape) => {
                Interlocked.Add(ref shapeCount, AddShapes(newShapes, fw, shape.PadTop(), 0, w, 0, 1, 0, d));
                Interlocked.Add(ref shapeCount, AddShapes(newShapes, fw, shape.PadBottom(), 0, w, h, h + 1, 0, d));
            });
        }

        Console.Write("\\");
        newShapes.Clear();
        (ww, hh, dd) = MinRotation(w, h, d + 1);
        newShapes = LoadShapesHashSet(n + 1, ww, hh, dd);
        Console.Write("\b");
        using (var fw = new FileWriter(n + 1, ww, hh, dd)) {
            Parallel.ForEach(LoadShapes(n, w, h, d), (shape) => {
                Interlocked.Add(ref shapeCount, AddShapes(newShapes, fw, shape.PadFront(), 0, w, 0, h, 0, 1));
                Interlocked.Add(ref shapeCount, AddShapes(newShapes, fw, shape.PadBack(), 0, w, 0, h, d, d + 1));
            });
        }

        return shapeCount;
    }

    // for each blank cube from x0 to w, y0 to h, z0 to d, if it has an adjacent neighbor
    // add that cube, find the minimum rotation, and add to the newShapes hash set (under
    // lock since we could be doing this in parallel.)
    private static long AddShapes(HashSet<byte[]> newShapes, FileWriter fw, byte[] shape, int x0, int w, int y0, int h, int z0, int d) {
        long shapeCount = 0;
        for (var x = x0; x < w; x++)
            for (var y = y0; y < h; y++)
                for (var z = z0; z < d; z++)
                    if (!shape.Get(x, y, z))
                        if (HasSetNeighbor(shape, x, y, z)) {
                            var newShape = shape.Copy();
                            newShape.Set(x, y, z, true);
                            var s = newShape.MinRotation();
                            bool writeShape = false;
                            lock (newShapes) {
                                writeShape = newShapes.Add(s);
                                if (writeShape) shapeCount++;
                            }
                            if (writeShape)
                                lock (fw)
                                    fw.Write(s);
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

    // for each shape in parallel, get its minimum chiral rotation and add to
    // newShapes hash set under lock
    public static long ChiralShapes(FileScanner.Results file) {
        var newShapes = new HashSet<byte[]>(ByteArrayEqualityComparer.Instance);
        Parallel.ForEach(LoadShapes(file.n, file.w, file.h, file.d), (shape) => {
            var newShape = shape.Copy().MinChiralRotation();
            lock (newShapes)
                newShapes.Add(newShape);
        });
        return newShapes.Count;
    }

    private static bool HasSetNeighbor(byte[] shape, int x, int y, int z) {
        var (w, h, d) = shape.Dimensions();
        // minor opt: we do easier comparisons first with short-circuiting
        return (x > 0 && shape.Get(x - 1, y, z)) ||
            (y > 0 && shape.Get(x, y - 1, z)) ||
            (z > 0 && shape.Get(x, y, z - 1)) ||
            (x + 1 < w && shape.Get(x + 1, y, z)) ||
            (y + 1 < h && shape.Get(x, y + 1, z)) ||
            (z + 1 < d && shape.Get(x, y, z + 1));
    }
}

public class ByteArrayEqualityComparer : IEqualityComparer<byte[]> {
    public static readonly ByteArrayEqualityComparer Instance = new ByteArrayEqualityComparer();

    bool IEqualityComparer<byte[]>.Equals(byte[]? x, byte[]? y) {
        return x is not null && y is not null && x.CompareTo(y) == 0;
    }

    int IEqualityComparer<byte[]>.GetHashCode(byte[] obj) {
        int hashCode = obj.Length;
        for (int i = 0; i < obj.Length; i++) {
            hashCode = hashCode * 37 + obj[i];
        }
        return hashCode;
    }
}

public class FileScanner {
    public class Results {
        public int w, h, d, n;
        public string filepath => Program.FILE_PATH + n + "/" + w + "," + h + "," + d + Program.FILE_EXT;
    }

    public readonly List<Results> List = new();

    public FileScanner(int n) {
        var di = new DirectoryInfo(Program.FILE_PATH + n);
        var files = di.GetFiles("*" + Program.FILE_EXT).OrderBy(f => f.Length);
        foreach (var file in files) {
            if (file.Name.EndsWith(Program.FILE_EXT)) {
                var dim = file.Name.Substring(0, file.Name.Length - Program.FILE_EXT.Length).Split(',');
                if (dim.Length != 3) continue;
                if (!int.TryParse(dim[0], out int w) || w < 1 || w > n) continue;
                if (!int.TryParse(dim[1], out int h) || h < 1 || h > n) continue;
                if (!int.TryParse(dim[2], out int d) || d < 1 || d > n) continue;
                List.Add(new Results() { n = n, w = w, h = h, d = d });
            }
        }
    }
}

public class FileWriter : IDisposable {
    private FileStream fs = null;
    private readonly int length;
    private readonly string path;

    public static void Clear(int n) {
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
        length = (w * h * d + 11 + 7) / 8;
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
        length = (w * h * d + 11 + 7) / 8;
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