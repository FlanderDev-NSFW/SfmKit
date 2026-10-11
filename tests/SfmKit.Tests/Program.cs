using System;
using System.Collections.Generic;

namespace SfmKit.Tests
{
    /// <summary>Minimal assertion helpers. Exit code 0 = all passed.</summary>
    internal static class T
    {
        internal static int Passed, Failed;

        internal static void Check(bool condition, string name)
        {
            if (condition) { Passed++; Console.WriteLine("  PASS  " + name); }
            else { Failed++; Console.WriteLine("  FAIL  " + name); }
        }

        internal static void Throws<TEx>(Action action, string name) where TEx : Exception
        {
            try { action(); Check(false, name + " (no exception)"); }
            catch (TEx) { Check(true, name); }
            catch (Exception e) { Check(false, name + " (wrong exception " + e.GetType().Name + ")"); }
        }
    }

    internal static class Program
    {
        private static int Main()
        {
            var suites = new List<(string, Action)>
            {
                ("ReadinessGate", ReadinessGateTests.Run),
            };

            foreach (var (name, run) in suites)
            {
                Console.WriteLine(name);
                try { run(); }
                catch (Exception e) { T.Check(false, name + " suite crashed: " + e.Message); }
            }

            Console.WriteLine();
            Console.WriteLine(T.Passed + " passed, " + T.Failed + " failed");
            return T.Failed == 0 ? 0 : 1;
        }
    }
}
