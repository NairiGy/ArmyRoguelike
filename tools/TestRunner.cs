// Not part of the Unity project: compiled by tools/dev.sh test.
using System;
using System.Linq;
using System.Reflection;

// Minimal NUnit-style runner: runs every [Test] method in this assembly and reports failures.
public static class Runner
{
    public static int Main()
    {
        int passed = 0, failed = 0, skipped = 0;
        var tests = Assembly.GetExecutingAssembly().GetTypes()
            .SelectMany(t => t.GetMethods().Where(m => m.GetCustomAttributes().Any(a => a.GetType().Name == "TestAttribute"))
            .Select(m => (t, m)));
        foreach (var (type, method) in tests)
        {
            try
            {
                method.Invoke(Activator.CreateInstance(type), null);
                passed++;
            }
            catch (TargetInvocationException e)
            {
                var inner = e.InnerException;
                if (inner != null && (inner.GetType().Name == "InconclusiveException" || inner.GetType().Name == "IgnoreException"))
                {
                    skipped++;
                    Console.WriteLine($"SKIP {type.Name}.{method.Name}");
                    continue;
                }
                failed++;
                Console.WriteLine($"FAIL {type.Name}.{method.Name}: {inner?.GetType().Name}: {inner?.Message}");
            }
        }
        Console.WriteLine($"passed {passed}, failed {failed}, skipped {skipped}");
        return failed == 0 ? 0 : 1;
    }
}
