using System.Collections.Concurrent;
using System.Diagnostics;

namespace ShapeMaker;

//using MyHashSet = HashSet<byte[]>;
//using MyHashSet = BitShapeHashSet;
using MyHashSet = BitShapeHashSet64k;
//using MyHashSet = ConcurrentDictionary<byte[], byte>;

public class Program {
    public static string FILE_PATH = "~/dev/ShapeMaker";
    public const string FILE_EXT = ".bin";
    public const string FILE_COMPLETE = "_COMPLETE";
    public const int MAX_COMPUTE_N = 19;
    public static bool DO_CHIRAL_COUNT = true;
    public static bool DO_FORCE_RECOMPUTE = false;

    /*
        Results and timing (in seconds) from 14" 2023 MacBook Pro w/ 96GB 12-core M2 Max, .NET 7 in Release mode
        n=2, shapes: 1 time: 0.0183248, chiral count: 1 time: 0.0006542
        n=3, shapes: 2 time: 0.0013687, chiral count: 2 time: 0.000129
        n=4, shapes: 8 time: 0.0023709, chiral count: 7 time: 0.0002405
        n=5, shapes: 29 time: 0.0021787, chiral count: 23 time: 0.0007507
        n=6, shapes: 166 time: 0.0074994, chiral count: 112 time: 0.001262
        n=7, shapes: 1,023 time: 0.0096493, chiral count: 607 time: 0.0043927
        n=8, shapes: 6,922 time: 0.0280305, chiral count: 3,811 time: 0.0214148
        n=9, shapes: 48,311 time: 0.2259835, chiral count: 25,413 time: 0.1807897
        n=10, shapes: 346,543 time: 1.587241, chiral count: 178,083 time: 0.5748292
        n=11, shapes: 2,522,522 time: 13.1223066, chiral count: 1,279,537 time: 4.313887
        n=12, shapes: 18,598,427 time: 106.563318, chiral count: 9,371,094 time: 36.8076492
        n=13, shapes: 138,462,649 time: 962.6670521, chiral count: 69,513,546 time: 409.4930811
        n=14, shapes: 1,039,496,297 time: 9737.4709864, chiral count: 520,878,101 time: 3823.9919743
        n=15, shapes: 7,859,514,470 time: 83117.6538951, chiral count: 3,934,285,874 time: 25384.3347744
        n=16, shapes: 59,795,121,480 time: ?, chiral count: 29,915,913,663 time: ?
        Peak memory usage: ~40GB
     */

    // Potential Optimizations / Enhancements:
    // * We could do a counting pass to see how to best partition the data to avoid makeing a bunch
    //   of sharding passes that create few or no new polycubes.

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
        for (int i = 1; i < args.Length; i++) {
            var arg = args[i];
            if (arg.StartsWith("--"))
                if (arg == "--no-chiral-count")
                    DO_CHIRAL_COUNT = false;
                else if (arg == "--force-recompute")
                    DO_FORCE_RECOMPUTE = true;
                else
                    throw new ArgumentException("Unrecognized parameter " + arg);
            else
                FILE_PATH = arg;
        }

        if (FILE_PATH == "~")
            FILE_PATH = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        else if (FILE_PATH.StartsWith("~/"))
            FILE_PATH = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), FILE_PATH.Substring("~/".Length));

        var totalAvailableMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;

        bool recompute = DO_FORCE_RECOMPUTE;

        string completeString = FileReader.NCompleteString(1);
        if (recompute || completeString == null) {
            if (recompute) FileWriter.Clear(1); else FileWriter.ClearTmp(1);
            using (var writer = new FileWriter(1, 1, 1, 1))
                writer.Write(new BitShape("1,1,1,*").bytes);
            FileWriter.MarkNComplete(1, DO_CHIRAL_COUNT ? "n=1, shapes: 1 time: 0, chiral count: 1 time: 0" : "n=1, shapes: 1 time: 0");
        }

        for (byte n = 2; n <= MAX_COMPUTE_N; n++) {
            completeString = FileReader.NCompleteString(n);
            if (!recompute && completeString != null) {
                Console.WriteLine(completeString);
                continue;
            }

            if (recompute) FileWriter.Clear(n); else FileWriter.ClearTmp(n);
            completeString = "n=" + n + ", shapes: ";
            Console.Write(completeString);
            Stopwatch sw = Stopwatch.StartNew();
            TimeSpan additionalTime = TimeSpan.Zero;
            var inputFileList = new FileScanner((byte)(n - 1)).List;
            var targetSizes = ShapeMakerEstimator.ShapeSizesFromExtendingShapes(inputFileList).ToList();
            long shapeCount = 0;
            int currentSizeIndex = 0, targetSizesCount = targetSizes.Count;
            foreach (var size in targetSizes) {
                int shardCount = 0; // don't shard
                // if the combined input size is 1GB, for example, the output is likely to be ~8GB, and ~24GB in memory
                if (n >= 14 && size.w > 1 && size.h > 1 && size.d > 1) {
                    long inMemSize = size.sz * 8; // should be *8*3 normally, but just *8 for BitShapeHashSet
                    if (inMemSize > totalAvailableMemory) {
                        shardCount = -8; // just shard on corner count
                        if (inMemSize / 4 > totalAvailableMemory) { // unless that's not enough
                            shardCount = n; // shard on corner/edge/face counts
                        }
                    }
                }
                currentSizeIndex++;
                if (FileReader.FileExists(n, size.w, size.h, size.d)) {
                    int bytesPerShape = new BitShape(size.w, size.h, size.d).bytes.Length;
                    shapeCount += FileReader.FileSize(n, size.w, size.h, size.d) / bytesPerShape;
                    TimeSpan timeTaken = FileReader.FileTime(n, size.w, size.h, size.d);
                    additionalTime += timeTaken;
                } else {
                    double totalSeconds = additionalTime.Add(sw.Elapsed).TotalSeconds;
                    var progress = "            " + (shardCount != 0 ? "/" + shardCount : "") + "[" + shapeCount.ToString("N0") + ", " + totalSeconds.ToString("N0") + "s, " + size.w + "x" + size.h + "x" + size.d + " " + currentSizeIndex + "/" + targetSizesCount + "]     ";
                    Console.Write(progress + new string('\b', progress.Length));
                    if (n < MAX_COMPUTE_N)
                        using (var writer = new FileWriter(n, size.w, size.h, size.d))
                            shapeCount += ShapeMaker.ShapesFromExtendingShapes(inputFileList, writer, size.w, size.h, size.d, shardCount);
                    else
                        shapeCount += ShapeMaker.ShapesFromExtendingShapes(inputFileList, null, size.w, size.h, size.d, shardCount);
                }
                {
                    double totalSeconds = additionalTime.Add(sw.Elapsed).TotalSeconds;
                    var progress = "            " + (shardCount != 0 ? "/" + shardCount : "") + "[" + shapeCount.ToString("N0") + ", " + totalSeconds.ToString("N0") + "s, " + size.w + "x" + size.h + "x" + size.d + " " + currentSizeIndex + "/" + targetSizesCount + "]     ";
                    Console.Write(progress + new string('\b', progress.Length));
                }
            }
            sw.Stop();
            {
                double totalSeconds = additionalTime.Add(sw.Elapsed).TotalSeconds;
                string progress = shapeCount.ToString("N0") + " time: " + totalSeconds + "      \b\b\b\b\b\b";
                completeString += progress;
                Console.Write(progress);
            }

            if (DO_CHIRAL_COUNT) {
                completeString += DoChiralCount(n, targetSizes);
            }

            Console.WriteLine();
            FileWriter.MarkNComplete(n, completeString);
        }
    }

    private static string DoChiralCount(byte n, List<(byte w, byte h, byte d, long sz)> targetSizes)
    {
        var sw = Stopwatch.StartNew();
        long chiralCount = 0;
        int currentSizeIndex = 0;
        int targetSizesCount = targetSizes.Count;
        foreach (var size in targetSizes)
        {
            string progress = "     " + ++currentSizeIndex + "/" + targetSizesCount + "=" + size.w + "x" + size.h + "x" + size.d + ", " + chiralCount.ToString("N0") + ", " + sw.Elapsed.TotalSeconds.ToString("N0") + "s   ";
            Console.Write(progress + new string('\b', progress.Length));
            FileScanner.Results fileInfo = new FileScanner.Results() { n = n, w = size.w, h = size.h, d = size.d, ext = Program.FILE_EXT };
            int shapeSizeInBytes = new BitShape(size.w, size.h, size.d).bytes.Length;
            long sourceShapes = FileReader.FileSize(n, size.w, size.h, size.d) / shapeSizeInBytes;
            long sourceShapes100 = sourceShapes / 100;
            long sourceShapeCount = 0, nextShapeCount = sourceShapes100;
            int percent = 0;
            Parallel.ForEach(FileReader.LoadShapes(fileInfo), (shape) =>
            {
                long newShapeCount = Interlocked.Increment(ref sourceShapeCount);
                if (newShapeCount == nextShapeCount)
                {
                    percent++;
                    nextShapeCount += sourceShapes100;
                    var progress2 = " " + percent + "%";
                    lock (fileInfo) Console.Write(progress2 + new string('\b', progress2.Length));
                }
                if (shape.IsMinChiralRotation()) Interlocked.Increment(ref chiralCount);
            });
        }
        sw.Stop();
        {
            string progress = ", chiral count: " + chiralCount.ToString("N0") + " time: " + sw.Elapsed.TotalSeconds + "    ";
            Console.Write(progress);
            return progress;
        }
    }
}

public static class ShapeMakerEstimator {
    public static IEnumerable<(byte w, byte h, byte d, long sz)> ShapeSizesFromExtendingShapes(IEnumerable<FileScanner.Results> inputFileList) {
        var totalFileSizeForDimensions = new Dictionary<(byte w, byte h, byte d), long>();
        foreach (var fileInfo in inputFileList)
            foreach (var size in ShapeSizesFromExtendingShapes(fileInfo))
                if (!totalFileSizeForDimensions.TryAdd((size.w, size.h, size.d), size.sz))
                    totalFileSizeForDimensions[(size.w, size.h, size.d)] += size.sz;
        return totalFileSizeForDimensions.ToList()
            .OrderBy(i => i.Key.w * 65536 + i.Key.h * 256 + i.Key.d)
            .Select(i => (i.Key.w, i.Key.h, i.Key.d, i.Value));
    }

    private static IEnumerable<(byte w, byte h, byte d, long sz)> ShapeSizesFromExtendingShapes(FileScanner.Results fileInfo) {
        byte n = fileInfo.n, w = fileInfo.w, h = fileInfo.h, d = fileInfo.d;
        if (n < w * h * d) yield return (w, h, d, fileInfo.size);
        var (w1, h1, d1) = ShapeMakerHelper.MinRotation((byte)(w + 1), h, d);
        yield return (w1, h1, d1, fileInfo.size);
        var (w2, h2, d2) = ShapeMakerHelper.MinRotation(w, (byte)(h + 1), d);
        yield return (w2, h2, d2, fileInfo.size);
        var (w3, h3, d3) = ShapeMakerHelper.MinRotation(w, h, (byte)(d + 1));
        yield return (w3, h3, d3, fileInfo.size);
    }
}

public static class ShapeMakerHelper {
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
}

public static class ShapeMaker {
    public static long ShapesFromExtendingShapes(IEnumerable<FileScanner.Results> fileList, FileWriter writer, byte w, byte h, byte d, int shardCount) {
        var newShapes = new MyHashSet((w * h * d + 7) / 8);
        //var newShapes = new MyHashSet(ByteArrayEqualityComparer.Instance);
        //var newShapes = new MyHashSet[256];
        //for (int i = 0; i < 256; i++) newShapes[i] = new HashSet<byte[]>(ByteArrayEqualityComparer.Instance);

        if (shardCount == 0) {
            foreach (var fileInfo in fileList)
                ShapesFromExtendingShapes(fileInfo, newShapes, w, h, d, -1, -1, -1);
            //if (writer != null) foreach (var hs in newShapes) foreach (var shape in hs) writer.Write(shape);
            //return newShapes.Sum(_ => _.LongCount());
            if (writer != null) foreach (var shape in newShapes) writer.Write(shape);
            return newShapes.LongCount();
        }

        long shapeCount = 0;
        if (shardCount < 0) { // just corner count sharding
            for (int cornerIndex = 0; cornerIndex <= 8; cornerIndex++) {
                foreach (var fileInfo in fileList)
                    ShapesFromExtendingShapes(fileInfo, newShapes, w, h, d, cornerIndex, -1, -1);
                //if (writer != null) foreach (var hs in newShapes) foreach (var shape in hs) writer.Write(shape);
                //foreach (var hs in newShapes) { shapeCount += hs.LongCount(); hs.Clear(); }
                if (writer != null) foreach (var shape in newShapes) writer.Write(shape);
                shapeCount += newShapes.LongCount();
                newShapes.Clear();
            }

        } else {
            int maxInteriorCount = Math.Max(0, w - 2) * Math.Max(0, h - 2) * Math.Max(0, d - 2);
            for (int cornerIndex = 0; cornerIndex <= 8; cornerIndex++)
                for (int edgeIndex = cornerIndex == 0 ? 0 : 1; edgeIndex <= shardCount - cornerIndex; edgeIndex++)
                    for (int faceIndex = edgeIndex == 0 ? 0 : 1; faceIndex <= shardCount - cornerIndex - edgeIndex; faceIndex++)
                        if (cornerIndex + edgeIndex + faceIndex >= shardCount - maxInteriorCount) {
                            foreach (var fileInfo in fileList)
                                ShapesFromExtendingShapes(fileInfo, newShapes, w, h, d, cornerIndex, edgeIndex, faceIndex);
                            //if (writer != null) foreach (var hs in newShapes) foreach (var shape in hs) writer.Write(shape);
                            //foreach (var hs in newShapes) { shapeCount += hs.LongCount(); hs.Clear(); }
                            if (writer != null) foreach (var shape in newShapes) writer.Write(shape);
                            shapeCount += newShapes.LongCount();
                            newShapes.Clear();
                        }

        }
        return shapeCount;
    }

    // for each shape in parallel, try to add cube to it
    // first does by adding cube to the shape in its current size
    // then tries padding each of the 6 faces of the shape and adding a cube there
    private static void ShapesFromExtendingShapes(FileScanner.Results fileInfo, MyHashSet newShapes, byte targetWidth, byte targetHeight, byte targetDepth, int targetCornerCount, int targetEdgeCount, int targetFaceCount) {
        byte w = fileInfo.w, h = fileInfo.h, d = fileInfo.d;
        int shapeSizeInBytes = new BitShape(w, h, d).bytes.Length;
        long sourceShapes = FileReader.FileSize(fileInfo.n, w, h, d) / shapeSizeInBytes;
        long sourceShapes100 = sourceShapes / 100;

        if (w == targetWidth && h == targetHeight && d == targetDepth) {
            StatusUpdate('*', targetCornerCount, targetEdgeCount, targetFaceCount);
            long sourceShapeCount = 0, nextShapeCount = sourceShapes100;
            int percent = 0;
            Parallel.ForEach(FileReader.LoadShapes(fileInfo), (shape) => {
                long newShapeCount = Interlocked.Increment(ref sourceShapeCount);
                if (newShapeCount == nextShapeCount) {
                    percent++;
                    nextShapeCount += sourceShapes100;
                    var progress = "*" + percent + "%";
                    lock (fileInfo) Console.Write(progress + new string('\b', progress.Length));
                }
                AddShapes(newShapes, shape, 0, w, 0, h, 0, d, targetCornerCount, targetEdgeCount, targetFaceCount); // unpadded
            });
        }

        var (wMin, hMin, dMin) = ShapeMakerHelper.MinRotation((byte)(w + 1), h, d);
        if (wMin == targetWidth && hMin == targetHeight && dMin == targetDepth) {
            StatusUpdate('|', targetCornerCount, targetEdgeCount, targetFaceCount);
            long sourceShapeCount = 0, nextShapeCount = sourceShapes100;
            int percent = 0;
            Parallel.ForEach(FileReader.LoadShapes(fileInfo), (shape) => {
                long newShapeCount = Interlocked.Increment(ref sourceShapeCount);
                if (newShapeCount == nextShapeCount) {
                    percent++;
                    nextShapeCount += sourceShapes100;
                    var progress = "|" + percent + "%";
                    lock (fileInfo) Console.Write(progress + new string('\b', progress.Length));
                }
                AddShapes(newShapes, shape.PadLeft(), 0, 1, 0, h, 0, d, targetCornerCount, targetEdgeCount, targetFaceCount);
                AddShapes(newShapes, shape.PadRight(), w, w + 1, 0, h, 0, d, targetCornerCount, targetEdgeCount, targetFaceCount);
            });
        }

        (wMin, hMin, dMin) = ShapeMakerHelper.MinRotation(w, (byte)(h + 1), d);
        if (wMin == targetWidth && hMin == targetHeight && dMin == targetDepth) {
            StatusUpdate('-', targetCornerCount, targetEdgeCount, targetFaceCount);
            long sourceShapeCount = 0, nextShapeCount = sourceShapes100;
            int percent = 0;
            Parallel.ForEach(FileReader.LoadShapes(fileInfo), (shape) => {
                long newShapeCount = Interlocked.Increment(ref sourceShapeCount);
                if (newShapeCount == nextShapeCount) {
                    percent++;
                    nextShapeCount += sourceShapes100;
                    var progress = "-" + percent + "%";
                    lock (fileInfo) Console.Write(progress + new string('\b', progress.Length));
                }
                AddShapes(newShapes, shape.PadTop(), 0, w, 0, 1, 0, d, targetCornerCount, targetEdgeCount, targetFaceCount);
                AddShapes(newShapes, shape.PadBottom(), 0, w, h, h + 1, 0, d, targetCornerCount, targetEdgeCount, targetFaceCount);
            });
        }

        (wMin, hMin, dMin) = ShapeMakerHelper.MinRotation(w, h, (byte)(d + 1));
        if (wMin == targetWidth && hMin == targetHeight && dMin == targetDepth) {
            StatusUpdate('/', targetCornerCount, targetEdgeCount, targetFaceCount);
            long sourceShapeCount = 0, nextShapeCount = sourceShapes100;
            int percent = 0;
            Parallel.ForEach(FileReader.LoadShapes(fileInfo), (shape) => {
                long newShapeCount = Interlocked.Increment(ref sourceShapeCount);
                if (newShapeCount == nextShapeCount) {
                    percent++;
                    nextShapeCount += sourceShapes100;
                    var progress = "/" + percent + "%";
                    lock (fileInfo) Console.Write(progress + new string('\b', progress.Length));
                }
                AddShapes(newShapes, shape.PadFront(), 0, w, 0, h, 0, 1, targetCornerCount, targetEdgeCount, targetFaceCount);
                AddShapes(newShapes, shape.PadBack(), 0, w, 0, h, d, d + 1, targetCornerCount, targetEdgeCount, targetFaceCount);
            });
        }
    }

    private static void StatusUpdate(char stepChar, int targetCornerCount, int targetEdgeCount, int targetFaceCount) {
        Console.Write(stepChar + "    " + (targetCornerCount >= 0 ? targetCornerCount + "," + Pad2(targetEdgeCount) + "," + Pad2(targetFaceCount) + "\b\b\b\b\b\b\b\b\b\b\b\b" : "\b\b\b\b\b"));
    }

    private static string Pad2(int value) => value >= 0 && value <= 9 ? "0" + value.ToString() : value.ToString();

    // for each blank cube from x0 to w, y0 to h, z0 to d, if it has an adjacent neighbor
    // add that cube, find the minimum rotation, and add to the newShapes hash set (under
    // lock since we could be doing this in parallel.)
    private static void AddShapes(MyHashSet newShapes, BitShape shape, int xStart, int w, int yStart, int h, int zStart, int d, int targetCornerCount, int targetEdgeCount, int targetFaceCount) {
        int cornerCount = 0, edgeCount = 0, faceCount = 0;
        if (targetCornerCount >= 0) {
            var counts = shape.CornerEdgeFaceCount();
            if (counts.corners > targetCornerCount || (counts.corners + 1) < targetCornerCount) return;
            if (targetEdgeCount >= 0) {
                if (counts.edges > targetEdgeCount || (counts.edges + 1) < targetEdgeCount) return;
                if (counts.faces > targetFaceCount || (counts.faces + 1) < targetFaceCount) return;
            }
            (cornerCount, edgeCount, faceCount) = counts;
        }
        var newShape = new BitShape(shape);
        var newShapeBytes = newShape.bytes;
        var shapeBytes = shape.bytes;
        var shapeBytesLength = shapeBytes.Length;
        int xLimit = shape.w - 1, yLimit = shape.h - 1, zLimit = shape.d - 1;
        for (var x = xStart; x < w; x++) {
            bool xFace = x == 0 || x == xLimit;
            for (var y = yStart; y < h; y++) {
                bool yFace = y == 0 || y == yLimit;
                for (var z = zStart; z < d; z++) {
                    if (targetCornerCount >= 0) {
                        bool zFace = z == 0 || z == zLimit;
                        bool isInterior = !xFace && !yFace && !zFace;
                        if (isInterior && (targetCornerCount != cornerCount || targetEdgeCount != edgeCount || targetFaceCount != faceCount)) continue;
                        bool isCorner = xFace && yFace && zFace;
                        if (isCorner && targetCornerCount != cornerCount + 1) continue;
                        if (targetEdgeCount >= 0) {
                            bool isEdge = xFace && yFace || yFace && zFace || xFace && zFace;
                            if (isEdge && targetEdgeCount != edgeCount + 1) continue;
                            bool isFace = !isCorner && !isEdge && !isInterior;
                            if (isFace && targetFaceCount != faceCount + 1) continue;
                        }
                    }
                    if (!shape[x, y, z])
                        if (shape.HasSetNeighbor(x, y, z)) {
                            Array.Copy(shapeBytes, newShapeBytes, shapeBytesLength);
                            newShape[x, y, z] = true;
                            var bytes = newShape.MinRotation().bytes;
                            //lock (newShapes) newShapes.Add(bytes);
                            newShapes.Add(bytes);
                            //newShapes.TryAdd(bytes, 0);
                            //int len = bytes.Length; byte hashIndex = len < 3 ? bytes[0] : bytes[len - 2]; // use last full byte
                            //lock (newShapes[hashIndex]) newShapes[hashIndex].Add(bytes);
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
        public string filepath => FileReader.FilePath(n, w, h, d, ext);
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
    private readonly DateTime createTime;

    public static void Clear(byte n) {
        var di = new DirectoryInfo(Path.Combine(Program.FILE_PATH, n.ToString()));
        if (!di.Exists)
            di.Create();
        else {
            var list = new FileScanner(n).List;
            foreach (var f in list)
                File.Delete(f.filepath);
            var tmpList = new FileScanner(n, ".tmp").List;
            foreach (var f in tmpList)
                File.Delete(f.filepath);
            var completePath = Path.Combine(Program.FILE_PATH, n.ToString(), Program.FILE_COMPLETE);
            if (File.Exists(completePath))
                File.Delete(completePath);
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

    public static void MarkNComplete(int n, string s) {
        var path = Path.Combine(Program.FILE_PATH, n.ToString(), Program.FILE_COMPLETE);
        File.WriteAllText(path, s);
    }

    public FileWriter(int n, int w, int h, int d) {
        path = FileReader.FilePath(n, w, h, d, "");
        length = new BitShape((byte)w, (byte)h, (byte)d).bytes.Length;
        createTime = DateTime.UtcNow;
    }

    public void Write(byte[] shape) {
        if (shape.Length != length) throw new ArgumentOutOfRangeException(nameof(shape), shape.Length, "unexpected shape length - should be " + length);
        if (fs == null)
            fs = new FileStream(path + ".tmp", FileMode.Append, FileAccess.Write, FileShare.None, 65536, FileOptions.None);
        fs.Write(shape);
    }

    public void Dispose() {
        fs?.Dispose();
        if (File.Exists(path + ".tmp")) {
            var updateTime = DateTime.UtcNow;
            File.SetCreationTimeUtc(path + ".tmp", createTime);
            File.SetLastWriteTimeUtc(path + ".tmp", updateTime);
            File.Move(path + ".tmp", path + Program.FILE_EXT);
        }
    }
}

public class FileReader : IDisposable {
    private readonly FileStream fs;
    private readonly int length;

    public static string FilePath(int n, int w, int h, int d, string ext = Program.FILE_EXT) => Path.Combine(Program.FILE_PATH, n.ToString(), w + "," + h + "," + d + ext);

    public static bool FileExists(int n, int w, int h, int d) => File.Exists(FilePath(n, w, h, d));

    public static long FileSize(int n, int w, int h, int d) {
        var fi = new FileInfo(FilePath(n, w, h, d));
        if (!fi.Exists) return -1;
        return fi.Length;
    }

    public static TimeSpan FileTime(int n, int w, int h, int d) {
        var fi = new FileInfo(FilePath(n, w, h, d));
        if (!fi.Exists) return TimeSpan.Zero;
        return fi.LastWriteTimeUtc - fi.CreationTimeUtc;
    }

    public static string NCompleteString(int n) {
        var path = Path.Combine(Program.FILE_PATH, n.ToString(), Program.FILE_COMPLETE);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    public static IEnumerable<BitShape> LoadShapes(FileScanner.Results fileInfo) {
        byte n = fileInfo.n, w = fileInfo.w, h = fileInfo.h, d = fileInfo.d;
        using (var reader = new FileReader(n, w, h, d))
            for (; ; ) {
                var bytes = reader.Read();
                if (bytes == null) break;
                yield return new BitShape(w, h, d, bytes);
            }
    }

    public FileReader(int n, int w, int h, int d) {
        fs = new FileStream(FilePath(n, w, h, d), FileMode.Open, FileAccess.Read, FileShare.None, 65536, FileOptions.None);
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
