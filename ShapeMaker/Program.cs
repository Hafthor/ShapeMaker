using System.Diagnostics;

namespace ShapeMaker;

public class Program {
    /*
        Timing (in seconds) from 14-inch 2023 MacBook Pro w/ 12-core M2 Max, .NET 7 in Release mode
        n=2, shapes: 1 time: 5.07E-05, chiral shapes: 1 time: 4.83E-05
        n=3, shapes: 2 time: 6.3E-05, chiral shapes: 2 time: 0.0002505
        n=4, shapes: 8 time: 0.0002665, chiral shapes: 7 time: 0.0001047
        n=5, shapes: 29 time: 0.0003285, chiral shapes: 23 time: 0.0255475
        n=6, shapes: 166 time: 0.0013086, chiral shapes: 112 time: 0.0046102
        n=7, shapes: 1,023 time: 0.0070336, chiral shapes: 607 time: 0.0363258
        n=8, shapes: 6,922 time: 0.0344915, chiral shapes: 3,811 time: 0.0738864
        n=9, shapes: 48,311 time: 0.1374268, chiral shapes: 25,413 time: 0.2044382
        n=10, shapes: 346,543 time: 0.9377152, chiral shapes: 178,083 time: 1.437922
        n=11, shapes: 2,522,522 time: 8.0277754, chiral shapes: 1,279,537 time: 15.334365
        n=12, shapes: 18,598,427 time: a minute and a half
        n=13, shapes: 138,462,649 time: about a half an hour
        n=14, shapes: 1,039,496,297 time: a bit over a day
     */

    // Potential Optimizations:
    // * test adding cube inside existing shape unpadded and then test adding pad on one side at a time
    // * optimize 24 rotations for non-uniform dimensional shapes
    // * use BitArray to store shape?
    // * use record and hashset on that
    // * early out of match rotation check

    // Potential Features:
    // * Make a 4-D version?

    static void Main(string[] args) {
        var shapes = new HashSet<string>() { "1,1,1,1" };
        Console.WriteLine("shapes1: " + shapes.Count);
        bool doChiral = true;

        // warm up so timing is more stable
        var warmupShapes = ShapesFromExtendingShapes(ShapesFromExtendingShapes(ShapesFromExtendingShapes(ShapesFromExtendingShapes(ShapesFromExtendingShapes(shapes)))));
        if (doChiral) {
            ChiralShapes(warmupShapes);
        }

        for (var n = 2; n < 15; n++) {
            Stopwatch sw = Stopwatch.StartNew();
            shapes = ShapesFromExtendingShapes(shapes);
            sw.Stop();
            Console.Write("n=" + n + ", shapes: " + shapes.Count.ToString("N0") + " time: " + sw.Elapsed.TotalSeconds);

            if (doChiral) {
                sw = Stopwatch.StartNew();
                var chiral = ChiralShapes(shapes);
                sw.Stop();
                Console.WriteLine(", chiral shapes: " + chiral.Count.ToString("N0") + " time: " + sw.Elapsed.TotalSeconds);
            } else {
                Console.WriteLine();
            }
        }
    }

    private static void AddShapes(HashSet<string> newShapes, Shape shape, int x0, int w, int y0, int h, int z0, int d) {
        for (var x = x0; x < w; x++)
            for (var y = y0; y < h; y++)
                for (var z = z0; z < d; z++)
                    if (!shape.Is(x, y, z))
                        if (shape.HasSetNeighbor(x, y, z)) {
                            shape.Set(x, y, z, true);
                            var s = shape.MinRotation().ToString();
                            lock (newShapes) {
                                newShapes.Add(s);
                            }
                            shape.Set(x, y, z, false); // restore
                        }
    }

    private static HashSet<string> ShapesFromExtendingShapes(IEnumerable<string> shapes) {
        var newShapes = new HashSet<string>();

        Parallel.ForEach(shapes, (shape) => {
            var baseShape = new Shape(shape);
            var (w, h, d) = baseShape.Dimensions();

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

    private static HashSet<string> ChiralShapes(IEnumerable<string> shapes) {
        var newShapes = new HashSet<string>();

        Parallel.ForEach(shapes, (shape) => {
            var baseShape = new Shape(shape).MinChiralRotation().ToString();
            lock (newShapes) newShapes.Add(baseShape);
        });

        return newShapes;
    }
}

public class ShapeComparer : IEqualityComparer<Shape> {
    public static ShapeComparer Instance = new ShapeComparer();

    bool IEqualityComparer<Shape>.Equals(Shape? x, Shape? y) {
        return x != null && y != null && x.CompareTo(y) == 0;
    }

    int IEqualityComparer<Shape>.GetHashCode(Shape obj) {
        return obj.GetHashCode();
    }
}

public class Shape {
    private readonly bool[,,] shape;

    public override int GetHashCode() {
        var hash = 0;
        var (w, h, d) = Dimensions();
        for (var x = 0; x < w; x++)
            for (var y = 0; y < h; y++)
                for (var z = 0; z < d; z++)
                    hash = hash * 3 + (shape[x, y, z] ? 1 : 0);
        return hash;
    }

    public Shape(int w, int h, int d) {
        shape = new bool[w, h, d];
    }

    public Shape(int w, int h, int d, string s) {
        shape = new bool[w, h, d];
        Parse(s);
    }

    public Shape(string s) {
        var ss = s.Split(',');
        if (ss.Length != 4)
            throw new ArgumentException("String must have 4 components separated by commas");

        int w = int.Parse(ss[0]), h = int.Parse(ss[1]), d = int.Parse(ss[2]);
        shape = new bool[w, h, d];
        Parse(ss[3]);
    }

    public bool Is(int x, int y, int z) => shape[x, y, z];

    public bool HasSetNeighbor(int x, int y, int z) {
        var (w, h, d) = Dimensions();

        return (x > 0 && shape[x - 1, y, z]) ||
            (y > 0 && shape[x, y - 1, z]) ||
            (z > 0 && shape[x, y, z - 1]) ||
            (x + 1 < w && shape[x + 1, y, z]) ||
            (y + 1 < h && shape[x, y + 1, z]) ||
            (z + 1 < d && shape[x, y, z + 1]);
    }

    public void Set(int x, int y, int z, bool v) => shape[x, y, z] = v;

    private void Parse(string s) {
        var (w, h, d) = Dimensions();
        if (s.Length != w * h * d)
            throw new ArgumentException("String must be of length w*h*d");

        var i = 0;
        for (var x = 0; x < w; x++)
            for (var y = 0; y < h; y++)
                for (var z = 0; z < d; z++)
                    shape[x, y, z] = s[i++] == '1';
    }

    public (int w, int h, int d) Dimensions() {
        return (shape.GetLength(0), shape.GetLength(1), shape.GetLength(2));
    }

    public char[] ShapeChars() {
        var (w, h, d) = Dimensions();
        var s = new char[w * h * d];

        var i = 0;
        for (var x = 0; x < w; x++)
            for (var y = 0; y < h; y++)
                for (var z = 0; z < d; z++)
                    s[i++] = shape[x, y, z] ? '1' : '0';

        return s;
    }

    public string ShapeString() => new string(ShapeChars());

    public override string ToString() {
        var (w, h, d) = Dimensions();
        var s = ShapeString();
        return w + "," + h + "," + d + "," + s;
    }

    // adds an empty layer around shape
    public Shape Pad() {
        var (w, h, d) = Dimensions();
        return ShapeCopy(new Shape(w + 2, h + 2, d + 2), 1, w, 1, h, 1, d);
    }

    public Shape PadLeft() {
        var (w, h, d) = Dimensions();
        return ShapeCopy(new Shape(w + 1, h, d), 1, w, 0, h, 0, d);
    }

    public Shape PadRight() {
        var (w, h, d) = Dimensions();
        return ShapeCopy(new Shape(w + 1, h, d), 0, w, 0, h, 0, d);
    }

    public Shape PadTop() {
        var (w, h, d) = Dimensions();
        return ShapeCopy(new Shape(w, h + 1, d), 0, w, 1, h, 0, d);
    }

    public Shape PadBottom() {
        var (w, h, d) = Dimensions();
        return ShapeCopy(new Shape(w, h + 1, d), 0, w, 0, h, 0, d);
    }

    public Shape PadFront() {
        var (w, h, d) = Dimensions();
        return ShapeCopy(new Shape(w, h, d + 1), 0, w, 0, h, 1, d);
    }

    public Shape PadBack() {
        var (w, h, d) = Dimensions();
        return ShapeCopy(new Shape(w, h, d + 1), 0, w, 0, h, 0, d);
    }

    public Shape ShapeCopy(Shape to, int xa, int w, int ya, int h, int za, int d) {
        for (var x = 0; x < w; x++)
            for (var y = 0; y < h; y++)
                for (var z = 0; z < d; z++)
                    to.shape[x + xa, y + ya, z + za] = shape[x, y, z];
        return to;
    }

    // returns the shape in the smallest dimensions that it will fit in
    public Shape Trim() {
        var (w, h, d) = Dimensions();

        int lx = int.MaxValue, mx = int.MinValue;
        int ly = int.MaxValue, my = int.MinValue;
        int lz = int.MaxValue, mz = int.MinValue;

        for (var x = 0; x < w; x++)
            for (var y = 0; y < h; y++)
                for (var z = 0; z < d; z++)
                    if (shape[x, y, z]) {
                        if (x < lx) lx = x; else if (x > mx) mx = x;
                        if (y < ly) ly = y; else if (y > my) my = y;
                        if (z < lz) lz = z; else if (z > mz) mz = z;
                    }

        int nw = mx - lx + 1, nh = my - ly + 1, nd = mz - lz + 1;
        var newShape = new Shape(nw, nh, nd);
        for (var x = 0; x < nw; x++)
            for (var y = 0; y < nh; y++)
                for (var z = 0; z < nd; z++)
                    newShape.shape[x, y, z] = shape[lx + x, ly + y, lz + z];

        return newShape;
    }

    // returns shape rotated clockwise on X axis by 90 degrees
    public Shape RotateX() {
        var (w, h, d) = Dimensions();

        var newShape = new Shape(w, d, h);
        for (var x = 0; x < w; x++)
            for (var y = 0; y < h; y++)
                for (var z = 0; z < d; z++)
                    newShape.shape[x, z, y] = shape[x, y, d - z - 1];

        return newShape;
    }

    public Shape RotateX(int n) {
        n = ((n % 4) + 4) % 4;
        var newShape = this;
        while (n-- > 0)
            newShape = newShape.RotateX();
        return newShape;
    }

    // mirrors in-place
    public Shape MirrorX() {
        var (w, h, d) = Dimensions();
        for (var x = 0; x < w / 2; x++)
            for (var y = 0; y < h; y++)
                for (var z = 0; z < d; z++)
                    (shape[x, y, z], shape[w - x - 1, y, z]) = (shape[w - x - 1, y, z], shape[x, y, z]);
        return this;
    }

    // returns shape rotated clockwise on Y axis by 90 degrees
    public Shape RotateY() {
        var (w, h, d) = Dimensions();

        var newShape = new Shape(d, h, w);
        for (var x = 0; x < w; x++)
            for (var y = 0; y < h; y++)
                for (var z = 0; z < d; z++)
                    newShape.shape[z, y, x] = shape[x, y, d - z - 1];

        return newShape;
    }

    public Shape RotateY(int n) {
        n = ((n % 4) + 4) % 4;
        var newShape = this;
        while (n-- > 0)
            newShape = newShape.RotateY();
        return newShape;
    }

    // mirrors in-place
    public Shape MirrorY() {
        var (w, h, d) = Dimensions();
        for (var x = 0; x < w; x++)
            for (var y = 0; y < h / 2; y++)
                for (var z = 0; z < d; z++)
                    (shape[x, y, z], shape[x, h - y - 1, z]) = (shape[x, h - y - 1, z], shape[x, y, z]);
        return this;
    }

    // returns shape rotated clockwise on Z axis by 90 degrees
    public Shape RotateZ() {
        var (w, h, d) = Dimensions();

        var newShape = new Shape(h, w, d);
        for (var x = 0; x < w; x++)
            for (var y = 0; y < h; y++)
                for (var z = 0; z < d; z++)
                    newShape.shape[y, x, z] = shape[x, h - y - 1, z];

        return newShape;
    }

    public Shape RotateZ(int n) {
        n = ((n % 4) + 4) % 4;
        var newShape = this;
        while (n-- > 0)
            newShape = newShape.RotateZ();
        return newShape;
    }

    // mirrors in-place
    public Shape MirrorZ() {
        var (w, h, d) = Dimensions();
        for (var x = 0; x < w; x++)
            for (var y = 0; y < h; y++)
                for (var z = 0; z < d; z++)
                    (shape[x, y, z], shape[x, y, d - z - 1]) = (shape[x, y, d - z - 1], shape[x, y, z]);
        return this;
    }

    // returns the shape rotated such that the dimensions are narrowest in the X and broadest in the Z
    public Shape MinDimension() {
        var (w, h, d) = Dimensions();

        if (w <= h && h <= d)
            return this;

        // w,h,d possibilities
        // 1,2,3 => fine
        // 1,3,2 => swap h,d
        // 2,1,3 => swap w,h
        // 2,3,1 => swap h,d swap w,h
        // 3,1,2 => swap w,h swap h,d
        // 3,2,1 => swap w,d

        // we need to rotate
        if (w <= h && w <= d && h > d)
            return RotateX(); // swaps h,d
        if (w <= d && h <= d && w > h)
            return RotateY(); // swaps w,h
        if (w > h && h > d)
            return RotateZ(); // swaps w,d
        if (h > w && w > d)
            return RotateX().RotateY();
        if (w > h && w > d && h <= d)
            return RotateY().RotateX();

        throw new ApplicationException("not sure how we got here");
    }

    private IEnumerable<Shape> AllRotations() {
        var shape00 = this;
        yield return shape00;
        var shape01 = shape00.RotateX();
        yield return shape01;
        var shape02 = shape01.RotateX();
        yield return shape02;
        var shape03 = shape02.RotateX();
        yield return shape03;

        var shape10 = shape00.RotateY();
        yield return shape10;
        var shape11 = shape10.RotateX();
        yield return shape11;
        var shape12 = shape11.RotateX();
        yield return shape12;
        var shape13 = shape12.RotateX();
        yield return shape13;

        var shape20 = shape10.RotateY();
        yield return shape20;
        var shape21 = shape20.RotateX();
        yield return shape21;
        var shape22 = shape21.RotateX();
        yield return shape22;
        var shape23 = shape22.RotateX();
        yield return shape23;

        var shape30 = shape20.RotateY();
        yield return shape30;
        var shape31 = shape30.RotateX();
        yield return shape31;
        var shape32 = shape31.RotateX();
        yield return shape32;
        var shape33 = shape32.RotateX();
        yield return shape33;

        var shape40 = shape00.RotateZ();
        yield return shape40;
        var shape41 = shape40.RotateX();
        yield return shape41;
        var shape42 = shape41.RotateX();
        yield return shape42;
        var shape43 = shape42.RotateX();
        yield return shape43;

        var shape50 = shape40.RotateZ().RotateZ();
        yield return shape50;
        var shape51 = shape50.RotateX();
        yield return shape51;
        var shape52 = shape51.RotateX();
        yield return shape52;
        var shape53 = shape52.RotateX();
        yield return shape53;
    }

    // returns the shape rotated such that it is the mimimum serialization of the shape
    public Shape MinRotation() {
        //var shape = MinDimension();
        //var (w, h, d) = shape.Dimensions();
        // a rubiks cube, for example, is a 3x3x3 shape
        // it can be resting on any of its six colors
        // and could have any of four sides facing you. 6*4=24

        // if the shape is dimensionally uniform
        // we will need to apply rotations to get all 24 combinations
        // and see which one serializes lowest

        Shape best = null;
        foreach (var s in AllRotations())
            if (best == null || best.CompareTo(s) > 0)
                best = s;
        return best;

        // if the shape is not dimensionally uniform, this could be faster
        // if the shape is 3x2x1, for example
        // we can only try 180º rotations

        // for now, we will just do all 24 rotations and let the .Min figure it out
    }

    public IEnumerable<Shape> AllMirrors() {
        // gray code 000, 001, 011, 010, 110, 111, 101, 100
        //               x    y    x    z    x    y    x
        var shape = this;
        yield return shape;
        shape.MirrorX();
        yield return shape;
        shape.MirrorY();
        yield return shape;
        shape.MirrorX();
        yield return shape;
        shape.MirrorZ();
        yield return shape;
        shape.MirrorX();
        yield return shape;
        shape.MirrorY();
        yield return shape;
        shape.MirrorX();
        yield return shape;
    }

    public Shape MinChiralRotation() {
        // check all rotations for each of mirrored possibilities (8)
        Shape best = null;
        foreach (var s in AllMirrors())
            foreach (var ss in s.AllRotations())
                if (best == null || best.CompareTo(ss) > 0)
                    best = ss;
        return best;
    }

    // returns <0 if this is lower than other, 0 if equal, >0 if other is lower
    public int CompareTo(Shape other) {
        var dw = this.shape.GetLength(0) - other.shape.GetLength(0);
        if (dw != 0) return dw;

        var dh = this.shape.GetLength(1) - other.shape.GetLength(1);
        if (dh != 0) return dh;

        var dd = this.shape.GetLength(2) - other.shape.GetLength(2);
        if (dd != 0) return dd;

        var (w, h, d) = Dimensions();
        for (var x = 0; x < w; x++)
            for (var y = 0; y < h; y++)
                for (var z = 0; z < d; z++)
                    if (shape[x, y, z] != other.shape[x, y, z])
                        return shape[x, y, z] ? -1 : 1;

        return 0;
    }
}