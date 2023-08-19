using System.Collections.Concurrent;
using System.Diagnostics;

namespace ShapeMaker;

using MyHashSet = HashSet<byte[]>;
//using MyHashSet = BitShapeHashSet;
//using MyHashSet = ConcurrentDictionary<byte[], byte>;

public class Program {
    public static readonly string FILE_PATH = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "ShapeMaker");
    public const string FILE_EXT = ".bin";
    public const string FILE_COMPLETE = "_COMPLETE";
    public const int MAX_COMPUTE_N = 19;
    public const bool DO_CHIRAL_COUNT = true;
    public const bool DO_FORCE_RECOMPUTE = false;

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

        n=2, shapes: 1 time: 0.0166472          
        n=3, shapes: 2 time: 0.001548           
        n=4, shapes: 8 time: 0.002158           
        n=5, shapes: 29 time: 0.0018264          
        n=6, shapes: 166 time: 0.0040883          
        n=7, shapes: 1,023 time: 0.0072473          
        n=8, shapes: 6,922 time: 0.0326462          
        n=9, shapes: 48,311 time: 0.2014176          
        n=10, shapes: 346,543 time: 1.7254155          
        n=11, shapes: 2,522,522 time: 13.6087381          
        n=12, shapes: 18,598,427 time: 110.4653956          
        n=13, shapes: 138,462,649 time: 1032.2354959           
        n=14, shapes: 1,039,496,297 time: 12157.5793645           
        n=15, shapes: 7,859,514,470 time: 123731.0088215
        Peak memory usage: ~80GB
     */

    // Potential Optimizations / Enhancements:
    // * We could do a counting pass to see how to best partition the data to avoid
    //   making a bunch of passes that create few or no new polycubes.
    // * It would be nice if we had a way to capture the time taken for each dimensions
    //   file, so we can stop and resume with the correct timing results.

    // Potential Features:
    // * Make a 4-D version?

    // Limits:
    // Currently limited by RAM because of the need to hashset the shapes to find the unique ones. We
    // extend this a bit by sharding by the shape dimension first, that is, we find all the shapes of
    // a particular size together at the same time, even when it means we have to reread source shape
    // a few times. When it becomes necessary, we also shard it by corner/edge/face counts. This is a
    // rotationally independent counting process so it is done before finding the minimal rotation.
    // By sharding by corner count alone, it is estimated that this would extend the maximum effective
    // memory by a factor of 4. By sharding it also by edges and faces, as we do, it should provide a
    // further factor of 5 improvement, for a total of 20. This should allow us to easily compute n=16
    // and possibly n=17 on a 96GB machine.

    /// <summary>
    /// How it works:
    /// It starts with a n=1 polycube shape and it tries to add an adjacent neighbor cube to the shape
    /// and check to see if we've encountered this shape before. When comparing shapes, we always find
    /// the minimal rotation first, which means a rotation where w<=h and h<=d and then where the bits
    /// compare as less. At each step, we are taking the results of the prior n and extending all the
    /// unique shapes found to try to find new shapes. We can split the work in to what the dimensions
    /// of the target shape will be, for example 2x3x5. This does mean that we may reread prior shapes
    /// to generate all possible targets. When extending a shape, we attempt to extend it within the
    /// bounds of the prior shape, but we also test extending the shape by growing its boundaries. To
    /// scale this, we will, when it looks like the hashset for a specific dimension will exceed the
    /// host's memory, shard by shape features that are rotationally indepenedent, such as the number
    /// of corners, edges, or faces set.
    /// Note that this program writes out the shapes it finds as it goes. It is safe to terminate the
    /// program and run again to resume, although it will not have the correct time elapsed shown in
    /// that case.
    /// </summary>
    static void Main(string[] args) {
        var totalAvailableMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;

        bool recompute = DO_FORCE_RECOMPUTE;

        string s = NCompleteString(1);
        if (recompute || s == null) {
            if (recompute) FileWriter.Clear(1); else FileWriter.ClearTmp(1);
            using (var fw = new FileWriter(1, 1, 1, 1))
                fw.Write(new BitShape("1,1,1,*").bytes);
            MarkNComplete(1, "n=1, shapes: 1 time: 0"); // ", chiral shapes: 1 time 0");
        }

        for (byte n = 2; n <= MAX_COMPUTE_N; n++) {
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
            int fi = 0, fl = targetSizes.Count;
            foreach (var sz in targetSizes) {
                int tcmax = -1;
                // if the combined input size is 1GB, for example, the output is likely to be ~8GB, and ~24GB in memory
                if (n >= 14 && sz.w > 1 && sz.h > 1 && sz.d > 1) {
                    long inMemSize = sz.sz * 8 * 3;
                    if (inMemSize > totalAvailableMemory) tcmax = n;
                }
                fi++;
                if (FileReader.FileExists(n, sz.w, sz.h, sz.d)) {
                    int bytesPerShape = new BitShape(sz.w, sz.h, sz.d).bytes.Length;
                    shapeCount += FileReader.FileSize(n, sz.w, sz.h, sz.d) / bytesPerShape;
                } else {
                    var ssss = "            " + (tcmax >= 0 ? "/" + tcmax : "") + "[" + shapeCount.ToString("N0") + ", " + sw.Elapsed.TotalSeconds.ToString("N0") + "s, " + sz.w + "x" + sz.h + "x" + sz.d + " " + fi + "/" + fl + "]     ";
                    Console.Write(ssss + new string('\b', ssss.Length));
                    if (n < MAX_COMPUTE_N)
                        using (var fw = new FileWriter(n, sz.w, sz.h, sz.d))
                            shapeCount += ShapesFromExtendingShapes(list, fw, sz.w, sz.h, sz.d, tcmax);
                    else
                        shapeCount += ShapesFromExtendingShapes(list, null, sz.w, sz.h, sz.d, tcmax);
                }
                var sss = "            " + (tcmax >= 0 ? "/" + tcmax : "") + "[" + shapeCount.ToString("N0") + ", " + sw.Elapsed.TotalSeconds.ToString("N0") + "s, " + sz.w + "x" + sz.h + "x" + sz.d + " " + fi + "/" + fl + "]     ";
                Console.Write(sss + new string('\b', sss.Length));
            }
            sw.Stop();
            string ss = shapeCount.ToString("N0") + " time: " + sw.Elapsed.TotalSeconds + "      \b\b\b\b\b\b";
            s += ss;
            Console.Write(ss);

            if (DO_CHIRAL_COUNT) {
                sw = Stopwatch.StartNew();
                long chiralCount = 0;
                fi = 0;
                foreach (var f in targetSizes) {
                    string sss = "     " + ++fi + "/" + fl + "=" + f.w + "x" + f.h + "x" + f.d + ", " + chiralCount.ToString("N0") + ", " + sw.Elapsed.TotalSeconds.ToString("N0") + "s   ";
                    Console.Write(sss + new string('\b', sss.Length));
                    FileScanner.Results r = new FileScanner.Results() { n = n, w = f.w, h = f.h, d = f.d, ext = Program.FILE_EXT };
                    int shapeSizeInBytes = new BitShape(f.w, f.h, f.d).bytes.Length;
                    long sourceShapes = FileReader.FileSize(n, f.w, f.h, f.d) / shapeSizeInBytes;
                    long sourceShapeCount = 0;
                    Parallel.ForEach(LoadShapes(r), (shape) => {
                        long newShapeCount = Interlocked.Increment(ref sourceShapeCount);
                        if ((newShapeCount & 0xFFFF) == 0) {
                            long oldShapeCount = newShapeCount - 0x10000;
                            int perc = (int)(oldShapeCount * 100 / sourceShapes);
                            int perc2 = (int)(newShapeCount * 100 / sourceShapes);
                            if (perc < perc2) {
                                var s = " " + perc2 + "% ";
                                lock (r) Console.Write(s + new string('\b', s.Length));
                            }
                        }
                        if (shape.IsMinChiralRotation()) Interlocked.Increment(ref chiralCount);
                    });
                }
                sw.Stop();
                ss = ", chiral count: " + chiralCount.ToString("N0") + " time: " + sw.Elapsed.TotalSeconds + "    ";
                s += ss;
                Console.Write(ss);
            }

            Console.WriteLine();
            MarkNComplete(n, s);
        }
    }

    private static string NCompleteString(int n) {
        return File.Exists(Path.Combine(Program.FILE_PATH, n.ToString(), Program.FILE_COMPLETE)) ? File.ReadAllText(Path.Combine(Program.FILE_PATH, n.ToString(), Program.FILE_COMPLETE)) : null;
    }

    private static void MarkNComplete(int n, string s) {
        File.WriteAllText(Path.Combine(Program.FILE_PATH, n.ToString(), Program.FILE_COMPLETE), s);
    }

    private static IEnumerable<BitShape> LoadShapes(FileScanner.Results file) {
        using (var fr = new FileReader(file.n, file.w, file.h, file.d))
            for (; ; ) {
                var bytes = fr.Read();
                if (bytes == null) break;
                yield return new BitShape((byte)file.w, (byte)file.h, (byte)file.d, bytes);
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
        //var newShapes = new BitShapeHashSet((w * h * d + 7) / 8);
        //var newShapes = new ConcurrentDictionary<byte[], byte>(ByteArrayEqualityComparer.Instance);

        if (tcmax < 0) {
            foreach (var r in filelist)
                ShapesFromExtendingShapes(r, newShapes, w, h, d, -1, -1, -1);
            if (fw != null) foreach (var shape in newShapes) fw.Write(shape);
            return newShapes.LongCount();
        }

        long shapeCount = 0;
        int maxInteriorCount = Math.Max(0, w - 2) * Math.Max(0, h - 2) * Math.Max(0, d - 2);
        for (int cc = 0; cc <= 8; cc++)
            for (int ec = cc == 0 ? 0 : 1; ec <= tcmax - cc; ec++)
                for (int fc = ec == 0 ? 0 : 1; fc <= tcmax - cc - ec; fc++)
                    if (cc + ec + fc >= tcmax - maxInteriorCount) {
                        foreach (var r in filelist)
                            ShapesFromExtendingShapes(r, newShapes, w, h, d, cc, ec, fc);
                        if (fw != null) foreach (var shape in newShapes) fw.Write(shape);
                        shapeCount += newShapes.LongCount();
                        newShapes.Clear();
                    }

        return shapeCount;
    }

    // for each shape in parallel, try to add cube to it
    // first does by adding cube to the shape in its current size
    // then tries padding each of the 6 faces of the shape and adding a cube there
    public static void ShapesFromExtendingShapes(FileScanner.Results file, MyHashSet newShapes, byte tw, byte th, byte td, int tcc, int tec, int tfc) {
        byte w = file.w, h = file.h, d = file.d;
        int shapeSizeInBytes = new BitShape(w, h, d).bytes.Length;
        long sourceShapes = FileReader.FileSize(file.n, w, h, d) / shapeSizeInBytes;

        if (w == tw && h == th && d == td) {
            StatusUpdate('*', tcc, tec, tfc);
            long sourceShapeCount = 0;
            Parallel.ForEach(LoadShapes(file), (shape) => {
                long newShapeCount = Interlocked.Increment(ref sourceShapeCount);
                if ((newShapeCount & 0xFFFF) == 0) {
                    long oldShapeCount = newShapeCount - 0x10000;
                    int perc = (int)(oldShapeCount * 100 / sourceShapes);
                    int perc2 = (int)(newShapeCount * 100 / sourceShapes);
                    if (perc < perc2) {
                        var s = "*" + perc2 + "%";
                        lock (file) Console.Write(s + new string('\b', s.Length));
                    }
                }
                AddShapes(newShapes, shape, 0, w, 0, h, 0, d, tcc, tec, tfc); // unpadded
            });
        }

        var (ww, hh, dd) = MinRotation((byte)(w + 1), h, d);
        if (ww == tw && hh == th && dd == td) {
            StatusUpdate('|', tcc, tec, tfc);
            long sourceShapeCount = 0;
            Parallel.ForEach(LoadShapes(file), (shape) => {
                long newShapeCount = Interlocked.Increment(ref sourceShapeCount);
                if ((newShapeCount & 0xFFFF) == 0) {
                    long oldShapeCount = newShapeCount - 0x10000;
                    int perc = (int)(oldShapeCount * 100 / sourceShapes);
                    int perc2 = (int)(newShapeCount * 100 / sourceShapes);
                    if (perc < perc2) {
                        var s = "|" + perc2 + "%";
                        lock (file) Console.Write(s + new string('\b', s.Length));
                    }
                }
                AddShapes(newShapes, shape.PadLeft(), 0, 1, 0, h, 0, d, tcc, tec, tfc);
                AddShapes(newShapes, shape.PadRight(), w, w + 1, 0, h, 0, d, tcc, tec, tfc);
            });
        }

        (ww, hh, dd) = MinRotation(w, (byte)(h + 1), d);
        if (ww == tw && hh == th && dd == td) {
            StatusUpdate('-', tcc, tec, tfc);
            long sourceShapeCount = 0;
            Parallel.ForEach(LoadShapes(file), (shape) => {
                long newShapeCount = Interlocked.Increment(ref sourceShapeCount);
                if ((newShapeCount & 0xFFFF) == 0) {
                    long oldShapeCount = newShapeCount - 0x10000;
                    int perc = (int)(oldShapeCount * 100 / sourceShapes);
                    int perc2 = (int)(newShapeCount * 100 / sourceShapes);
                    if (perc < perc2) {
                        var s = "-" + perc2 + "%";
                        lock (file) Console.Write(s + new string('\b', s.Length));
                    }
                }
                AddShapes(newShapes, shape.PadTop(), 0, w, 0, 1, 0, d, tcc, tec, tfc);
                AddShapes(newShapes, shape.PadBottom(), 0, w, h, h + 1, 0, d, tcc, tec, tfc);
            });
        }

        (ww, hh, dd) = MinRotation(w, h, (byte)(d + 1));
        if (ww == tw && hh == th && dd == td) {
            StatusUpdate('/', tcc, tec, tfc);
            long sourceShapeCount = 0;
            Parallel.ForEach(LoadShapes(file), (shape) => {
                long newShapeCount = Interlocked.Increment(ref sourceShapeCount);
                if ((newShapeCount & 0xFFFF) == 0) {
                    long oldShapeCount = newShapeCount - 0x10000;
                    int perc = (int)(oldShapeCount * 100 / sourceShapes);
                    int perc2 = (int)(newShapeCount * 100 / sourceShapes);
                    if (perc < perc2) {
                        var s = "/" + perc2 + "%";
                        lock (file) Console.Write(s + new string('\b', s.Length));
                    }
                }
                AddShapes(newShapes, shape.PadFront(), 0, w, 0, h, 0, 1, tcc, tec, tfc);
                AddShapes(newShapes, shape.PadBack(), 0, w, 0, h, d, d + 1, tcc, tec, tfc);
            });
        }
    }

    private static void StatusUpdate(char step, int tcc, int tec, int tfc) => Console.Write(step + "    " + (tcc >= 0 ? tcc + "," + Pad2(tec) + "," + Pad2(tfc) + "\b\b\b\b\b\b\b\b\b\b\b\b" : "\b\b\b\b\b"));

    private static string Pad2(int v) => v >= 0 && v <= 9 ? "0" + v.ToString() : v.ToString();

    // for each blank cube from x0 to w, y0 to h, z0 to d, if it has an adjacent neighbor
    // add that cube, find the minimum rotation, and add to the newShapes hash set (under
    // lock since we could be doing this in parallel.)
    private static void AddShapes(MyHashSet newShapes, BitShape shape, int x0, int w, int y0, int h, int z0, int d, int tcc, int tec, int tfc) {
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
                            var s = newShape.MinRotation().bytes;
                            lock (newShapes) newShapes.Add(s);
                            //newShapes.Add(s);
                            //newShapes.TryAdd(s, 0);
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
        public required string ext;
        public string filepath => Path.Combine(Program.FILE_PATH, n.ToString(), w + "," + h + "," + d + ext);
        public long size;
    }

    public readonly List<Results> List = new();

    public FileScanner(byte n, string ext = Program.FILE_EXT) {
        var di = new DirectoryInfo(Path.Combine(Program.FILE_PATH, n.ToString()));
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
    private FileStream? fs = null;
    private readonly int length;
    private readonly string path;

    public static void Clear(byte n) {
        var di = new DirectoryInfo(Path.Combine(Program.FILE_PATH, n.ToString()));
        if (!di.Exists)
            di.Create();
        else {
            var list = new FileScanner(n).List;
            foreach (var f in list)
                File.Delete(f.filepath);
            var list2 = new FileScanner(n, ".tmp").List;
            foreach (var f in list2)
                File.Delete(f.filepath);
            if (File.Exists(Path.Combine(Program.FILE_PATH, n.ToString(), Program.FILE_COMPLETE)))
                File.Delete(Path.Combine(Program.FILE_PATH, n.ToString(), Program.FILE_COMPLETE));
        }
    }

    public static void ClearTmp(byte n) {
        var di = new DirectoryInfo(Path.Combine(Program.FILE_PATH, n.ToString()));
        if (!di.Exists)
            di.Create();
        else
            foreach (var f in new FileScanner(n, ".tmp").List)
                File.Delete(f.filepath);
    }

    public FileWriter(int n, int w, int h, int d) {
        path = Path.Combine(Program.FILE_PATH, n.ToString(), w + "," + h + "," + d);
        length = new BitShape((byte)w, (byte)h, (byte)d).bytes.Length;
    }

    public void Write(byte[] shape) {
        if (shape.Length != length) throw new ArgumentOutOfRangeException(nameof(shape), shape.Length, "unexpected shape length - should be " + length);
        if (fs == null)
            //fs = File.Open(path + ".tmp", FileMode.Append);
            fs = new FileStream(path + ".tmp", FileMode.Append, FileAccess.Write, FileShare.None, 65536, FileOptions.None);
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

    public static bool FileExists(int n, int w, int h, int d) => File.Exists(Path.Combine(Program.FILE_PATH, n.ToString(), w + "," + h + "," + d + Program.FILE_EXT));

    public static long FileSize(int n, int w, int h, int d) {
        var fi = new FileInfo(Path.Combine(Program.FILE_PATH, n.ToString(), w + "," + h + "," + d + Program.FILE_EXT));
        if (!fi.Exists) return -1;
        return fi.Length;
    }

    public FileReader(int n, int w, int h, int d) {
        fs = new FileStream(Path.Combine(Program.FILE_PATH, n.ToString(), w + "," + h + "," + d + Program.FILE_EXT), FileMode.Open, FileAccess.Read, FileShare.None, 65536, FileOptions.None);
        //fs = File.OpenRead(Path.Combine(Program.FILE_PATH, n.ToString(), w + "," + h + "," + d + Program.FILE_EXT));
        length = new BitShape((byte)w, (byte)h, (byte)d).bytes.Length;
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
