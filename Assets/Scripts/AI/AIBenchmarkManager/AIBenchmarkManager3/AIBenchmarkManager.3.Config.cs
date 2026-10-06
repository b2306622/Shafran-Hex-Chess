using UnityEngine;

public partial class AIBenchmarkManager
{
    [Header("Đánh giá độ sâu đối kháng")]

    [SerializeField]
    private bool enable3Benchmark = true;

    [SerializeField]
    private int benchmark3Positions = 20;

    [SerializeField]
    private int benchmark3RandomOpeningPlies = 8;

    [SerializeField]
    private int benchmark3RandomSeed = 20260912;

    [SerializeField]
    private bool benchmark3Depth2 = true;

    [SerializeField]
    private bool benchmark3Depth3 = true;

    [SerializeField]
    private bool benchmark3Depth4 = true;

    private const int DEPTH_2 = 2;
    private const int DEPTH_3 = 3;
    private const int DEPTH_4 = 4;

    [SerializeField]
    private int benchmark3MaxPlies = 100;

    [SerializeField]
    private bool benchmark3SwapSides = true;

    [SerializeField]
    private bool log3EachGame = false;

    private const bool BENCHMARK3_HISTORY =
        true;

    private const bool BENCHMARK3_KILLER =
        true;

    private const bool BENCHMARK3_TT =
        true;

    private const bool BENCHMARK3_ASPIRATION =
        true;
}
