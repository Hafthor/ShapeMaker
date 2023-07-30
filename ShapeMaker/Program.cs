using System.Diagnostics;
using System.IO.Compression;
using System.Text;

namespace ShapeMaker;

public class Program {
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
    // * Avoid rotations - should only need to test all rotations when w==h==d.
    // * Refactor BitShape to be extension methods of a byte array. First byte to hold length,
    //   second byte to hold w,h, remaining bytes to hold bits.

    // Potential Features:
    // * Make a 4-D version?
    // * Scale this out by making one file per {n},{w},{h},{d} combination. To run, we process
    //   all {n-1},{w},{h},{d} and write new unpadded shapes to {n},{w},{h},{d}, then we reprocess
    //   that list for the X paddings (left,right) of that to {n},{w+1},{h},{d} and so on. Of
    //   course, it may be that we have to write to {n},{h},{w+1},{d} instead, or some other
    //   minimal rotation.
    //   Ideally, we'd have a fast way to read/write hashsets, but also a way to easily read serially
    //   shape-by-shape for the input side of extending shapes. It might be easiest to store these
    //   sorted, use binary search to see if we already have that shape, if not add to a hashset,
    //   then when done, sort the hashset and merge into the sorted list and write that out.

    //   for example:
    //   1/1-1-1.txt file: "1"
    //   2/1-1-2.txt file: "11" (from Z padding on 1/1-1-1)
    //   3/1-1-3.txt file: "111" (from Z padding on 2/1-1-2)
    //   3/1-2-2.txt file: "0111" (from Y padding on 2/1-1-2)
    //   4/1-1-4.txt file: "1111" (from Z padding on 3/1-1-3)
    //   4/1-2-2.txt file: "1111" (from unpadded change on 3/1-2-2)
    //   4/1-2-3.txt file: "001111","010111","011110" (from paddings on 3/1-1-3 and 3/1-2-2)
    //   4/2-2-2.txt file: "00010111","0001110","00100111" (from X padding on 3/1-2-2)

    static int showOnCount = 0; // used to show progress
    const int showEveryCount = 1_000_000;

    static void Main(string[] args) {
        bool doChiral = true;

        byte[] shape1 = BitShape.Deserialize("1,1,1,*");
        using (var fzsw = new FileGzipStreamWriter("shapes-1.txt.gz"))
            fzsw.WriteLine(shape1.Serialize());

        for (var n = 2; n < 15; n++) {
            { // scoping so shapes goes out of scope after we save it
                showOnCount = showEveryCount;
                Console.Write("n=" + n + ", shapes: ");
                using (var fgsw = new FileGzipStreamWriter("shapes-" + n + ".txt.gz")) {
                    Stopwatch sw = Stopwatch.StartNew();
                    var shapes = ShapesFromExtendingShapes(LoadShapes(n - 1), sw, fgsw);
                    sw.Stop();
                    Console.Write(shapes.Count.ToString("N0") + " time: " + sw.Elapsed.TotalSeconds);
                    shapes.Clear(); // just to be sure we release memory for this large object
                    shapes = null; // just to be sure we release memory for this large object
                }
            }

            if (doChiral) {
                showOnCount = showEveryCount;
                Console.Write(", chiral shapes: ");
                Console.Write("n=" + n + ", shapes: ");
                using (var fgsw = new FileGzipStreamWriter("chiral-shapes-" + n + ".txt.gz")) {
                    Stopwatch sw = Stopwatch.StartNew();
                    var chiral = ChiralShapes(LoadShapes(n), sw, fgsw);
                    sw.Stop();
                    Console.Write(chiral.Count.ToString("N0") + " time: " + sw.Elapsed.TotalSeconds);
                    chiral.Clear(); // just to be sure we release memory for this large object
                    chiral = null; // just to be sure we release memory for this large object
                }
            }

            Console.WriteLine();
        }
    }

    private static IEnumerable<byte[]> LoadShapes(int n) {
        using (var fs = File.OpenRead("shapes-" + n + ".txt.gz"))
        using (var zr = new GZipStream(fs, CompressionMode.Decompress))
        using (var sr = new StreamReader(zr, Encoding.UTF8, false, 65536))
            for (; ; ) {
                var l = sr.ReadLine();
                if (l == null) break;
                yield return BitShape.Deserialize(l);
            }
    }

    // for each shape in parallel, try to add cube to it
    // first does by adding cube to the shape in its current size
    // then tries padding each of the 6 faces of the shape and adding a cube there
    public static HashSet<byte[]> ShapesFromExtendingShapes(IEnumerable<byte[]> shapes, Stopwatch sw, FileGzipStreamWriter fgsw) {
        var newShapes = new HashSet<byte[]>(ByteArrayEqualityComparer.Instance);

        Parallel.ForEach(shapes, (shape) => {
            var (w, h, d) = shape.Dimensions();

            AddShapes(newShapes, sw, fgsw, shape, 0, w, 0, h, 0, d); // unpadded
            AddShapes(newShapes, sw, fgsw, shape.PadLeft(), 0, 1, 0, h, 0, d);
            AddShapes(newShapes, sw, fgsw, shape.PadRight(), w, w + 1, 0, h, 0, d);
            AddShapes(newShapes, sw, fgsw, shape.PadTop(), 0, w, 0, 1, 0, d);
            AddShapes(newShapes, sw, fgsw, shape.PadBottom(), 0, w, h, h + 1, 0, d);
            AddShapes(newShapes, sw, fgsw, shape.PadFront(), 0, w, 0, h, 0, 1);
            AddShapes(newShapes, sw, fgsw, shape.PadBack(), 0, w, 0, h, d, d + 1);
        });

        return newShapes;
    }

    // for each blank cube from x0 to w, y0 to h, z0 to d, if it has an adjacent neighbor
    // add that cube, find the minimum rotation, and add to the newShapes hash set (under
    // lock since we could be doing this in parallel.)
    private static void AddShapes(HashSet<byte[]> newShapes, Stopwatch sw, FileGzipStreamWriter fgsw, byte[] shape, int x0, int w, int y0, int h, int z0, int d) {
        for (var x = x0; x < w; x++)
            for (var y = y0; y < h; y++)
                for (var z = z0; z < d; z++)
                    if (!shape.Get(x, y, z))
                        if (HasSetNeighbor(shape, x, y, z)) {
                            var newShape = shape.Copy();
                            newShape.Set(x, y, z, true);
                            var s = newShape.MinRotation();
                            lock (newShapes) {
                                int oldCount = newShapes.Count;
                                if (newShapes.Add(s))
                                    fgsw.WriteLine(s.Serialize());
                                bool showAndNext = newShapes.Count >= showOnCount && newShapes.Count != oldCount;
                                if (showAndNext) {
                                    var ss = "[" + showOnCount / 1_000_000 + "m " + sw.Elapsed.TotalSeconds.ToString("0") + "s]";
                                    Console.Write(ss + new string('\b', ss.Length));
                                    showOnCount += showEveryCount;
                                }
                            }
                        }
    }

    // for each shape in parallel, get its minimum chiral rotation and add to
    // newShapes hash set under lock
    public static HashSet<byte[]> ChiralShapes(IEnumerable<byte[]> shapes, Stopwatch sw, FileGzipStreamWriter fgsw) {
        var newShapes = new HashSet<byte[]>(ByteArrayEqualityComparer.Instance);

        Parallel.ForEach(shapes, (shape) => {
            var newShape = shape.Copy().MinChiralRotation();
            lock (newShapes) {
                int oldCount = newShapes.Count;
                if (newShapes.Add(newShape))
                    fgsw.WriteLine(newShape.Serialize());
                bool showAndNext = newShapes.Count >= showOnCount && newShapes.Count != oldCount;
                if (showAndNext) {
                    var ss = "[" + showOnCount / 1_000_000 + "m " + sw.Elapsed.TotalSeconds.ToString("0") + "s]";
                    Console.Write(ss + new string('\b', ss.Length));
                    showOnCount += showEveryCount;
                }
            }
        });

        return newShapes;
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

public class FileGzipStreamWriter : IDisposable {
    private readonly FileStream fs;
    private readonly GZipStream zw;
    private readonly StreamWriter sw;

    public FileGzipStreamWriter(string filename) {
        fs = File.Create(filename);
        zw = new GZipStream(fs, CompressionLevel.Fastest);
        sw = new StreamWriter(zw, Encoding.UTF8, 65536);
    }

    public void WriteLine(string s) {
        sw.WriteLine(s);
    }

    public void Dispose() {
        sw.Dispose();
        zw.Dispose();
        fs.Dispose();
    }
}