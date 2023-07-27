using System.Collections;
using System.Diagnostics;
using System.Text;

namespace ShapeMaker;

public class Program {
    /*
        Timing (in seconds) from 14-inch 2023 MacBook Pro w/ 96GB 12-core M2 Max, .NET 7 in Release mode
        n=2, shapes: 1 time: 0.0011644, chiral shapes: 1 time: 0.0005432
        n=3, shapes: 2 time: 0.000195, chiral shapes: 2 time: 0.0003663
        n=4, shapes: 8 time: 0.0002425, chiral shapes: 7 time: 0.0003905
        n=5, shapes: 29 time: 0.0008399, chiral shapes: 23 time: 0.0004595
        n=6, shapes: 166 time: 0.0024831, chiral shapes: 112 time: 0.0273049
        n=7, shapes: 1,023 time: 0.0328246, chiral shapes: 607 time: 0.0157585
        n=8, shapes: 6,922 time: 0.0556222, chiral shapes: 3,811 time: 0.1486691
        n=9, shapes: 48,311 time: 0.1556955, chiral shapes: 25,413 time: 0.1916542
        n=10, shapes: 346,543 time: 1.2470705, chiral shapes: 178,083 time: 1.2792246
        n=11, shapes: 2,522,522 time: 10.8460798, chiral shapes: 1,279,537 time: 27.1576975
        n=12, shapes: 18,598,427 time: 93.9832502, chiral shapes: 9,371,094 time: 83.3083316
        n=13, shapes: 138,462,649 time: 1473.5459219, chiral shapes: 69,513,546 time: 873.6866129
        n=14, shapes: 1,039,496,297 time: 85387.0518438, chiral shapes: 520,878,101 time: 9882.1415169
     */

    // Potential Optimizations:
    // * early out of match rotation check (requires HashSet<Shape<bool>>)
    // * write out shapes to file as we add them to hashset

    // Potential Features:
    // * Make a 4-D version?
    // * Scale this out by making one file per {n},{w},{h},{d} combination. To run, we process
    //   all {n-1},{w},{h},{d} and write new unpadded shapes to {n},{w},{h},{d}, then we reprocess
    //   that list for the X paddings (left,right) of that to {n},{w+1},{h},{d} and so on. Ideally,
    //   we'd have a fast way to read/write hashsets, but also a way to easily read serially shape-
    //   by-shape for the input side of extending shapes. It might be easiest to store these sorted,
    //   use binary search to see if we already have that shape, if not add to a hashset, then when
    //   done, sort the hashset and merge into the sorted list and write that out.

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

        var shapes1 = new HashSet<BitArray>(BitArrayEqualityComparer.Instance) { BitShape.Deserialize("1,1,1,*") };
        SaveShapes(1, shapes1);

        { // scoping - warm up so timing is more stable
            Stopwatch sw = Stopwatch.StartNew();
            var warmupShapes = ShapesFromExtendingShapes(ShapesFromExtendingShapes(ShapesFromExtendingShapes(ShapesFromExtendingShapes(ShapesFromExtendingShapes(shapes1, sw), sw), sw), sw), sw);
            if (doChiral)
                ChiralShapes(warmupShapes, sw);
            sw.Stop();
            warmupShapes.Clear(); // just to be sure we release memory for this object
            warmupShapes = null; // just to be sure we release memory for this object
        }

        for (var n = 2; n < 15; n++) {
            { // scoping so shapes goes out of scope after we save it
                showOnCount = showEveryCount;
                Console.Write("n=" + n + ", shapes: ");
                Stopwatch sw = Stopwatch.StartNew();
                var shapes = ShapesFromExtendingShapes(LoadShapes(n - 1), sw);
                sw.Stop();
                Console.Write(shapes.Count.ToString("N0") + " time: " + sw.Elapsed.TotalSeconds);
                SaveShapes(n, shapes);
                shapes.Clear(); // just to be sure we release memory for this large object
                shapes = null; // just to be sure we release memory for this large object
            }

            if (doChiral) {
                showOnCount = showEveryCount;
                Console.Write(", chiral shapes: ");
                Stopwatch sw = Stopwatch.StartNew();
                var chiral = ChiralShapes(LoadShapes(n), sw);
                sw.Stop();
                Console.Write(chiral.Count.ToString("N0") + " time: " + sw.Elapsed.TotalSeconds);
                SaveChiralShapes(n, chiral);
                chiral.Clear(); // just to be sure we release memory for this large object
                chiral = null; // just to be sure we release memory for this large object
            }

            Console.WriteLine();
        }
    }

    private static void SaveShapes(int n, IEnumerable<BitArray> shapes) => SaveShapes("shapes-" + n + ".txt", shapes);

    private static void SaveChiralShapes(int n, IEnumerable<BitArray> shapes) => SaveShapes("chiral-shapes" + n + ".txt", shapes);

    private static void SaveShapes(string filename, IEnumerable<BitArray> shapes) {
        using (var fs = File.Create(filename))
        using (var sw = new StreamWriter(fs, Encoding.UTF8, 65536))
            foreach (var s in shapes) {
                sw.WriteLine(s.Serialize());
            }
    }

    private static IEnumerable<BitArray> LoadShapes(int n) {
        using (var fs = File.OpenRead("shapes-" + n + ".txt"))
        using (var sr = new StreamReader(fs, Encoding.UTF8, false, 65536))
            for (; ; ) {
                var l = sr.ReadLine();
                if (l == null) break;
                yield return BitShape.Deserialize(l);
            }
    }

    // for each shape in parallel, try to add cube to it
    // first does by adding cube to the shape in its current size
    // then tries padding each of the 6 faces of the shape and adding a cube there
    public static HashSet<BitArray> ShapesFromExtendingShapes(IEnumerable<BitArray> shapes, Stopwatch sw) {
        var newShapes = new HashSet<BitArray>(BitArrayEqualityComparer.Instance);

        Parallel.ForEach(shapes, (shape) => {
            var (w, h, d) = shape.Dimensions();

            AddShapes(newShapes, sw, shape, 0, w, 0, h, 0, d); // unpadded
            AddShapes(newShapes, sw, shape.PadLeft(), 0, 1, 0, h, 0, d);
            AddShapes(newShapes, sw, shape.PadRight(), w, w + 1, 0, h, 0, d);
            AddShapes(newShapes, sw, shape.PadTop(), 0, w, 0, 1, 0, d);
            AddShapes(newShapes, sw, shape.PadBottom(), 0, w, h, h + 1, 0, d);
            AddShapes(newShapes, sw, shape.PadFront(), 0, w, 0, h, 0, 1);
            AddShapes(newShapes, sw, shape.PadBack(), 0, w, 0, h, d, d + 1);
        });

        return newShapes;
    }

    // for each blank cube from x0 to w, y0 to h, z0 to d, if it has an adjacent neighbor
    // add that cube, find the minimum rotation, and add to the newShapes hash set (under
    // lock since we could be doing this in parallel.)
    private static void AddShapes(HashSet<BitArray> newShapes, Stopwatch sw, BitArray shape, int x0, int w, int y0, int h, int z0, int d) {
        for (var x = x0; x < w; x++)
            for (var y = y0; y < h; y++)
                for (var z = z0; z < d; z++)
                    if (!shape.Get(x,y,z))
                        if (HasSetNeighbor(shape, x, y, z)) {
                            var newShape = new BitArray(shape);
                            newShape.Set(x, y, z, true);
                            var s = newShape.MinRotation();
                            lock (newShapes) {
                                int oldCount = newShapes.Count;
                                newShapes.Add(s);
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
    public static HashSet<BitArray> ChiralShapes(IEnumerable<BitArray> shapes, Stopwatch sw) {
        var newShapes = new HashSet<BitArray>(BitArrayEqualityComparer.Instance);

        Parallel.ForEach(shapes, (shape) => {
            var newShape = new BitArray(shape).MinChiralRotation();
            lock (newShapes) {
                int oldCount = newShapes.Count;
                newShapes.Add(newShape);
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

    private static bool HasSetNeighbor(BitArray shape, int x, int y, int z) {
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

public class BitArrayEqualityComparer : IEqualityComparer<BitArray> {
    public static readonly BitArrayEqualityComparer Instance = new BitArrayEqualityComparer();

    bool IEqualityComparer<BitArray>.Equals(BitArray? x, BitArray? y) {
        return x is not null && y is not null && x.CompareTo(y) == 0;
    }

    int IEqualityComparer<BitArray>.GetHashCode(BitArray obj) {
        int hashCode = obj.Length;
        for(int i=0; i<obj.Length;i++) {
            hashCode = hashCode * 3 + (obj[i] ? 1 : 0);
        }
        return hashCode;
    }
}