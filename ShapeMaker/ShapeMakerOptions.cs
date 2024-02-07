namespace ShapeMaker;

public class ShapeMakerOptions {
    // options and their defaults
    public string filePath = ".";
    public HashSetAlgorithm hashSetAlgorithm = HashSetAlgorithm.HashSet16M;
    public bool doMirrorCount = true;
    public bool doForceRecompute = false;
    public int maxComputeN = 19;

    public static int ParseCommandLineOptions(string[] args, ref ShapeMakerOptions options) {
        // parse command line options
        bool getMaxComputeNext = false;
        foreach (var arg in args) {
            if (getMaxComputeNext) {
                options.maxComputeN = int.Parse(arg);
                getMaxComputeNext = false;
            } else if (arg.StartsWith("--"))
                if (arg.Equals("--no-mirror-count", StringComparison.OrdinalIgnoreCase))
                    options.doMirrorCount = false;
                else if (arg.Equals("--force-recompute", StringComparison.OrdinalIgnoreCase))
                    options.doForceRecompute = true;
                else if (arg.Equals("--max-compute", StringComparison.OrdinalIgnoreCase))
                    getMaxComputeNext = true;
                else if (arg.Equals("--help", StringComparison.OrdinalIgnoreCase))
                    return ShowHelp();
                else if (Enum.TryParse(arg[2..], true, out HashSetAlgorithm algorithm))
                    options.hashSetAlgorithm = algorithm;
                else
                    return ShowError("Unrecognized parameter " + arg);
            else if (arg.StartsWith("-"))
                if (arg.Equals("-n", StringComparison.OrdinalIgnoreCase))
                    getMaxComputeNext = true;
                else if (arg.Equals("-f", StringComparison.OrdinalIgnoreCase))
                    options.doForceRecompute = true;
                else if (arg.Equals("-h", StringComparison.OrdinalIgnoreCase) || arg == "-?")
                    return ShowHelp();
                else
                    return ShowError("Unrecognized parameter " + arg);
            else if (arg.Equals("/h", StringComparison.OrdinalIgnoreCase) || arg == "/?")
                return ShowHelp();
            else
                options.filePath = arg;
        }
        if (getMaxComputeNext)
            return ShowError("Missing parameter for --max-compute (or -n)");

        if (options.filePath == "~")
            options.filePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        else if (options.filePath.StartsWith("~/"))
            options.filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), options.filePath.Substring("~/".Length));

        return -1; // don't terminate
        
        int ShowHelp() {
            Console.WriteLine("ShapeMaker [options] [path]");
            Console.WriteLine("path: where shape files will be stored");
            Console.WriteLine("options:");
            Console.WriteLine("  --no-mirror-count   skip the mirror count operation");
            Console.WriteLine("  --force-recompute   recompute all shapes, even if they have already been computed");
            Console.WriteLine("  --max-compute [n]   compute up to n voxel count shapes - note that it will not output shape files for the last n");
            Console.WriteLine("  --hashset           use a HashSet to store the shapes");
            Console.WriteLine("  --hashset256        use 256 HashSets to store the shapes");
            Console.WriteLine("  --dictionary        use a ConcurrentDictionary to store the shapes");
            Console.WriteLine("  --hashset64k        use a BitShapeHashSet with 64K buckets to store the shapes");
            Console.WriteLine("  --hashset16m        use a BitShapeHashSet with 16M buckets to store the shapes");
            Console.WriteLine("  --help              show this help");
            return 0;
        }

        int ShowError(string message) {
            Console.WriteLine("Error: " + message);
            Console.WriteLine("Use --help for help");
            return 1;
        }
    }
    
    
}