# ShapeMaker

ShapeMaker finds unique Polycubes.

If you run it, it will find Polycubes up to n=19, but it will likely run
out of RAM or user will run out of patience before then.

To find Polycubes of n cubes, it extends all found Polycubes of n-1 by
adding one cube to them.

ShapeMaker writes out files that are binary serializations of the Polycubes
it has found. By default they will go to a ShapeMaker subdirectory in the
user's Downloads folder. You can provide a command line argument telling
ShapeMaker where you would rather have these files saved to.