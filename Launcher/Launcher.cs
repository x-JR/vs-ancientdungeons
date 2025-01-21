using System;
using System.IO;
using Vintagestory.Client;

namespace Launcher;

public static class Launcher
{
    public static void Main(string[] args)
    {
        var libs = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Lib");
        var files = Directory.GetFiles(libs, "*.so*");
        foreach (var file in files)
        {
            var native = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Path.GetFileName(file));
            if(!File.Exists(native))
                File.Copy(file,native);
        }
        ClientProgram.Main(args);
    }
}