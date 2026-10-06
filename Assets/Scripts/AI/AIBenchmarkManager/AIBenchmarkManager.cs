using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public partial class AIBenchmarkManager
    : MonoBehaviour
{
    [SerializeField]
    private bool autoStart = false;

    [SerializeField]
    private bool restoreGameControllerAfterBenchmark =
        false;

    protected bool _benchmarkRunning;

    protected GameState _initialState;

    protected MonoBehaviour _gameController;
    protected bool _gameControllerWasEnabled;

    protected MonoBehaviour _aiGameController;
    protected bool _aiGameControllerWasEnabled;

    protected readonly StringBuilder _benchmarkReport =
        new StringBuilder();

    protected readonly Dictionary<string, AblationStatistics>
        _ablationStatistics =
            new Dictionary<string, AblationStatistics>();

    protected int _totalPositions;

    protected void Start()
    {
        if (autoStart)
        {
            StartBenchmark();
        }
    }

    [ContextMenu("Start Benchmark")]
    public void StartBenchmark()
    {
        if (_benchmarkRunning)
        {
            Debug.LogWarning(
                "[AIBenchmark] Benchmark is already running."
            );

            return;
        }

        StartCoroutine(
            RunBenchmarkRoutine()
        );
    }

    [ContextMenu("Stop Benchmark")]
    public void StopBenchmark()
    {
        if (!_benchmarkRunning)
        {
            Debug.LogWarning(
                "[AIBenchmark] No benchmark is currently running."
            );

            return;
        }

        StopAllCoroutines();

        RestoreGameController();

        _benchmarkRunning = false;

        Debug.LogWarning(
            "[AIBenchmark] Benchmark stopped by user."
        );
    }

    private IEnumerator RunBenchmarkRoutine()
    {
        _benchmarkRunning = true;

        _benchmarkReport.Clear();

        ResetStatistics();

        yield return StartCoroutine(
            WaitForChessBoardReady()
        );

        if (!_benchmarkRunning)
        {
            yield break;
        }

        if (ChessBoard.Current == null)
        {
            Debug.LogError(
                "[AIBenchmark] ChessBoard.Current is null."
            );

            FinishBenchmark();
            yield break;
        }

        if (ChessBoard.Current.Cells == null)
        {
            Debug.LogError(
                "[AIBenchmark] ChessBoard.Cells is null."
            );

            FinishBenchmark();
            yield break;
        }

        DisableGameController();

        _initialState =
            GameState.FromUnityBoard();

        if (!IsInitialStateValid(_initialState))
        {
            Debug.LogError(
                "[AIBenchmark] Initial GameState is invalid."
            );

            RestoreGameController();

            FinishBenchmark();
            yield break;
        }

        InitializeConfigurationStatistics();

        List<BenchmarkPosition> positions =
            GenerateBenchmarkPositions();

        if (
            positions == null ||
            positions.Count == 0)
        {
            Debug.LogError(
                "[AIBenchmark] No benchmark positions generated."
            );

            RestoreGameController();

            FinishBenchmark();
            yield break;
        }

        _totalPositions =
            positions.Count;

        // =====================================================
        // SEARCH OPTIMIZATION ABLATION
        // =====================================================

        if (enable1Benchmark)
        {
            if (benchmarkDepth2)
            {
                RunAblationBenchmark(
                    positions,
                    2
                );
            }

            if (benchmarkDepth3)
            {
                RunAblationBenchmark(
                    positions,
                    3
                );
            }

            if (benchmarkDepth4)
            {
                RunAblationBenchmark(
                    positions,
                    4
                );
            }

            AppendAblationFinalComparison();

            AppendAblationInterpretation();
        }

        // =====================================================
        // MOVE QUALITY + STABILITY
        // =====================================================

        if (enable2Benchmark)
        {
            Run2MoveQualityStability(
                positions
            );
        }

        // =====================================================
        // COMPETITIVE SEARCH-DEPTH EVALUATION
        // =====================================================

        if (enable3Benchmark)
        {
            Run3PlayingStrength();
        }

        AppendReport("");

        AppendReport(
            "=================================================="
        );

        AppendReport(
            restoreGameControllerAfterBenchmark
                ? "Controllers restored."
                : "Controllers remain disabled."
        );

        AppendReport(
            "AI BENCHMARK FINISHED"
        );

        AppendReport(
            "=================================================="
        );

        SaveBenchmarkReportToFile();

        if (restoreGameControllerAfterBenchmark)
        {
            RestoreGameController();
        }

        FinishBenchmark();
    }
}
