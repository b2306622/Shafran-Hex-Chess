using System.Collections.Generic;
using System.Text;

public partial class AIBenchmarkManager
{
    private void Appen2PositionReport(
        BenchmarkPosition position,
        int positionNumber,
        MoveQualitySample sample2,
        bool has2,
        MoveQualitySample sample3,
        bool has3,
        MoveQualitySample sample4,
        bool has4)
    {
        AppendReport("");

        AppendReport(
            $"POSITION {positionNumber} | " +
            $"Seed={position.Seed} | " +
            $"Side={position.State.SideToMove}"
        );

        if (has2)
        {
            Append2SampleReport(
                "D2",
                sample2
            );
        }

        if (has3)
        {
            Append2SampleReport(
                "D3",
                sample3
            );
        }

        if (has4)
        {
            Append2SampleReport(
                "D4",
                sample4
            );
        }

        if (has2 && has3)
        {
            AppendReport(
                $"D2 -> D3 Move Agreement : " +
                $"{(AreSameMove(sample2.BestMove, sample3.BestMove) ? 100.0 : 0.0):F2}%"
            );

            AppendReport(
                $"D2 -> D3 Score Difference: " +
                $"{(sample3.BestScore - sample2.BestScore):F2}"
            );

            AppendReport(
                $"D2 -> D3 PV Agreement   : " +
                $"{CalculatePVAgreement(sample2.PV, sample3.PV):F2}%"
            );
        }

        if (has3 && has4)
        {
            AppendReport(
                $"D3 -> D4 Move Agreement : " +
                $"{(AreSameMove(sample3.BestMove, sample4.BestMove) ? 100.0 : 0.0):F2}%"
            );

            AppendReport(
                $"D3 -> D4 Score Difference: " +
                $"{(sample4.BestScore - sample3.BestScore):F2}"
            );

            AppendReport(
                $"D3 -> D4 PV Agreement   : " +
                $"{CalculatePVAgreement(sample3.PV, sample4.PV):F2}%"
            );
        }
    }

    private void Append2SampleReport(
        string label,
        MoveQualitySample sample)
    {
        AppendReport(
            $"{label} Best Move        : " +
            $"{FormatMove(sample.BestMove)}"
        );

        AppendReport(
            $"{label} Best Score       : " +
            $"{sample.BestScore:F2}"
        );

        AppendReport(
            $"{label} Completed Depth  : " +
            $"{sample.CompletedDepth}"
        );

        AppendReport(
            $"{label} Nodes            : " +
            $"{sample.Nodes}"
        );

        AppendReport(
            $"{label} Search Time      : " +
            $"{sample.SearchTimeSeconds:F6}s"
        );

        AppendReport(
            $"{label} NPS              : " +
            $"{sample.NPS:F0}"
        );

        AppendReport(
            $"{label} PV Length        : " +
            $"{sample.PVLength}"
        );

        AppendReport(
            $"{label} PV Completeness  : " +
            $"{(sample.PVComplete ? "100.00%" : "0.00%")}"
        );

        AppendReport(
            $"{label} PV               : " +
            $"{Format2PV(sample.PV)}"
        );
    }

    private string Format2PV(
        List<StateMove> pv)
    {
        if (
            pv == null ||
            pv.Count == 0)
        {
            return "NONE";
        }

        StringBuilder builder =
            new StringBuilder();

        for (
            int i = 0;
            i < pv.Count;
            i++)
        {
            if (i > 0)
            {
                builder.Append(
                    " | "
                );
            }

            builder.Append(
                FormatMove(
                    pv[i]
                )
            );
        }

        return
            builder.ToString();
    }

    private void Append2DepthStatistics(
        MoveQualityDepthStatistics stats)
    {
        if (
            stats == null ||
            stats.SampleCount <= 0)
        {
            return;
        }

        double meanScore =
            CalculateMean(
                stats.ScoreSamples
            );

        double stdScore =
            CalculateSampleStandardDeviation(
                stats.ScoreSamples
            );

        double ciScore =
            CalculateConfidenceInterval95(
                stats.ScoreSamples
            );

        double meanTime =
            CalculateMean(
                stats.SearchTimeSamples
            );

        double stdTime =
            CalculateSampleStandardDeviation(
                stats.SearchTimeSamples
            );

        double ciTime =
            CalculateConfidenceInterval95(
                stats.SearchTimeSamples
            );

        double meanNodes =
            CalculateMean(
                stats.NodeSamples
            );

        double stdNodes =
            CalculateSampleStandardDeviation(
                stats.NodeSamples
            );

        double ciNodes =
            CalculateConfidenceInterval95(
                stats.NodeSamples
            );

        double meanNPS =
            stats.TotalSearchTime > 0.0
                ? stats.TotalNodes /
                  stats.TotalSearchTime
                : 0.0;

        double meanPVLength =
            CalculateMean(
                stats.PVLengthSamples
            );

        double pvCompleteness =
            (double)
            stats.PVCompleteCount /
            stats.SampleCount *
            100.0;

        AppendReport("");
        AppendReport(
            $"DEPTH {stats.Depth}"
        );

        AppendReport(
            $"Samples                  : " +
            $"{stats.SampleCount}"
        );

        AppendReport(
            $"Mean Best Score          : " +
            $"{meanScore:F2}"
        );

        AppendReport(
            $"Std Dev Best Score       : " +
            $"{stdScore:F2}"
        );

        AppendReport(
            $"95% CI Best Score        : " +
            $"±{ciScore:F2}"
        );

        AppendReport(
            $"Mean Nodes               : " +
            $"{meanNodes:F2}"
        );

        AppendReport(
            $"Std Dev Nodes            : " +
            $"{stdNodes:F2}"
        );

        AppendReport(
            $"95% CI Nodes             : " +
            $"±{ciNodes:F2}"
        );

        AppendReport(
            $"Mean Search Time         : " +
            $"{meanTime:F6}s"
        );

        AppendReport(
            $"Std Dev Search Time      : " +
            $"{stdTime:F6}s"
        );

        AppendReport(
            $"95% CI Search Time       : " +
            $"±{ciTime:F6}s"
        );

        AppendReport(
            $"Aggregate NPS            : " +
            $"{meanNPS:F0}"
        );

        AppendReport(
            $"Mean PV Length           : " +
            $"{meanPVLength:F2}"
        );

        AppendReport(
            $"PV Completeness          : " +
            $"{pvCompleteness:F2}%"
        );
    }

    private void Append2ComparisonReport(
        MoveQualityComparisonStatistics comparison,
        string title)
    {
        if (
            comparison == null ||
            comparison.PositionCount <= 0)
        {
            return;
        }

        double moveAgreementRate =
            (double)
            comparison.SameBestMoveCount /
            comparison.PositionCount *
            100.0;

        double meanScoreDifference =
            CalculateMean(
                comparison.ScoreDifferenceSamples
            );

        double meanAbsoluteScoreDifference =
            CalculateMean(
                comparison.AbsoluteScoreDifferenceSamples
            );

        double scoreStd =
            CalculateSampleStandardDeviation(
                comparison.ScoreDifferenceSamples
            );

        double pvAgreement =
            comparison.PVAgreementSum /
            comparison.PositionCount;

        double pvAgreementStd =
            CalculateSampleStandardDeviation(
                comparison.PVAgreementSamples
            );

        AppendReport("");

        AppendReport(
            title
        );

        AppendReport(
            $"Comparisons               : " +
            $"{comparison.PositionCount}"
        );

        AppendReport(
            $"Same Best Move            : " +
            $"{comparison.SameBestMoveCount}"
        );

        AppendReport(
            $"Different Best Move       : " +
            $"{comparison.PositionCount - comparison.SameBestMoveCount}"
        );

        AppendReport(
            $"Move Agreement Rate       : " +
            $"{moveAgreementRate:F2}%"
        );

        AppendReport(
            $"Mean Score Difference     : " +
            $"{meanScoreDifference:F2}"
        );

        AppendReport(
            $"Mean Absolute Score Diff  : " +
            $"{meanAbsoluteScoreDifference:F2}"
        );

        AppendReport(
            $"Score Difference Std Dev  : " +
            $"{scoreStd:F2}"
        );

        AppendReport(
            $"Mean PV Agreement         : " +
            $"{pvAgreement:F2}%"
        );

        AppendReport(
            $"PV Agreement Std Dev      : " +
            $"{pvAgreementStd:F2}%"
        );
    }

    private void Append2DivergenceSummary()
    {
        AppendReport("");

        AppendReport(
            "================ DIVERGENCE DATA ================"
        );

        AppendReport(
            $"Divergence Cases         : " +
            $"{_divergencePositions2.Count}"
        );

        for (
            int i = 0;
            i < _divergencePositions2.Count;
            i++)
        {
            DivergencePosition2 item =
                _divergencePositions2[i];

            if (item == null)
            {
                continue;
            }

            AppendReport(
                $"Case {item.CaseId} | " +
                $"Position={item.PositionNumber} | " +
                $"Seed={item.Seed} | " +
                $"Preplayed={item.PreplayedPlies} | " +
                $"D2={(item.HasD2 ? FormatMove(item.D2Move) : "NONE")} | " +
                $"D3={(item.HasD3 ? FormatMove(item.D3Move) : "NONE")} | " +
                $"D4={(item.HasD4 ? FormatMove(item.D4Move) : "NONE")} | " +
                $"D2/D3=" +
                $"{(item.HasD2 && item.HasD3 ? (item.D2vsD3Different ? "DIFFERENT" : "SAME") : "N/A")} | " +
                $"D3/D4=" +
                $"{(item.HasD3 && item.HasD4 ? (item.D3vsD4Different ? "DIFFERENT" : "SAME") : "N/A")}"
            );
        }

        AppendReport(
            "=================================================="
        );
    }
}
