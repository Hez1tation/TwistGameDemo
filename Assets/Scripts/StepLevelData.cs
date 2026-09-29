public static class StepLevelData
{
    public static readonly string[] Names =
    {
        "Level 1 - Getting Started",
        "Level 2 - Multiply Up",
    };

    public static readonly string[] Hints =
    {
        "Every move costs 1 step. Numbers do nothing on their own - grab a + operator first, then the next number you touch adds to your step count.",
        "The direct route won't leave you enough steps. Find the x operator, then grab a number to multiply your step count before you run out.",
    };

    // # wall | . floor | P start | E exit. Used only to build the scene's primitives.
    public static readonly string[][] Layouts =
    {
        new[]
        {
            "###############",
            "#P...........E#",
            "###############",
        },
        new[]
        {
            "###############",
            "#P...........E#",
            "#####.#########",
            "#####.#########",
            "###############",
        },
    };

    public static readonly (int x, int y, int value)[][] Numbers =
    {
        new[] { (4, 1, 2), (8, 1, 3), (12, 1, 4) },
        new[] { (5, 3, 7) },
    };

    public static readonly (int x, int y, MathOp op)[][] Operators =
    {
        new[] { (3, 1, MathOp.Plus), (7, 1, MathOp.Plus), (11, 1, MathOp.Plus) },
        new[] { (5, 2, MathOp.Multiply) },
    };

    // Level 1: direct route is exactly 1 step short without pickups. Each number only counts
    // if you grabbed the + operator right before it (verified: still an 8-step surplus if you
    // collect every operator/number pair on the way).
    // Level 2: direct route and the detour-without-the-x-operator both fail; the only
    // solution (use x7 on the multiply tile) leaves a 4-step surplus.
    public static readonly int[] StartSteps = { 11, 8 };
}
