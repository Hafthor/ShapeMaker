using ShapeMaker;

namespace ShapeMakerTests;

[TestClass]
public class BitShapeHashSetTests {
    [TestMethod]
    public void OneByteHashSet() {
        var r = new Random();
        var ra = Enumerable.Range(0, 256).Select(_ => (byte)r.Next(256)).ToList();
        var rs = new HashSet<byte>();
        var bshs = BitShapeHashSetFactory.Create(1);

        foreach (var v in ra)
            Assert.AreEqual(rs.Add(v), bshs.Add(new[] { v }));

        foreach (var ba in bshs)
            Assert.IsTrue(rs.Remove(ba[0]));
        Assert.AreEqual(0, rs.Count);

        // test parallel operations
        // note would have been possible for this test to fail if two threads are both working with the same
        // number and thread A executes just the rs.Add, then thread B executes the whole test. It will fail
        // since rs already had the value, while the BitShapeHashSet did not. In order to safe guard against
        // this we need an object for each element in ra that we can place a lock on.
        var lo = new Dictionary<byte, object>();
        foreach (var v in ra)
            if (!lo.ContainsKey(v))
                lo.Add(v, new object());
        rs.Clear();
        bshs.Clear();
        Parallel.ForEach(ra, v => {
            lock (lo[v]) {
                bool added;
                lock (rs)
                    added = rs.Add(v);
                Assert.AreEqual(added, bshs.Add(new[] { v }));
            }
        });
        foreach (var ba in bshs)
            Assert.IsTrue(rs.Remove(ba[0]));
        Assert.AreEqual(0, rs.Count);
    }

    [TestMethod]
    public void TwoByteHashSet() {
        var r = new Random();
        var ra = Enumerable.Range(0, 65536).Select(_ => (ushort)r.Next(65536)).ToList();
        var rs = new HashSet<ushort>();
        var bshs = BitShapeHashSetFactory.Create(2);

        foreach (var v in ra)
            Assert.AreEqual(rs.Add(v), bshs.Add(new[] { (byte)(v >> 8), (byte)v }));

        foreach (var ba in bshs)
            Assert.IsTrue(rs.Remove((ushort)((ba[0] << 8) + ba[1])));
        Assert.AreEqual(0, rs.Count);

        // test parallel operations
        // note would have been possible for this test to fail if two threads are both working with the same
        // number and thread A executes just the rs.Add, then thread B executes the whole test. It will fail
        // since rs already had the value, while the BitShapeHashSet did not. In order to safe guard against
        // this we need an object for each element in ra that we can place a lock on.
        var lo = new Dictionary<ushort, object>();
        foreach (var v in ra)
            if (!lo.ContainsKey(v))
                lo.Add(v, new object());
        rs.Clear();
        bshs.Clear();
        Parallel.ForEach(ra, v => {
            lock (lo[v]) {
                bool added;
                lock (rs)
                    added = rs.Add(v);
                Assert.AreEqual(added, bshs.Add(new[] { (byte)(v >> 8), (byte)v }));
            }
        });
        foreach (var ba in bshs)
            Assert.IsTrue(rs.Remove((ushort)((ba[0] << 8) + ba[1])));
        Assert.AreEqual(0, rs.Count);
    }

    [TestMethod]
    public void ThreeByteHashSet() {
        var r = new Random();
        var ra = Enumerable.Range(0, 65280).Select(_ => r.Next(65280) * 257).ToList();
        var rs = new HashSet<int>();
        var bshs = BitShapeHashSetFactory.Create(3);

        foreach (var v in ra)
            Assert.AreEqual(rs.Add(v), bshs.Add(new[] { (byte)(v >> 16), (byte)(v >> 8), (byte)v }));

        foreach (var ba in bshs)
            Assert.IsTrue(rs.Remove((ba[0] << 16) + (ba[1] << 8) + ba[2]));
        Assert.AreEqual(0, rs.Count);

        // test parallel operations
        // note would have been possible for this test to fail if two threads are both working with the same
        // number and thread A executes just the rs.Add, then thread B executes the whole test. It will fail
        // since rs already had the value, while the BitShapeHashSet did not. In order to safe guard against
        // this we need an object for each element in ra that we can place a lock on.
        var lo = new Dictionary<int, object>();
        foreach (var v in ra)
            if (!lo.ContainsKey(v))
                lo.Add(v, new object());
        rs.Clear();
        bshs.Clear();
        Parallel.ForEach(ra, v => {
            lock (lo[v]) {
                bool added;
                lock (rs)
                    added = rs.Add(v);
                Assert.AreEqual(added, bshs.Add(new[] { (byte)(v >> 16), (byte)(v >> 8), (byte)v }));
            }
        });
        foreach (var ba in bshs)
            Assert.IsTrue(rs.Remove((ba[0] << 16) + (ba[1] << 8) + ba[2]));
        Assert.AreEqual(0, rs.Count);
    }

    [TestMethod]
    public void FourByteHashSet() {
        var r = new Random();
        var ra = Enumerable.Range(0, 65535).Select(_ => (uint)r.Next(65535) * 65537u).ToList();
        var rs = new HashSet<uint>();
        var bshs = BitShapeHashSetFactory.Create(4);

        foreach (var v in ra)
            Assert.AreEqual(rs.Add(v), bshs.Add(new[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v }));

        foreach (var ba in bshs)
            Assert.IsTrue(rs.Remove((uint)(((uint)ba[0] << 24) + (ba[1] << 16) + (ba[2] << 8) + ba[3])));
        Assert.AreEqual(0, rs.Count);

        // test parallel operations
        // note would have been possible for this test to fail if two threads are both working with the same
        // number and thread A executes just the rs.Add, then thread B executes the whole test. It will fail
        // since rs already had the value, while the BitShapeHashSet did not. In order to safe guard against
        // this we need an object for each element in ra that we can place a lock on.
        var lo = new Dictionary<uint, object>();
        foreach (var v in ra)
            if (!lo.ContainsKey(v))
                lo.Add(v, new object());
        rs.Clear();
        bshs.Clear();
        Parallel.ForEach(ra, v => {
            lock (lo[v]) {
                bool added;
                lock (rs)
                    added = rs.Add(v);
                Assert.AreEqual(added, bshs.Add(new[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v }));
            }
        });

        foreach (var ba in bshs)
            Assert.IsTrue(rs.Remove((uint)(((uint)ba[0] << 24) + (ba[1] << 16) + (ba[2] << 8) + ba[3])));
        Assert.AreEqual(0, rs.Count);
    }

    [TestMethod]
    public void FiveByteHashSet() {
        var r = new Random();
        var ra = Enumerable.Range(0, 65535).Select(_ => r.Next(65535) * 65280L * 257L).ToList();
        var rs = new HashSet<long>();
        var bshs = BitShapeHashSetFactory.Create(5);

        foreach (var v in ra)
            Assert.AreEqual(rs.Add(v), bshs.Add(new[] { (byte)(v >> 32), (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v }));

        foreach (var ba in bshs)
            Assert.IsTrue(rs.Remove(((long)ba[0] << 32) + ((long)ba[1] << 24) + (ba[2] << 16) + (ba[3] << 8) + ba[4]));
        Assert.AreEqual(0, rs.Count);

        // test parallel operations
        // note would have been possible for this test to fail if two threads are both working with the same
        // number and thread A executes just the rs.Add, then thread B executes the whole test. It will fail
        // since rs already had the value, while the BitShapeHashSet did not. In order to safe guard against
        // this we need an object for each element in ra that we can place a lock on.
        var lo = new Dictionary<long, object>();
        foreach (var v in ra)
            if (!lo.ContainsKey(v))
                lo.Add(v, new object());
        rs.Clear();
        bshs.Clear();
        Parallel.ForEach(ra, v => {
            lock (lo[v]) {
                bool added;
                lock (rs)
                    added = rs.Add(v);
                Assert.AreEqual(added, bshs.Add(new[] { (byte)(v >> 32), (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v }));
            }
        });

        foreach (var ba in bshs)
            Assert.IsTrue(rs.Remove(((long)ba[0] << 32) + ((long)ba[1] << 24) + (ba[2] << 16) + (ba[3] << 8) + ba[4]));
        Assert.AreEqual(0, rs.Count);
    }

    [TestMethod]
    public void SixByteHashSet16M() => SixByteHashSet(true);
    
    [TestMethod]
    public void SixByteHashSet64K() => SixByteHashSet(false);
    
    public void SixByteHashSet(bool preferSpeedOverMemory) {
        var r = new Random();
        var ra = Enumerable.Range(0, 65535).Select(_ => r.Next(65535) * 65535L * 65537L).ToList();
        var rs = new HashSet<long>();
        var bshs = BitShapeHashSetFactory.Create(6, preferSpeedOverMemory);

        foreach (var v in ra)
            Assert.AreEqual(rs.Add(v), bshs.Add(new[] { (byte)(v >> 40), (byte)(v >> 32), (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v }));

        foreach (var ba in bshs)
            Assert.IsTrue(rs.Remove(((long)ba[0] << 40) + ((long)ba[1] << 32) + ((long)ba[2] << 24) + (ba[3] << 16) + (ba[4] << 8) + ba[5]));
        Assert.AreEqual(0, rs.Count);

        // test overloading a page
        var ra2 = Enumerable.Range(0, 65535).Select(_ => (long)r.Next(65535) << 24).ToList();
        rs.Clear();
        bshs.Clear();

        foreach (var v in ra2)
            Assert.AreEqual(rs.Add(v), bshs.Add(new[] { (byte)(v >> 40), (byte)(v >> 32), (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v }));

        foreach (var ba in bshs)
            Assert.IsTrue(rs.Remove(((long)ba[0] << 40) + ((long)ba[1] << 32) + ((long)ba[2] << 24) + (ba[3] << 16) + (ba[4] << 8) + ba[5]));
        Assert.AreEqual(0, rs.Count);

        // test parallel operations
        // note would have been possible for this test to fail if two threads are both working with the same
        // number and thread A executes just the rs.Add, then thread B executes the whole test. It will fail
        // since rs already had the value, while the BitShapeHashSet did not. In order to safe guard against
        // this we need an object for each element in ra that we can place a lock on.
        var lo = new Dictionary<long, object>();
        foreach (var v in ra)
            if (!lo.ContainsKey(v))
                lo.Add(v, new object());
        rs.Clear();
        bshs.Clear();
        Parallel.ForEach(ra, v => {
            lock (lo[v]) {
                bool added;
                lock (rs)
                    added = rs.Add(v);
                Assert.AreEqual(added, bshs.Add(new[] { (byte)(v >> 40), (byte)(v >> 32), (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v }));
            }
        });

        foreach (var ba in bshs)
            Assert.IsTrue(rs.Remove(((long)ba[0] << 40) + ((long)ba[1] << 32) + ((long)ba[2] << 24) + (ba[3] << 16) + (ba[4] << 8) + ba[5]));
        Assert.AreEqual(0, rs.Count);

        // test overloading a page
        var lo2 = new Dictionary<long, object>();
        foreach (var v in ra2)
            if (!lo2.ContainsKey(v))
                lo2.Add(v, new object());
        rs.Clear();
        bshs.Clear();

        Parallel.ForEach(ra2, v => {
            lock (lo2[v]) {
                bool added;
                lock (rs)
                    added = rs.Add(v);
                Assert.AreEqual(added, bshs.Add(new[] { (byte)(v >> 40), (byte)(v >> 32), (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v }));
            }
        });

        foreach (var ba in bshs)
            Assert.IsTrue(rs.Remove(((long)ba[0] << 40) + ((long)ba[1] << 32) + ((long)ba[2] << 24) + (ba[3] << 16) + (ba[4] << 8) + ba[5]));
        Assert.AreEqual(0, rs.Count);
    }
}
