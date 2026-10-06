using UnityEngine;

public partial class AIBenchmarkManager
{
    [Header("Chất lượng nước đi & Độ ổn định")]

    [SerializeField]
    private bool enable2Benchmark = true;

    [SerializeField]
    private bool benchmark2Depth2 = true;

    [SerializeField]
    private bool benchmark2Depth3 = true;

    [SerializeField]
    private bool benchmark2Depth4 = true;

    [SerializeField]
    private bool log2EachPosition = false;

    private const bool BENCHMARK_HISTORY =
        true;

    private const bool BENCHMARK_KILLER =
        true;

    private const bool BENCHMARK_TT =
        true;

    private const bool BENCHMARK_ASPIRATION =
        true;
}
