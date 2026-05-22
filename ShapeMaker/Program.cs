using System.Diagnostics;

namespace ShapeMaker;

/// <summary>
/// Main program class.
/// </summary>
public static class Program {
    public const string FILE_EXT = ".bin";
    public const string FILE_COMPLETE = "_COMPLETE";

    public static ShapeMakerOptions options = new();

    /// <summary>
    /// Performs the computation to find all possible shapes of voxel count n, as well as all the mirror unique shapes.
    /// </summary>
    static int Main(string[] args) {
        int exitCode = ShapeMakerOptions.ParseCommandLineOptions(args, options);
        if (exitCode >= 0) return exitCode;

        var totalUsableMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 2;

        // compute shape for n=1 to get started
        string completeString = FileReader.NCompleteString(1);
        if (options.doForceRecompute || completeString is null) {
            if (options.doForceRecompute)
                FileWriter.Clear(1);
            else
                FileWriter.ClearTmp(1);
            using (var writer = new FileWriter(1, 1, 1, 1))
                writer.Write(new BitShape("1x1x1,*").bytes);
            FileWriter.MarkNComplete(1,
                options.doMirrorCount ? "n=1, shapes: 1 time: 0, mirror count: 1 time: 0" : "n=1, shapes: 1 time: 0");
        }

        for (byte n = 2; n <= options.maxComputeN; n++) {
            completeString = FileReader.NCompleteString(n);
            if (!options.doForceRecompute && completeString is not null) {
                Console.WriteLine(completeString);
                continue;
            }

            if (options.doForceRecompute)
                FileWriter.Clear(n);
            else
                FileWriter.ClearTmp(n);
            completeString = "n=" + n + ", shapes: ";
            Console.Write(completeString);
            Stopwatch sw = Stopwatch.StartNew();
            TimeSpan additionalTime = TimeSpan.Zero;
            var inputFileList = new FileScanner((byte)(n - 1)).List
                .OrderByDescending(f => f.size)
                .ToList();
            var targetSizes = ShapeMakerEstimator.ShapeSizesFromExtendingShapes(inputFileList)
                .OrderByDescending(f => f.sz)
                .ToList();
            long shapeCount = 0, mirrorCount = 0;
            int currentSizeIndex = 0, targetSizesCount = targetSizes.Count;
            foreach (var size in targetSizes) {
                int shardCount = 1; // default: don't shard
                long inMemSize = size.sz * 8; // next n should have about 8x more polycubes
                if (options.hashSetAlgorithm is HashSetAlgorithm.Dictionary or HashSetAlgorithm.HashSet
                    or HashSetAlgorithm.HashSet256)
                    inMemSize *= 2; // these implementations use ~2x memory
                if (inMemSize > totalUsableMemory) {
                    inMemSize += inMemSize; // double because of uneven shards
                    shardCount += (int)(inMemSize / totalUsableMemory);
                }
                currentSizeIndex++;
                if (FileReader.FileExists(n, size.w, size.h, size.d)) {
                    int bytesPerShape = new BitShape(size.w, size.h, size.d).bytes.Length;
                    shapeCount += FileReader.FileSize(n, size.w, size.h, size.d) / bytesPerShape;
                    TimeSpan timeTaken = FileReader.FileTime(n, size.w, size.h, size.d);
                    additionalTime += timeTaken;
                } else {
                    double totalSeconds = additionalTime.Add(sw.Elapsed).TotalSeconds;
                    var progress = "            " + (shardCount != 1 ? "/" + shardCount : "") +
                                   "[" + shapeCount.ToString("N0") + ", " + totalSeconds.ToString("N0") + "s, " +
                                   size.w + "x" + size.h + "x" + size.d + " " + currentSizeIndex + "/" +
                                   targetSizesCount + "]     ";
                    ConsoleWriteWithBackspace(progress);
                    if (n < options.maxComputeN)
                        using (var writer = new FileWriter(n, size.w, size.h, size.d)) {
                            var result = ShapesFromExtendingShapes(inputFileList, writer, size, shardCount);
                            shapeCount += result.shapeCount;
                            mirrorCount += result.mirrorCount;
                        }
                    else {
                        var result = ShapesFromExtendingShapes(inputFileList, null, size, shardCount);
                        shapeCount += result.shapeCount;
                        mirrorCount += result.mirrorCount;
                    }
                }
                {
                    double totalSeconds = additionalTime.Add(sw.Elapsed).TotalSeconds;
                    var progress = "            " + (shardCount != 1 ? "/" + shardCount : "") +
                                   "[" + shapeCount.ToString("N0") + ", " + totalSeconds.ToString("N0") + "s, " +
                                   size.w + "x" + size.h + "x" + size.d + " " + currentSizeIndex + "/" +
                                   targetSizesCount + "]     ";
                    ConsoleWriteWithBackspace(progress);
                }
            }
            sw.Stop();
            {
                double totalSeconds = additionalTime.Add(sw.Elapsed).TotalSeconds;
                string progress = shapeCount.ToString("N0");
                if (options.doMirrorCount)
                    progress += ", mirror count: " + mirrorCount.ToString("N0");
                progress += " time: " + totalSeconds;
                completeString += progress;
                Console.Write(progress);
                ConsoleWriteWithBackspace("      ");
            }

            Console.WriteLine();
            FileWriter.MarkNComplete(n, completeString);
        }
        return 0;
    }

    /// <summary>
    /// Extend list of files of shapes to find new shapes and add them to a hashset.
    /// </summary>
    /// <param name="fileList">list of files of previous n voxel count shapes</param>
    /// <param name="writer">file writer to store new found shapes into</param>
    /// <param name="size">size of shapes to extend (width, height, depth)</param>
    /// <param name="shardCount">shardCount - if 1 no sharding</param>
    /// <returns>(shape count found for target size {width} {height} {depth}, mirror unique shape count for size)</returns>
    private static (long shapeCount, long mirrorCount) ShapesFromExtendingShapes(IList<FileScanner.Results> fileList, FileWriter writer, (byte w, byte h, byte d, long _) size, int shardCount) {
        int bytesLength = (size.w * size.h * size.d + 7) / 8;
        IBitShapeHashSet newShapes = options.hashSetAlgorithm switch {
            HashSetAlgorithm.HashSet => BitShapeHashSetFactory.CreateWithHashSet(use256HashSets: false),
            HashSetAlgorithm.HashSet256 => BitShapeHashSetFactory.CreateWithHashSet(use256HashSets: true),
            HashSetAlgorithm.Dictionary => BitShapeHashSetFactory.CreateWithDictionary(),
            HashSetAlgorithm.HashSet64K => BitShapeHashSetFactory.Create(bytesLength, preferSpeedOverMemory: false),
            HashSetAlgorithm.HashSet16M => BitShapeHashSetFactory.Create(bytesLength, preferSpeedOverMemory: true),
            _ => throw new ArgumentException("Unrecognized hash set algorithm " + options.hashSetAlgorithm),
        };

        long shapeCount = 0, mirrorCount = 0;
        for (int shard = 0; shard < shardCount; shard++) {
            var result = ShapesFromExtendingShapes(fileList, writer, newShapes, size.w, size.h, size.d, shardCount, shard);
            shapeCount += result.shapeCount;
            mirrorCount += result.mirrorCount;
        }
        return (shapeCount, mirrorCount);
    }

    /// <summary>
    /// Extend list of files of shapes to find new shapes and add them to a hashset.
    /// </summary>
    /// <param name="fileList">list of files of previous n voxel count shapes</param>
    /// <param name="writer">file writer to store new found shapes into</param>
    /// <param name="newShapes">hash set to add to</param>
    /// <param name="targetWidth">target width</param>
    /// <param name="targetHeight">target height</param>
    /// <param name="targetDepth">target depth</param>
    /// <param name="shardCount">shard count</param>
    /// <param name="shard">shard number</param>
    /// <returns>(shape count, mirror shape count)</returns>
    private static (long shapeCount, long mirrorCount) ShapesFromExtendingShapes(IEnumerable<FileScanner.Results> fileList, FileWriter writer, IBitShapeHashSet newShapes, byte targetWidth, byte targetHeight, byte targetDepth, int shardCount, int shard) {
        long shapeCount = 0, mirrorCount = 0;
        foreach (var fileInfo in fileList)
            mirrorCount += ShapesFromExtendingShapes(fileInfo, newShapes, targetWidth, targetHeight, targetDepth, shardCount, shard);

        if (writer is not null)
            foreach (var shape in newShapes) {
                writer.Write(shape);
                shapeCount++;
            }
        else
            shapeCount = newShapes.Count();
        newShapes.Clear();
        // we collect here because all the shapes are written out
        GC.Collect(GC.MaxGeneration);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration);
        return (shapeCount, mirrorCount);
    }

    /// <summary>
    /// Extend file of shapes to find new shapes and add them to a hashset. For each shape in parallel, try to
    /// add voxel to it. First does by adding voxel to the shape in its current size, then tries padding each
    /// of the 6 faces of the shape and adding a voxel there.
    /// </summary>
    /// <param name="fileInfo">file of previous n voxel count shapes</param>
    /// <param name="newShapes">hash set to add to</param>
    /// <param name="targetWidth">target width</param>
    /// <param name="targetHeight">target height</param>
    /// <param name="targetDepth">target depth</param>
    /// <param name="shardCount">shard count</param>
    /// <param name="shard">shard number</param>
    /// <returns>oddly, returns the number of mirror unique shapes found</returns>
    private static long ShapesFromExtendingShapes(FileScanner.Results fileInfo, IBitShapeHashSet newShapes, byte targetWidth, byte targetHeight, byte targetDepth, int shardCount, int shard) {
        byte w = fileInfo.w, h = fileInfo.h, d = fileInfo.d;
        int shapeSizeInBytes = new BitShape(w, h, d).bytes.Length;
        long sourceShapes = FileReader.FileSize(fileInfo.n, w, h, d) / shapeSizeInBytes;
        long sourceShapes100 = sourceShapes / 100;
        long mirrorCount = 0;

        if (w == targetWidth && h == targetHeight && d == targetDepth) {
            // target shape size is same as source shape size, so we are just adding a voxel to the shape
            StatusUpdate('*', shardCount, shard);
            long sourceShapeCount = 0, nextShapeCount = sourceShapes100;
            int percent = 0;
            Parallel.ForEach(FileReader.LoadShapes(fileInfo), shape => {
                if (Interlocked.Increment(ref sourceShapeCount) == nextShapeCount) {
                    nextShapeCount += sourceShapes100;
                    ConsoleWriteWithBackspace("*" + ++percent + "%");
                }
                long mc = AddShapes(newShapes, shape, 0, w, 0, h, 0, d, shardCount, shard);
                Interlocked.Add(ref mirrorCount, mc);
            });
        }

        var (wMin, hMin, dMin) = ShapeMakerHelper.MinRotation((byte)(w + 1), h, d);
        if (wMin == targetWidth && hMin == targetHeight && dMin == targetDepth) {
            // target shape size is one voxel wider than source shape size, so we are adding a layer on the left and right and adding a voxel to that layer
            StatusUpdate('|', shardCount, shard);
            long sourceShapeCount = 0, nextShapeCount = sourceShapes100;
            int percent = 0;
            Parallel.ForEach(FileReader.LoadShapes(fileInfo), shape => {
                if (Interlocked.Increment(ref sourceShapeCount) == nextShapeCount) {
                    nextShapeCount += sourceShapes100;
                    ConsoleWriteWithBackspace("|" + ++percent + "%");
                }
                long mc1 = AddShapes(newShapes, shape.PadLeft(), 0, 1, 0, h, 0, d, shardCount, shard);
                long mc2 = AddShapes(newShapes, shape.PadRight(), w, w + 1, 0, h, 0, d, shardCount, shard);
                Interlocked.Add(ref mirrorCount, mc1 + mc2);
            });
        }

        (wMin, hMin, dMin) = ShapeMakerHelper.MinRotation(w, (byte)(h + 1), d);
        if (wMin == targetWidth && hMin == targetHeight && dMin == targetDepth) {
            // target shape size is one voxel taller than source shape size, so we are adding a layer on the top and bottom and adding a voxel to that layer
            StatusUpdate('-', shardCount, shard);
            long sourceShapeCount = 0, nextShapeCount = sourceShapes100;
            int percent = 0;
            Parallel.ForEach(FileReader.LoadShapes(fileInfo), shape => {
                if (Interlocked.Increment(ref sourceShapeCount) == nextShapeCount) {
                    nextShapeCount += sourceShapes100;
                    ConsoleWriteWithBackspace("-" + ++percent + "%");
                }
                long mc1 = AddShapes(newShapes, shape.PadTop(), 0, w, 0, 1, 0, d, shardCount, shard);
                long mc2 = AddShapes(newShapes, shape.PadBottom(), 0, w, h, h + 1, 0, d, shardCount, shard);
                Interlocked.Add(ref mirrorCount, mc1 + mc2);
            });
        }

        (wMin, hMin, dMin) = ShapeMakerHelper.MinRotation(w, h, (byte)(d + 1));
        if (wMin == targetWidth && hMin == targetHeight && dMin == targetDepth) {
            // target shape size is one voxel deeper than source shape size, so we are adding a layer on the front and back and adding a voxel to that layer
            StatusUpdate('/', shardCount, shard);
            long sourceShapeCount = 0, nextShapeCount = sourceShapes100;
            int percent = 0;
            Parallel.ForEach(FileReader.LoadShapes(fileInfo), shape => {
                if (Interlocked.Increment(ref sourceShapeCount) == nextShapeCount) {
                    nextShapeCount += sourceShapes100;
                    ConsoleWriteWithBackspace("/" + ++percent + "%");
                }
                long mc1 = AddShapes(newShapes, shape.PadFront(), 0, w, 0, h, 0, 1, shardCount, shard);
                long mc2 = AddShapes(newShapes, shape.PadBack(), 0, w, 0, h, d, d + 1, shardCount, shard);
                Interlocked.Add(ref mirrorCount, mc1 + mc2);
            });
        }
        return mirrorCount;
    }

    /// <summary>
    /// Writes to console, but returning the cursor to the previous position.
    /// </summary>
    /// <param name="text">text to write to console</param>
    private static void ConsoleWriteWithBackspace(string text) {
        lock (Console.Out) {
            Console.Write(text);
            Console.Write(new string('\b', text.Length));
        }
    }

    /// <summary>
    /// Writes shape size status to console.
    /// </summary>
    /// <param name="stepChar">step character, * for interior, - for top/bottom extension, | for left/right extension,
    /// / for front/back extension</param>
    /// <param name="shardCount">shard count</param>
    /// <param name="shard">shard number</param>
    private static void StatusUpdate(char stepChar, int shardCount, int shard) {
        ConsoleWriteWithBackspace(stepChar + (shardCount > 1 ? (shard + 1).ToString() : "").PadLeft(11));
    }

    /// <summary>
    /// Adds new possible shapes to hash set. For each blank voxel in range, if it has an adjacent neighbor, add that
    /// voxel, find the minimum rotation and add to the hash set. This method is expected to be called in parallel,
    /// so any operations done in here must be thread-safe, especially adding to the hash set.
    /// </summary>
    /// <param name="newShapes">hash set to add to</param>
    /// <param name="shape">starting shape</param>
    /// <param name="xStart">starting x coordinate to try extending</param>
    /// <param name="w">width on x coordinate to try extending</param>
    /// <param name="yStart">starting y coordinate to try extending</param>
    /// <param name="h">height on y coordinate to try extending</param>
    /// <param name="zStart">starting z coordinate to try extending</param>
    /// <param name="d">depth of z coordinate to try extending</param>
    /// <param name="shardCount">shard count</param>
    /// <param name="shard">shard number</param>
    /// <returns>oddly, this returns the mirror count shapes found</returns>
    private static long AddShapes(IBitShapeHashSet newShapes, BitShape shape, int xStart, int w, int yStart, int h, int zStart, int d, int shardCount, int shard) {
        long mirrorCount = 0;
        var newShape = new BitShape(shape);
        var newShapeBytes = newShape.bytes;
        var shapeBytes = shape.bytes;
        var shapeBytesLength = shapeBytes.Length;
        bool needToCopy = newShapes is BitShapeHashSet16M or BitShapeHashSet64K;
        for (var x = xStart; x < w; x++) {
            for (var y = yStart; y < h; y++) {
                for (var z = zStart; z < d; z++) {
                    if (!shape[x, y, z] && shape.HasSetNeighbor(x, y, z)) {
                        if (needToCopy) {
                            // we need to copy the shape bytes because the hash implementations don't copy them
                            Array.Copy(shapeBytes, newShapeBytes, shapeBytesLength);
                            newShape[x, y, z] = true;
                            var minRotation = newShape.MinRotation();
                            if (minRotation.IsInShard(shard, shardCount)) {
                                bool added = newShapes.Add(minRotation.bytes);
                                if (options.doMirrorCount && added && minRotation.IsMinMirrorRotation())
                                    Interlocked.Increment(ref mirrorCount);
                            }
                        } else {
                            // we don't need to copy the shape bytes because these hash implementations end up copying them anyway
                            newShape[x, y, z] = true;
                            var minRotation = newShape.MinRotation();
                            if (minRotation.IsInShard(shard, shardCount)) {
                                bool added = newShapes.Add(minRotation.bytes);
                                if (options.doMirrorCount && added && minRotation.IsMinMirrorRotation())
                                    Interlocked.Increment(ref mirrorCount);
                            }
                            newShape[x, y, z] = false;
                        }
                    }
                }
            }
        }
        return mirrorCount;
    }
}