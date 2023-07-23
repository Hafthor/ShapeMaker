using System.Diagnostics;

namespace ShapeMaker;

public class Program {
    /*
        Timing (in seconds) from 14-inch 2023 MacBook Pro w/ 96GB 12-core M2 Max, .NET 7 in Release mode
        n=2, shapes: 1 time: 3.08E-05, chiral shapes: 1 time: 0.0001811
        n=3, shapes: 2 time: 4.81E-05, chiral shapes: 2 time: 0.0001515
        n=4, shapes: 8 time: 0.0002062, chiral shapes: 7 time: 0.0001959
        n=5, shapes: 29 time: 0.0003474, chiral shapes: 23 time: 0.0256058
        n=6, shapes: 166 time: 0.0038547, chiral shapes: 112 time: 0.0785211
        n=7, shapes: 1,023 time: 0.0154454, chiral shapes: 607 time: 0.0349585
        n=8, shapes: 6,922 time: 0.0393247, chiral shapes: 3,811 time: 0.0649957
        n=9, shapes: 48,311 time: 0.2387795, chiral shapes: 25,413 time: 0.1686733
        n=10, shapes: 346,543 time: 0.8810059, chiral shapes: 178,083 time: 0.8175648
        n=11, shapes: 2,522,522 time: 7.621756, chiral shapes: 1,279,537 time: 6.8102416
        n=12, shapes: 18,598,427 time: 77.0081817, chiral shapes: 9,371,094 time: 91.3158117
        n=13, shapes: 138,462,649 time: 1368.929878, chiral shapes: 69,513,546 time: 2759.5726144
        n=14, shapes: 1,039,496,297 time: a bit over a day
     */

    // Potential Optimizations:
    // * early out of match rotation check (requires HashSet<Shape<bool>>)
    // * save shapes to file, stream from file to do chiral and next shapes

    // Potential Features:
    // * Make a 4-D version?

    static int showOnCount = 0; // used to show progress
    const int showEveryCount = 10000;

    static void Main(string[] args) {
        bool doChiral = true;

        var shapes = new HashSet<string>() { "1,1,1,1" };
        Console.WriteLine("shapes1: " + shapes.Count);

        // warm up so timing is more stable
        var warmupShapes = ShapesFromExtendingShapes(ShapesFromExtendingShapes(ShapesFromExtendingShapes(ShapesFromExtendingShapes(ShapesFromExtendingShapes(shapes)))));
        if (doChiral) {
            ChiralShapes(warmupShapes);
        }

        for (var n = 2; n < 15; n++) {
            showOnCount = showEveryCount;
            Stopwatch sw = Stopwatch.StartNew();
            shapes = ShapesFromExtendingShapes(shapes);
            sw.Stop();
            Console.Write("n=" + n + ", shapes: " + shapes.Count.ToString("N0") + " time: " + sw.Elapsed.TotalSeconds);

            if (doChiral) {
                showOnCount = showEveryCount;
                sw = Stopwatch.StartNew();
                var chiral = ChiralShapes(shapes);
                sw.Stop();
                Console.WriteLine(", chiral shapes: " + chiral.Count.ToString("N0") + " time: " + sw.Elapsed.TotalSeconds);
            } else {
                Console.WriteLine();
            }
        }
    }

    // for each shape in parallel, try to add cube to it
    // first does by adding cube to the shape in its current size
    // then tries padding each of the 6 faces of the shape and adding a cube there
    public static HashSet<string> ShapesFromExtendingShapes(IEnumerable<string> shapes) {
        var newShapes = new HashSet<string>();

        Parallel.ForEach(shapes, (shape) => {
            var baseShape = DeserializeShape(shape);
            int w = baseShape.w, h = baseShape.h, d = baseShape.d;

            AddShapes(newShapes, baseShape, 0, w, 0, h, 0, d); // unpadded
            AddShapes(newShapes, baseShape.PadLeft(), 0, 1, 0, h, 0, d);
            AddShapes(newShapes, baseShape.PadRight(), w, w + 1, 0, h, 0, d);
            AddShapes(newShapes, baseShape.PadTop(), 0, w, 0, 1, 0, d);
            AddShapes(newShapes, baseShape.PadBottom(), 0, w, h, h + 1, 0, d);
            AddShapes(newShapes, baseShape.PadFront(), 0, w, 0, h, 0, 1);
            AddShapes(newShapes, baseShape.PadBack(), 0, w, 0, h, d, d + 1);
        });

        return newShapes;
    }

    // for each blank cube from x0 to w, y0 to h, z0 to d, if it has an adjacent neighbor
    // add that cube, find the minimum rotation, and add to the newShapes hash set (under
    // lock since we could be doing this in parallel.)
    private static void AddShapes(HashSet<string> newShapes, Shape<bool> shape, int x0, int w, int y0, int h, int z0, int d) {
        for (var x = x0; x < w; x++)
            for (var y = y0; y < h; y++)
                for (var z = z0; z < d; z++)
                    if (!shape.shape[x, y, z])
                        if (HasSetNeighbor(shape, x, y, z)) {
                            var newShape = new Shape<bool>(shape);
                            newShape.shape[x, y, z] = true;
                            var s = SerializeShape(newShape.MinRotation());
                            bool showAndNext;
                            lock (newShapes) {
                                int oldCount = newShapes.Count;
                                newShapes.Add(s);
                                showAndNext = newShapes.Count == showOnCount && newShapes.Count != oldCount;
                            }
                            if (showAndNext) {
                                var ss = "[" + showOnCount.ToString("N0") + "]";
                                Console.Write(ss + new string('\b', ss.Length));
                                showOnCount += showEveryCount;
                            }
                        }
    }

    // for each shape in parallel, get its minimum chiral rotation and add to
    // newShapes hash set under lock
    public static HashSet<string> ChiralShapes(IEnumerable<string> shapes) {
        var newShapes = new HashSet<string>();

        Parallel.ForEach(shapes, (shapeString) => {
            var newShapeString = SerializeShape(DeserializeShape(shapeString).MinChiralRotation());
            bool showAndNext;
            lock (newShapes) {
                int oldCount = newShapes.Count;
                newShapes.Add(newShapeString);
                showAndNext = newShapes.Count == showOnCount && newShapes.Count != oldCount;
            }
            if (showAndNext) {
                var ss = "[" + showOnCount.ToString("N0") + "]";
                Console.Write(ss + new string('\b', ss.Length));
                showOnCount += showEveryCount;
            }
        });

        return newShapes;
    }

    private static Shape<bool> DeserializeShape(string serializedShape) {
        var splitS = serializedShape.Split(',');
        if (splitS.Length != 4) throw new ArgumentException("serialized string must have 4 comma separated components");
        int w = int.Parse(splitS[0]), h = int.Parse(splitS[1]), d = int.Parse(splitS[2]);
        var s3 = splitS[3];
        var shape = new Shape<bool>(w, h, d);
        var shapeShape = shape.shape;
        for (int x = 0, i = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                for (int z = 0; z < d; z++)
                    shapeShape[x, y, z] = s3[i++] == '1';
        return shape;
    }

    private static string SerializeShape(Shape<bool> shape) {
        int w = shape.w, h = shape.h, d = shape.d;
        var s = shape.shape;
        var ca = new char[w * h * d];
        for (int x = 0, i = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                for (int z = 0; z < d; z++)
                    ca[i++] = s[x, y, z] ? '1' : '0';
        return w + "," + h + "," + d + "," + new string(ca);
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