using System.Diagnostics;

namespace ShapeMaker;

public class Program {
    public const string FILE_PATH = "/Users/hafthor/dev/ShapeMaker/";
    public const string FILE_EXT = ".bin";
    public const string FILE_COMPLETE = "_COMPLETE";

    /*
        Results and timing (in seconds) from 14" 2023 MacBook Pro w/ 96GB 12-core M2 Max, .NET 7 in Release mode
        n=2, shapes: 1, chiral shapes: 1 time: 0.071802
        n=3, shapes: 2, chiral shapes: 2 time: 0.002224
        n=4, shapes: 8, chiral shapes: 7 time: 0.0802893
        n=5, shapes: 29, chiral shapes: 23 time: 0.0306935
        n=6, shapes: 166, chiral shapes: 112 time: 0.0861341
        n=7, shapes: 1,023, chiral shapes: 607 time: 0.2358299
        n=8, shapes: 6,922, chiral shapes: 3,811 time: 0.3127255
        n=9, shapes: 48,311, chiral shapes: 25,413 time: 0.7920315
        n=10, shapes: 346,543, chiral shapes: 178,083 time: 4.2821279
        n=11, shapes: 2,522,522, chiral shapes: 1,279,537 time: 21.4681853
        n=12, shapes: 18,598,427, chiral shapes: 9,371,094 time: 172.5449822
        n=13, shapes: 138,462,649, chiral shapes: 69,513,546 time: 1740.2329292
        n=14, shapes: 1,039,496,297, chiral shapes: 520,878,101 time: 16211.6567239
        n=15, shapes: 7,859,514,470, chiral shapes: 3,934,285,874 time: 144239.3006667
        Peak memory usage: ~40GB
     */

    // Potential Optimizations / Enhancements:
    // * A % complete rather than just current count showing progress.
    // * We could do a counting pass to see how to best partition the data to avoid
    //   making a bunch of passes that create few or no new polycubes.
    // * It would be nice if we had a way to capture the time taken for each dimensions
    //   file, so we can stop and resume with the correct timing results.
    // * Removing chiral shape calculation for now. Need to add it back in later.
    
    // Potential Features:
    // * Make a 4-D version?

    static void Main(string[] args) {
        var totalAvailableMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;

        bool recompute = false;

        string s = NCompleteString(1);
        if (recompute || s == null) {
            if (recompute) FileWriter.Clear(1); else FileWriter.ClearTmp(1);
            using (var fw = new FileWriter(1, 1, 1, 1))
                fw.Write(new BitShape("1,1,1,*").bytes);
            MarkNComplete(1, "n=1, shapes: 1 time: 0"); // ", chiral shapes: 1 time 0");
        }

        for (byte n = 2; n < 20; n++) {
            s = NCompleteString(n);
            if (!recompute && s != null) {
                Console.WriteLine(s);
                continue;
            }

            if (recompute) FileWriter.Clear(n); else FileWriter.ClearTmp(n);
            s = "n=" + n + ", shapes: ";
            Console.Write(s);
            Stopwatch sw = Stopwatch.StartNew();
            var list = new FileScanner((byte)(n - 1)).List;
            var targetSizes = ShapeSizesFromExtendingShapes(list).ToList();
            long shapeCount = 0;
            foreach (var sz in targetSizes) {
                // if the combined input size is 1GB, for example, the output is likely to be ~8GB, and ~24GB in memory
                int tcmax = (n >= 14 && sz.sz * 8 * 3 > totalAvailableMemory && sz.w > 1 && sz.h > 1 && sz.d > 1) ? n : -1;
                if (FileReader.FileExists(n, sz.w, sz.h, sz.d)) {
                    int bytesPerShape = (sz.w * sz.h * sz.d + 7) / 8;
                    shapeCount += FileReader.FileSize(n, sz.w, sz.h, sz.d) / bytesPerShape;
                } else {
                    var ssss = "        " + (tcmax >= 0 ? "/" + tcmax.ToString() : "") + "[" + shapeCount.ToString("N0") + ", " + sw.Elapsed.TotalSeconds.ToString("N0") + "s, " + sz.w + "x" + sz.h + "x" + sz.d + "]     ";
                    Console.Write(ssss + new string('\b', ssss.Length));
                    using (var fw = new FileWriter(n, sz.w, sz.h, sz.d)) {
                        shapeCount += ShapesFromExtendingShapes(list, fw, sz.w, sz.h, sz.d, tcmax);
                    }
                }
                var sss = "        " + (tcmax >= 0 ? "/" + tcmax.ToString() : "") + "[" + shapeCount.ToString("N0") + ", " + sw.Elapsed.TotalSeconds.ToString("N0") + "s, " + sz.w + "x" + sz.h + "x" + sz.d + "]     ";
                Console.Write(sss + new string('\b', sss.Length));
            }
            sw.Stop();
            string ss = shapeCount.ToString("N0") + " time: " + sw.Elapsed.TotalSeconds + "      \b\b\b\b\b\b";
            s += ss;
            Console.Write(ss);
            /*
            sw = Stopwatch.StartNew();
            long chiralCount = 0;
            int fi = 0, fl = targetSizes.Count;
            foreach (var f in targetSizes) {
                var (w, h, d) = (f.w, f.h, f.d);
                string sss = " " + ++fi + "/" + fl + "=" + w + "x" + h + "x" + d + "  ";
                Console.Write(sss + new string('\b', sss.Length));
                Parallel.ForEach(LoadShapes(n, w, h, d), (shapeBytes) => {
                    var shape = new BitShape(w, h, d, shapeBytes);
                    if (shape.IsMinChiralRotation()) Interlocked.Increment(ref chiralCount);
                });
            }
            sw.Stop();
            ss = ", chiral count: " + chiralCount.ToString("N0") + " time: " + sw.Elapsed.TotalSeconds;
            s += ss;
            Console.Write(ss);
            */
            Console.WriteLine();
            MarkNComplete(n, s);
        }
    }

    private static string NCompleteString(int n) {
        return File.Exists(Program.FILE_PATH + n + "/" + Program.FILE_COMPLETE) ? File.ReadAllText(Program.FILE_PATH + n + "/" + Program.FILE_COMPLETE) : null;
    }

    private static void MarkNComplete(int n, string s) {
        File.WriteAllText(Program.FILE_PATH + n + "/" + Program.FILE_COMPLETE, s);
    }

    private static IEnumerable<byte[]> LoadShapes(int n, int w, int h, int d) {
        using (var fr = new FileReader(n, w, h, d))
            for (; ; ) {
                var l = fr.Read();
                if (l == null) break;
                yield return l;
            }
    }

    public static IEnumerable<(byte w, byte h, byte d, long sz)> ShapeSizesFromExtendingShapes(IEnumerable<FileScanner.Results> filelist) {
        var totalFileSizeForDimensions = new Dictionary<(byte w, byte h, byte d), long>();
        foreach (var r in filelist)
            foreach (var sz in ShapeSizesFromExtendingShapes(r))
                if (!totalFileSizeForDimensions.TryAdd((sz.w, sz.h, sz.d), sz.sz))
                    totalFileSizeForDimensions[(sz.w, sz.h, sz.d)] += sz.sz;
        return totalFileSizeForDimensions.ToList().OrderBy(i => i.Key.w * 65536 + i.Key.h * 256 + i.Key.d).Select(i => (i.Key.w, i.Key.h, i.Key.d, i.Value));
    }

    public static IEnumerable<(byte w, byte h, byte d, long sz)> ShapeSizesFromExtendingShapes(FileScanner.Results file) {
        byte n = file.n, w = file.w, h = file.h, d = file.d;
        if (n < w * h * d) yield return (w, h, d, file.size);
        var (w1, h1, d1) = MinRotation((byte)(w + 1), h, d);
        yield return (w1, h1, d1, file.size);
        var (w2, h2, d2) = MinRotation(w, (byte)(h + 1), d);
        yield return (w2, h2, d2, file.size);
        var (w3, h3, d3) = MinRotation(w, h, (byte)(d + 1));
        yield return (w3, h3, d3, file.size);
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

    public static long ShapesFromExtendingShapes(IEnumerable<FileScanner.Results> filelist, FileWriter fw, byte w, byte h, byte d, int tcmax) {
        var newShapes = new HashSet<byte[]>(ByteArrayEqualityComparer.Instance);

        if (tcmax < 0) {
            foreach (var r in filelist)
                ShapesFromExtendingShapes(r, fw, newShapes, w, h, d, -1, -1, -1);
            return newShapes.LongCount();
        }

        long shapeCount = 0;
        int maxInteriorCount = Math.Max(0, w - 2) * Math.Max(0, h - 2) * Math.Max(0, d - 2);
        for (int cc = 0; cc <= 8; cc++)
            for (int ec = cc == 0 ? 0 : 1; ec <= tcmax - cc; ec++)
                for (int fc = ec == 0 ? 0 : 1; fc <= tcmax - cc - ec; fc++)
                    if (cc + ec + fc >= tcmax - maxInteriorCount) {
                        foreach (var r in filelist)
                            ShapesFromExtendingShapes(r, fw, newShapes, w, h, d, cc, ec, fc);
                        shapeCount += newShapes.LongCount();
                        newShapes.Clear();
                    }

        return shapeCount;
    }

    // for each shape in parallel, try to add cube to it
    // first does by adding cube to the shape in its current size
    // then tries padding each of the 6 faces of the shape and adding a cube there
    public static void ShapesFromExtendingShapes(FileScanner.Results file, FileWriter fw, HashSet<byte[]> newShapes, byte tw, byte th, byte td, int tcc, int tec, int tfc) {
        byte n = file.n, w = file.w, h = file.h, d = file.d;

        if (w == tw && h == th && d == td) {
            if (tcc >= 0) Console.Write("*" + tcc + "," + Pad2(tec) + "," + Pad2(tfc) + "\b\b\b\b\b\b\b\b"); else Console.Write("*\b");
            Parallel.ForEach(LoadShapes(n, w, h, d), (shapeBytes) => {
                AddShapes(newShapes, fw, new BitShape(w, h, d, shapeBytes), 0, w, 0, h, 0, d, tcc, tec, tfc); // unpadded
            });
        }

        var (ww, hh, dd) = MinRotation((byte)(w + 1), h, d);
        if (ww == tw && hh == th && dd == td) {
            if (tcc >= 0) Console.Write("|" + tcc + "," + Pad2(tec) + "," + Pad2(tfc) + "\b\b\b\b\b\b\b\b"); else Console.Write("|\b");
            Parallel.ForEach(LoadShapes(n, w, h, d), (shapeBytes) => {
                BitShape shape = new BitShape(w, h, d, shapeBytes);
                AddShapes(newShapes, fw, shape.PadLeft(), 0, 1, 0, h, 0, d, tcc, tec, tfc);
                AddShapes(newShapes, fw, shape.PadRight(), w, w + 1, 0, h, 0, d, tcc, tec, tfc);
            });
        }

        (ww, hh, dd) = MinRotation(w, (byte)(h + 1), d);
        if (ww == tw && hh == th && dd == td) {
            if (tcc >= 0) Console.Write("-" + tcc + "," + Pad2(tec) + "," + Pad2(tfc) + "\b\b\b\b\b\b\b\b"); else Console.Write("-\b");
            Parallel.ForEach(LoadShapes(n, w, h, d), (shapeBytes) => {
                BitShape shape = new BitShape(w, h, d, shapeBytes);
                AddShapes(newShapes, fw, shape.PadTop(), 0, w, 0, 1, 0, d, tcc, tec, tfc);
                AddShapes(newShapes, fw, shape.PadBottom(), 0, w, h, h + 1, 0, d, tcc, tec, tfc);
            });
        }

        (ww, hh, dd) = MinRotation(w, h, (byte)(d + 1));
        if (ww == tw && hh == th && dd == td) {
            if (tcc >= 0) Console.Write("/" + tcc + "," + Pad2(tec) + "," + Pad2(tfc) + "\b\b\b\b\b\b\b\b"); else Console.Write("/\b");
            Parallel.ForEach(LoadShapes(n, w, h, d), (shapeBytes) => {
                BitShape shape = new BitShape(w, h, d, shapeBytes);
                AddShapes(newShapes, fw, shape.PadFront(), 0, w, 0, h, 0, 1, tcc, tec, tfc);
                AddShapes(newShapes, fw, shape.PadBack(), 0, w, 0, h, d, d + 1, tcc, tec, tfc);
            });
        }
    }

    private static string Pad2(int v) => v >= 0 && v <= 9 ? "0" + v.ToString() : v.ToString();

    // for each blank cube from x0 to w, y0 to h, z0 to d, if it has an adjacent neighbor
    // add that cube, find the minimum rotation, and add to the newShapes hash set (under
    // lock since we could be doing this in parallel.)
    private static void AddShapes(HashSet<byte[]> newShapes, FileWriter fw, BitShape shape, int x0, int w, int y0, int h, int z0, int d, int tcc, int tec, int tfc) {
        int cc = 0, ec = 0, fc = 0;
        if (tcc >= 0) {
            var counts = shape.CornerEdgeFaceCount();
            if (counts.corners > tcc || (counts.corners + 1) < tcc) return;
            if (counts.edges > tec || (counts.edges + 1) < tec) return;
            if (counts.faces > tfc || (counts.faces + 1) < tfc) return;
            (cc, ec, fc) = counts;
        }
        int xl = shape.w - 1, yl = shape.h - 1, zl = shape.d - 1;
        for (var x = x0; x < w; x++) {
            bool xyes = x == 0 || x == xl;
            for (var y = y0; y < h; y++) {
                bool yyes = y == 0 || y == yl;
                for (var z = z0; z < d; z++) {
                    if (tcc >= 0) {
                        bool zyes = z == 0 || z == zl;
                        bool isInterior = !xyes && !yyes && !zyes;
                        if (isInterior && (tcc != cc || tec != ec || tfc != fc)) continue;
                        bool isCorner = xyes && yyes && zyes;
                        if (isCorner && tcc != cc + 1) continue;
                        bool isEdge = xyes && yyes || yyes && zyes || xyes && zyes;
                        if (isEdge && tec != ec + 1) continue;
                        bool isFace = !isCorner && !isEdge && !isInterior;
                        if (isFace && tfc != fc + 1) continue;
                    }
                    if (!shape[x, y, z])
                        if (shape.HasSetNeighbor(x, y, z)) {
                            var newShape = new BitShape(shape);
                            newShape[x, y, z] = true;
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
        internal string ext;
        public string filepath => Program.FILE_PATH + n + "/" + w + "," + h + "," + d + ext;
        public long size;
    }

    public readonly List<Results> List = new();

    public FileScanner(byte n, string ext = Program.FILE_EXT) {
        var di = new DirectoryInfo(Program.FILE_PATH + n);
        var files = di.GetFiles("*" + ext).OrderBy(f => f.Length);
        foreach (var file in files) {
            if (file.Name.EndsWith(ext)) {
                var dim = file.Name.Substring(0, file.Name.Length - ext.Length).Split(',');
                if (dim.Length != 3) continue;
                if (!byte.TryParse(dim[0], out var w) || w < 1 || w > n) continue;
                if (!byte.TryParse(dim[1], out var h) || h < 1 || h > n) continue;
                if (!byte.TryParse(dim[2], out var d) || d < 1 || d > n) continue;
                List.Add(new Results() { n = n, w = w, h = h, d = d, ext = ext, size = file.Length });
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
            var list2 = new FileScanner(n, ".tmp").List;
            foreach (var f in list2)
                File.Delete(f.filepath);
            if (File.Exists(Program.FILE_PATH + n + "/" + Program.FILE_COMPLETE))
                File.Delete(Program.FILE_PATH + n + "/" + Program.FILE_COMPLETE);
        }
    }

    public static void ClearTmp(byte n) {
        var di = new DirectoryInfo(Program.FILE_PATH + n);
        if (!di.Exists)
            di.Create();
        else
            foreach (var f in new FileScanner(n, ".tmp").List)
                File.Delete(f.filepath);
    }

    public FileWriter(int n, int w, int h, int d) {
        path = Program.FILE_PATH + n + "/" + w + "," + h + "," + d;
        length = (w * h * d + 7) / 8;
    }

    public void Write(byte[] shape) {
        if (shape.Length != length) throw new ArgumentOutOfRangeException(nameof(shape), shape.Length, "unexpected shape length - should be " + length);
        if (fs == null)
            fs = File.Open(path + ".tmp", FileMode.Append);
        fs.Write(shape);
    }

    public void Dispose() {
        fs?.Dispose();
        if (File.Exists(path + ".tmp"))
            File.Move(path + ".tmp", path + Program.FILE_EXT);
    }
}

public class FileReader : IDisposable {
    private readonly FileStream fs;
    private readonly int length;

    public static bool FileExists(int n, int w, int h, int d) => File.Exists(Program.FILE_PATH + n + "/" + w + "," + h + "," + d + Program.FILE_EXT);

    public static long FileSize(int n, int w, int h, int d) {
        var fi = new FileInfo(Program.FILE_PATH + n + "/" + w + "," + h + "," + d + Program.FILE_EXT);
        if (!fi.Exists) return -1;
        return fi.Length;
    }

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
