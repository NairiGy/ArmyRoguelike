// Not part of the Unity project: compiled by tools/dev.sh wiki.
using System.IO;
using System.Text;

// Writes the gamepedia without Unity (no statue images), for when the editor is busy.
public static class Program
{
    public static int Main(string[] args)
    {
        File.WriteAllText(args[0], GamepediaWriter.Build(null, "text-only export"), new UTF8Encoding(false));
        return 0;
    }
}
