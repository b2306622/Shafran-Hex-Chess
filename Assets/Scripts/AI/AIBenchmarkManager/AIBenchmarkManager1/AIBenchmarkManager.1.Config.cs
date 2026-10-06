using UnityEngine;

public partial class AIBenchmarkManager
{
    [Header("Tối ưu hóa tìm kiếm")]

    [SerializeField]
    private bool enable1Benchmark = true;

    [SerializeField]
    private bool benchmarkDepth2 = true;

    [SerializeField]
    private bool benchmarkDepth3 = true;

    [SerializeField]
    private bool benchmarkDepth4 = true;

    [SerializeField]
    private bool log1EachPosition = false;

    [SerializeField]
    private int numberOfPositions = 20;

    [SerializeField]
    private int randomOpeningPlies = 8;

    [SerializeField]
    private int randomSeed = 20260910;

    private const string CONFIG_BASELINE =
        "BASELINE";

    private const string CONFIG_HISTORY =
        "+ HISTORY";

    private const string CONFIG_KILLER =
        "+ KILLER";

    private const string CONFIG_TT =
        "+ TT";

    private const string CONFIG_ASPIRATION =
        "+ ASPIRATION";

    private readonly string[]
        _ablationConfigurations =
        {
            CONFIG_BASELINE,
            CONFIG_HISTORY,
            CONFIG_KILLER,
            CONFIG_TT,
            CONFIG_ASPIRATION
        };
}
