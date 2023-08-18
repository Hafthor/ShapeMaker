using ShapeMaker;

namespace ShapeMakerTests;

[TestClass]
public class BitShapeHashSetTests {
	[TestMethod]
	public void OneByteHashSet() {
		var r = new Random();
		var ra = Enumerable.Range(0, 256).Select(_ => (byte)r.Next(256));
		var rs = new HashSet<byte>();
		var bshs = new BitShapeHashSet(1);

		foreach (var v in ra) {
			bool added = rs.Add(v);
			bool added2 = bshs.Add(new byte[] { v });
			Assert.AreEqual(added, added2);
		}

		foreach (var ba in bshs) {
			var v = ba[0];
			Assert.IsTrue(rs.Remove(v));
		}
		Assert.AreEqual(0, rs.Count);
	}

	[TestMethod]
	public void TwoByteHashSet() {
        var r = new Random();
        var ra = Enumerable.Range(0, 65536).Select(_ => (ushort)r.Next(65536));
        var rs = new HashSet<ushort>();
        var bshs = new BitShapeHashSet(2);

        foreach (var v in ra) {
            bool added = rs.Add(v);
            bool added2 = bshs.Add(new byte[] { (byte)(v >> 8), (byte)v });
            Assert.AreEqual(added, added2);
        }

        foreach (var ba in bshs) {
			ushort v = (ushort)((ba[0] << 8) + ba[1]);
            Assert.IsTrue(rs.Remove(v));
        }
        Assert.AreEqual(0, rs.Count);
    }

	[TestMethod]
    public void ThreeByteHashSet() {
        var r = new Random();
        var ra = Enumerable.Range(0, 65280).Select(_ => r.Next(65280) * 257);
        var rs = new HashSet<int>();
        var bshs = new BitShapeHashSet(3);

        foreach (var v in ra) {
            bool added = rs.Add(v);
            bool added2 = bshs.Add(new byte[] { (byte)(v >> 16), (byte)(v >> 8), (byte)v });
            Assert.AreEqual(added, added2);
        }

        foreach (var ba in bshs) {
            int v = (ba[0] << 16) + (ba[1] << 8) + ba[2];
            Assert.IsTrue(rs.Remove(v));
        }
        Assert.AreEqual(0, rs.Count);
    }

    [TestMethod]
    public void FourByteHashSet() {
        var r = new Random();
        var ra = Enumerable.Range(0, 65535).Select(_ => (uint)r.Next(65535) * 65537u);
        var rs = new HashSet<uint>();
        var bshs = new BitShapeHashSet(4);

        foreach (var v in ra) {
            bool added = rs.Add(v);
            bool added2 = bshs.Add(new byte[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v });
            Assert.AreEqual(added, added2);
        }

        foreach (var ba in bshs) {
            uint v = (uint)(((uint)ba[0] << 24) + (ba[1] << 16) + (ba[2] << 8) + ba[3]);
            Assert.IsTrue(rs.Remove(v));
        }
        Assert.AreEqual(0, rs.Count);

        rs.Clear();
        bshs.Clear();

        foreach (var v in ra) {
            bool added = rs.Add(v);
            bool added2 = bshs.Add(new byte[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v });
            Assert.AreEqual(added, added2);
        }

        foreach (var ba in bshs) {
            uint v = (uint)(((uint)ba[0] << 24) + (ba[1] << 16) + (ba[2] << 8) + ba[3]);
            Assert.IsTrue(rs.Remove(v));
        }
        Assert.AreEqual(0, rs.Count);
    }

    [TestMethod]
    public void FiveByteHashSet() {
        var r = new Random();
        var ra = Enumerable.Range(0, 65535).Select(_ => r.Next(65535) * 65280L * 257L);
        var rs = new HashSet<long>();
        var bshs = new BitShapeHashSet(5);

        foreach (var v in ra) {
            bool added = rs.Add(v);
            bool added2 = bshs.Add(new byte[] { (byte)(v >> 32), (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v });
            Assert.AreEqual(added, added2);
        }

        foreach (var ba in bshs) {
            long v = ((long)ba[0] << 32) + ((long)ba[1] << 24) + (ba[2] << 16) + (ba[3] << 8) + ba[4];
            Assert.IsTrue(rs.Remove(v));
        }
        Assert.AreEqual(0, rs.Count);
    }

    [TestMethod]
    public void SixByteHashSet() {
        var r = new Random();
        var ra = Enumerable.Range(0, 65535).Select(_ => r.Next(65535) * 65535L * 65537L);
        var rs = new HashSet<long>();
        var bshs = new BitShapeHashSet(6);

        foreach (var v in ra) {
            bool added = rs.Add(v);
            bool added2 = bshs.Add(new byte[] { (byte)(v >> 40), (byte)(v >> 32), (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v });
            Assert.AreEqual(added, added2);
        }

        foreach (var ba in bshs) {
            long v = ((long)ba[0] << 40) + ((long)ba[1] << 32) +((long)ba[2] << 24) + (ba[3] << 16) + (ba[4] << 8) + ba[5];
            Assert.IsTrue(rs.Remove(v));
        }
        Assert.AreEqual(0, rs.Count);

        // test overloading a page
        ra = Enumerable.Range(0, 65535).Select(_ => (long)r.Next(65535) << 24);
        rs.Clear();
        bshs.Clear();

        foreach (var v in ra) {
            bool added = rs.Add(v);
            bool added2 = bshs.Add(new byte[] { (byte)(v >> 40), (byte)(v >> 32), (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v });
            Assert.AreEqual(added, added2);
        }

        foreach (var ba in bshs) {
            long v = ((long)ba[0] << 40) + ((long)ba[1] << 32) + ((long)ba[2] << 24) + (ba[3] << 16) + (ba[4] << 8) + ba[5];
            Assert.IsTrue(rs.Remove(v));
        }
        Assert.AreEqual(0, rs.Count);
    }
}

