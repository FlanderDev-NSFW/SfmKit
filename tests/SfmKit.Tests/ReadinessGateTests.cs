using SfmKit.Pure;

namespace SfmKit.Tests
{
    internal static class ReadinessGateTests
    {
        internal static void Run()
        {
            var id = new long[] { 100, 200 };

            // Needs 3 distinct frames AND 0.5 s.
            var g = new ReadinessGate(3, 0.5f);
            T.Check(!g.Observe(id, true, 0.0f, 1), "not ready on first sample");
            T.Check(!g.Observe(id, true, 0.2f, 2), "not ready: 2 frames, 0.2 s");
            T.Check(!g.Observe(id, true, 0.3f, 3), "not ready: 3 frames but only 0.3 s");
            T.Check(g.Observe(id, true, 0.6f, 4), "ready: 4 frames and 0.6 s");

            // Several calls within one frame count once.
            g = new ReadinessGate(3, 0f);
            g.Observe(id, true, 0f, 1); g.Observe(id, true, 0f, 1); g.Observe(id, true, 0f, 1);
            T.Check(!g.Observe(id, true, 0f, 1), "same frame repeated is one sample");
            T.Check(!g.Observe(id, true, 0f, 2), "2 distinct frames still not enough");
            T.Check(g.Observe(id, true, 0f, 3), "3 distinct frames ready");

            // Identity change restarts the window.
            g = new ReadinessGate(2, 0f);
            g.Observe(id, true, 0f, 1);
            T.Check(g.Observe(id, true, 0f, 2), "ready with stable identity");
            T.Check(!g.Observe(new long[] { 100, 999 }, true, 0f, 3), "identity change resets");
            T.Check(g.Observe(new long[] { 100, 999 }, true, 0f, 4), "ready again after new identity is stable");

            // Condition false resets.
            g = new ReadinessGate(2, 0f);
            g.Observe(id, true, 0f, 1);
            T.Check(!g.Observe(id, false, 0f, 2), "condition false is never ready");
            T.Check(!g.Observe(id, true, 0f, 3), "window restarts after condition false");
            T.Check(g.Observe(id, true, 0f, 4), "ready after window refilled");

            // Null / empty identity is never ready.
            g = new ReadinessGate(1, 0f);
            T.Check(!g.Observe(null, true, 0f, 1), "null identity not ready");
            T.Check(!g.Observe(new long[0], true, 0f, 2), "empty identity not ready");

            // Argument validation.
            T.Throws<System.ArgumentOutOfRangeException>(() => new ReadinessGate(0, 0f), "minFrames must be >= 1");
            T.Throws<System.InvalidOperationException>(() => new ReadinessGate().Observe(id, true, float.NaN, 1), "NaN clock rejected");
        }
    }
}
