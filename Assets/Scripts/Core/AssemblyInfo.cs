using System.Runtime.CompilerServices;

// EditMode tests drive the public surface, but a proto may want to unit-test an internal seam directly.
// Test-only — no production code outside Proto.Core sees internals.
[assembly: InternalsVisibleTo("Proto.Tests")]
