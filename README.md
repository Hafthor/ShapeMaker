# ShapeMaker

ShapeMaker finds unique [polycubes](https://en.wikipedia.org/wiki/Polycube).

If you run it, it will find polycubes up to n=19, but it will likely run
out of RAM or user will run out of patience before then.

To find polycubes of n cubes, it extends all found polycubes of n-1 by
adding one cube to them.

ShapeMaker writes out files that are binary serializations of the polycubes
it has found. By default they will go to a ShapeMaker subdirectory in the
user's Downloads folder. You can provide a command line argument telling
ShapeMaker where you would rather have these files saved to.

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

Results and timing (in seconds) from 15" 2022 Microsoft Surface Laptop 4 w/ 32GB 4-core 11th Gen 3GHz Core i7, .NET 7 in Release mode

    n=2, shapes: 1 time: 0.0333732, chiral count: 1 time: 0.0013402
    n=3, shapes: 2 time: 0.0074581, chiral count: 2 time: 0.000919
    n=4, shapes: 8 time: 0.0095852, chiral count: 7 time: 0.0012408
    n=5, shapes: 29 time: 0.0122915, chiral count: 23 time: 0.0063124
    n=6, shapes: 166 time: 0.0229438, chiral count: 112 time: 0.0051743
    n=7, shapes: 1,023 time: 0.0489735, chiral count: 607 time: 0.0202495
    n=8, shapes: 6,922 time: 0.134048, chiral count: 3,811 time: 0.0596569
    n=9, shapes: 48,311 time: 0.6422478, chiral count: 25,413 time: 0.4140952
    n=10, shapes: 346,543 time: 2.9891527, chiral count: 178,083 time: 1.7550483
    n=11, shapes: 2,522,522 time: 16.7978658, chiral count: 1,279,537 time: 12.4580685
    n=12, shapes: 18,598,427 time: 127.1727464, chiral count: 9,371,094 time: 102.1856359
    n=13, shapes: 138,462,649 time: 1230.0157816, chiral count: 69,513,546 time: 899.4473192
    n=14, shapes: 1,039,496,297 time: 26384.3786476, chiral count: 520,878,101 time: 21295.4038754

# Potential Optimizations / Enhancements
* We could do a counting pass to see how to best partition the data to avoid making a bunch 
  of sharding passes that create few or no new polycubes.
* Save/resume partial sharded results.

# Potential Features
* Make a 4-D version?

# Limits
Currently limited by RAM because of the need to hashset the shapes to find the unique ones. We
extend this a bit by sharding by the shape dimension first, that is, we find all the shapes of
a particular size together at the same time, even when it means we have to reread source shape
a few times. When it becomes necessary, we also shard it by corner/edge/face counts. This is a
rotationally independent counting process so it is done before finding the minimal rotation.
By sharding by corner count alone, it is estimated that this would extend the maximum effective
memory by a factor of 4. By sharding it also by edges and faces, as we do, it should provide a
further factor of 5 improvement, for a total of 20. This should allow us to easily compute n=16
and possibly n=17 on a 96GB machine. This would likely take weeks to run.

# How it works
It starts with a n=1 polycube shape and it tries to add an adjacent neighbor cube to the shape
and check to see if we've encountered this shape before. When comparing shapes, we always find
the minimal rotation first, which means a rotation where w<=h and h<=d and then where the bits
compare as less. At each step, we are taking the results of the prior n and extending all the
unique shapes found to try to find new shapes. We can split the work in to what the dimensions
of the target shape will be, for example 2x3x5. This does mean that we may reread prior shapes
to generate all possible targets. When extending a shape, we attempt to extend it within the
bounds of the prior shape, but we also test extending the shape by growing its boundaries. To
scale this, we will, when it looks like the hashset for a specific dimension will exceed the
host's memory, shard by shape features that are rotationally independent, such as the number
of corners, edges, or faces set.

Note that this program writes out the shapes it finds as it goes. It is safe to terminate the
program and run again to resume.