using System.Diagnostics;
using System.Text;

namespace ShapeMaker;

public class Program {
    /*
        Timing (in seconds) from 14-inch 2023 MacBook Pro w/ 96GB 12-core M2 Max, .NET 7 in Release mode
        n=2, shapes: 1 time: 0.0018114, chiral shapes: 1 time: 0.0257625
        n=3, shapes: 2 time: 0.0003327, chiral shapes: 2 time: 0.0002263
        n=4, shapes: 8 time: 0.0003637, chiral shapes: 7 time: 0.0003806
        n=5, shapes: 29 time: 0.0004629, chiral shapes: 23 time: 0.0253754
        n=6, shapes: 166 time: 0.0017314, chiral shapes: 112 time: 0.0260918
        n=7, shapes: 1,023 time: 0.0056179, chiral shapes: 607 time: 0.031555
        n=8, shapes: 6,922 time: 0.0577995, chiral shapes: 3,811 time: 0.0685468
        n=9, shapes: 48,311 time: 0.1662816, chiral shapes: 25,413 time: 0.3171091
        n=10, shapes: 346,543 time: 0.9449985, chiral shapes: 178,083 time: 0.8792436
        n=11, shapes: 2,522,522 time: 8.5114444, chiral shapes: 1,279,537 time: 7.25399
        n=12, shapes: 18,598,427 time: 81.1195411, chiral shapes: 9,371,094 time: 72.0814029
        n=13, shapes: 138,462,649 time: 1534.5545362, chiral shapes: 69,513,546 time: 1276.3501387
        n=14, shapes: 1,039,496,297 time: a bit over a day
     */

    // Potential Optimizations:
    // * early out of match rotation check (requires HashSet<Shape<bool>>)

    // Potential Features:
    // * Make a 4-D version?

    static int showOnCount = 0; // used to show progress
    const int showEveryCount = 1_000_000;

    static void Main(string[] args) {
        bool doChiral = true;

        var shapes1 = new HashSet<byte[]>() { Encoding.UTF8.GetBytes("1,1,1,1") };
        SaveShapes(1, shapes1);

        { // scoping - warm up so timing is more stable
            Stopwatch sw = Stopwatch.StartNew();
            var warmupShapes = ShapesFromExtendingShapes(ShapesFromExtendingShapes(ShapesFromExtendingShapes(ShapesFromExtendingShapes(ShapesFromExtendingShapes(shapes1, sw), sw), sw), sw), sw);
            if (doChiral)
                ChiralShapes(warmupShapes, sw);
            sw.Stop();
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
            }

            if (doChiral) {
                showOnCount = showEveryCount;
                Console.Write(", chiral shapes: ");
                Stopwatch sw = Stopwatch.StartNew();
                var chiral = ChiralShapes(LoadShapes(n), sw);
                sw.Stop();
                Console.Write(chiral.Count.ToString("N0") + " time: " + sw.Elapsed.TotalSeconds);
                SaveChiralShapes(n, chiral);
            }

            Console.WriteLine();
        }
    }

    private static void SaveShapes(int n, IEnumerable<byte[]> shapes) => SaveShapes("shapes-" + n + ".txt", shapes);

    private static void SaveChiralShapes(int n, IEnumerable<byte[]> shapes) => SaveShapes("chiral-shapes" + n + ".txt", shapes);

    private static void SaveShapes(string filename, IEnumerable<byte[]> shapes) {
        var nl = Encoding.UTF8.GetBytes(Environment.NewLine);
        using (var fs = File.Create(filename))
            foreach (var s in shapes) {
                fs.Write(s); fs.Write(nl);
            }
    }

    private static IEnumerable<byte[]> LoadShapes(int n) {
        using (var fs = File.OpenRead("shapes-" + n + ".txt"))
        using (var sr = new StreamReader(fs, Encoding.UTF8, false, 65536))
            for (; ; ) {
                var l = sr.ReadLine();
                if (l == null) break;
                yield return Encoding.UTF8.GetBytes(l);
            }
    }

    // for each shape in parallel, try to add cube to it
    // first does by adding cube to the shape in its current size
    // then tries padding each of the 6 faces of the shape and adding a cube there
    public static HashSet<byte[]> ShapesFromExtendingShapes(IEnumerable<byte[]> shapes, Stopwatch sw) {
        var newShapes = new HashSet<byte[]>(ByteArrayEqualityComparer.Instance);

        Parallel.ForEach(shapes, (shape) => {
            var baseShape = DeserializeShape(shape);
            int w = baseShape.w, h = baseShape.h, d = baseShape.d;

            AddShapes(newShapes, sw, baseShape, 0, w, 0, h, 0, d); // unpadded
            AddShapes(newShapes, sw, baseShape.PadLeft(), 0, 1, 0, h, 0, d);
            AddShapes(newShapes, sw, baseShape.PadRight(), w, w + 1, 0, h, 0, d);
            AddShapes(newShapes, sw, baseShape.PadTop(), 0, w, 0, 1, 0, d);
            AddShapes(newShapes, sw, baseShape.PadBottom(), 0, w, h, h + 1, 0, d);
            AddShapes(newShapes, sw, baseShape.PadFront(), 0, w, 0, h, 0, 1);
            AddShapes(newShapes, sw, baseShape.PadBack(), 0, w, 0, h, d, d + 1);
        });

        return newShapes;
    }

    // for each blank cube from x0 to w, y0 to h, z0 to d, if it has an adjacent neighbor
    // add that cube, find the minimum rotation, and add to the newShapes hash set (under
    // lock since we could be doing this in parallel.)
    private static void AddShapes(HashSet<byte[]> newShapes, Stopwatch sw, Shape<bool> shape, int x0, int w, int y0, int h, int z0, int d) {
        for (var x = x0; x < w; x++)
            for (var y = y0; y < h; y++)
                for (var z = z0; z < d; z++)
                    if (!shape.shape[x, y, z])
                        if (HasSetNeighbor(shape, x, y, z)) {
                            var newShape = new Shape<bool>(shape);
                            newShape.shape[x, y, z] = true;
                            var s = SerializeShape(newShape.MinRotation());
                            lock (newShapes) {
                                int oldCount = newShapes.Count;
                                newShapes.Add(s);
                                bool showAndNext = newShapes.Count >= showOnCount && newShapes.Count != oldCount;
                                if (showAndNext) {
                                    var ss = "[" + showOnCount / 1_000_000 + "m " + sw.Elapsed.TotalSeconds.ToString("G0") + "s]";
                                    Console.Write(ss + new string('\b', ss.Length));
                                    showOnCount += showEveryCount;
                                }
                            }
                        }
    }

    // for each shape in parallel, get its minimum chiral rotation and add to
    // newShapes hash set under lock
    public static HashSet<byte[]> ChiralShapes(IEnumerable<byte[]> shapes, Stopwatch sw) {
        var newShapes = new HashSet<byte[]>(ByteArrayEqualityComparer.Instance);

        Parallel.ForEach(shapes, (shapeString) => {
            var newShapeString = SerializeShape(DeserializeShape(shapeString).MinChiralRotation());
            lock (newShapes) {
                int oldCount = newShapes.Count;
                newShapes.Add(newShapeString);
                bool showAndNext = newShapes.Count >= showOnCount && newShapes.Count != oldCount;
                if (showAndNext) {
                    var ss = "[" + showOnCount / 1_000_000 + "m " + sw.Elapsed.TotalSeconds.ToString("G0") + "s]";
                    Console.Write(ss + new string('\b', ss.Length));
                    showOnCount += showEveryCount;
                }
            }
        });

        return newShapes;
    }

    private static bool HasSetNeighbor(Shape<bool> shape, int x, int y, int z) {
        int w = shape.w, h = shape.h, d = shape.d;
        var s = shape.shape;
        // minor opt: we do easier comparisons first with short-circuiting
        return (x > 0 && s[x - 1, y, z]) ||
            (y > 0 && s[x, y - 1, z]) ||
            (z > 0 && s[x, y, z - 1]) ||
            (x + 1 < w && s[x + 1, y, z]) ||
            (y + 1 < h && s[x, y + 1, z]) ||
            (z + 1 < d && s[x, y, z + 1]);
    }

    private static Shape<bool> DeserializeShape(byte[] serializedShape) {
        int i = 0;
        for (int c = 0; c < 3;)
            if (serializedShape[i++] == (byte)',')
                c++;
        var splitS = Encoding.UTF8.GetString(serializedShape, 0, i).Split(',');
        if (splitS.Length != 4) throw new ArgumentException("serialized string must have 4 comma separated components");
        int w = int.Parse(splitS[0]), h = int.Parse(splitS[1]), d = int.Parse(splitS[2]);
        var shape = new Shape<bool>(w, h, d);
        var shapeShape = shape.shape;
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                for (int z = 0; z < d; z++)
                    shapeShape[x, y, z] = serializedShape[i++] == '1';
        return shape;
    }

    private static byte[] SerializeShape(Shape<bool> shape) {
        int w = shape.w, h = shape.h, d = shape.d;
        var s = shape.shape;
        var dim = Encoding.UTF8.GetBytes(w + "," + h + "," + d + ",");
        int i = dim.Length;
        var ba = new byte[w * h * d + i];
        Array.Copy(dim, ba, i);
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                for (int z = 0; z < d; z++)
                    ba[i++] = (byte)(s[x, y, z] ? '1' : '0');
        return ba;
    }
}

public class ShapeBoolEqualityComparer : IEqualityComparer<Shape<bool>> {
    public static readonly ShapeBoolEqualityComparer Instance = new ShapeBoolEqualityComparer();

    bool IEqualityComparer<Shape<bool>>.Equals(Shape<bool>? x, Shape<bool>? y) {
        return x is not null && y is not null && x.CompareTo(y) == 0;
    }

    int IEqualityComparer<Shape<bool>>.GetHashCode(Shape<bool> obj) {
        int w = obj.w, h = obj.h, d = obj.d;
        var s = obj.shape;
        int hashCode = w * 37 * 37 + h * 37 + d;
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                for (int z = 0; z < d; z++)
                    hashCode = hashCode * 3 + (s[x, y, z] ? 1 : 0);
        return hashCode;
    }
}

public class ByteArrayEqualityComparer : IEqualityComparer<byte[]> {
    public static readonly ByteArrayEqualityComparer Instance = new ByteArrayEqualityComparer();

    bool IEqualityComparer<byte[]>.Equals(byte[]? x, byte[]? y) {
        return x is not null && y is not null && x.SequenceEqual(y);
    }

    int IEqualityComparer<byte[]>.GetHashCode(byte[] obj) {
        int hashCode = obj.Length;
        for (int i = 0; i < obj.Length; i++)
            hashCode = hashCode * 37 + obj[i];
        return hashCode;
    }
}