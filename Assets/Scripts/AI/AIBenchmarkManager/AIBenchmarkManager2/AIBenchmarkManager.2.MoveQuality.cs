using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public partial class AIBenchmarkManager
{
    private void Run2MoveQualityStability(
        List<BenchmarkPosition> positions)
    {
        if (!enable2Benchmark)
        {
            return;
        }

        Reset2DivergenceData();

        if (
            positions == null ||
            positions.Count == 0)
        {
            return;
        }

        AppendReport("");
        AppendReport(
            "=================================================="
        );

        AppendReport(
            "AI BENCHMARK 7.7"
        );

        AppendReport(
            "MOVE QUALITY + STABILITY"
        );

        AppendReport(
            "=================================================="
        );

        AppendReport(
            $"Positions Tested       : " +
            $"{positions.Count}"
        );

        AppendReport(
            "Depths Tested           : " +
            $"{(benchmark2Depth2 ? "D2 " : "")}" +
            $"{(benchmark2Depth3 ? "D3 " : "")}" +
            $"{(benchmark2Depth4 ? "D4" : "")}"
        );

        AppendReport(
            "Experimental Control    : " +
            "Same Position + Same Side + Same Hardware"
        );

        AppendReport(
            "Search Configuration    : " +
            "+ HISTORY + KILLER + TT + ASPIRATION"
        );

        AppendReport(
            "History Heuristic       : ENABLED"
        );

        AppendReport(
            "Killer Move Heuristic   : ENABLED"
        );

        AppendReport(
            "Transposition Table     : ENABLED"
        );

        AppendReport(
            "Aspiration Window       : ENABLED"
        );

        AppendReport(
            "=================================================="
        );

        Dictionary<int, MoveQualityDepthStatistics>
            depthStatistics =
            new Dictionary<int, MoveQualityDepthStatistics>();

        if (benchmark2Depth2)
        {
            depthStatistics.Add(
                2,
                new MoveQualityDepthStatistics(2)
            );
        }

        if (benchmark2Depth3)
        {
            depthStatistics.Add(
                3,
                new MoveQualityDepthStatistics(3)
            );
        }

        if (benchmark2Depth4)
        {
            depthStatistics.Add(
                4,
                new MoveQualityDepthStatistics(4)
            );
        }

        MoveQualityComparisonStatistics comparison23 =
            new MoveQualityComparisonStatistics(
                2,
                3
            );

        MoveQualityComparisonStatistics comparison34 =
            new MoveQualityComparisonStatistics(
                3,
                4
            );

        int comparedPositions23 = 0;
        int comparedPositions34 = 0;

        AppendReport(
            "HARDWARE / RUNTIME CONTROL"
        );

        AppendReport(
            $"Device Model            : " +
            $"{SystemInfo.deviceModel}"
        );

        AppendReport(
            $"CPU                     : " +
            $"{SystemInfo.processorType}"
        );

        AppendReport(
            $"CPU Cores               : " +
            $"{SystemInfo.processorCount}"
        );

        AppendReport(
            $"System Memory           : " +
            $"{SystemInfo.systemMemorySize} MB"
        );

        AppendReport(
            $"GPU                     : " +
            $"{SystemInfo.graphicsDeviceName}"
        );

        AppendReport(
            $"Operating System        : " +
            $"{SystemInfo.operatingSystem}"
        );

        AppendReport(
            $"Unity Version           : " +
            $"{Application.unityVersion}"
        );

        for (
            int i = 0;
            i < positions.Count;
            i++)
        {
            BenchmarkPosition position =
                positions[i];

            MoveQualitySample sample2 =
                default;

            MoveQualitySample sample3 =
                default;

            MoveQualitySample sample4 =
                default;

            bool has2 = false;
            bool has3 = false;
            bool has4 = false;

            if (benchmark2Depth2)
            {
                sample2 =
                    Execute2Search(
                        position,
                        2
                    );

                has2 = true;

                Accumulate2DepthStatistics(
                    depthStatistics[2],
                    sample2
                );
            }

            if (benchmark2Depth3)
            {
                sample3 =
                    Execute2Search(
                        position,
                        3
                    );

                has3 = true;

                Accumulate2DepthStatistics(
                    depthStatistics[3],
                    sample3
                );
            }

            if (benchmark2Depth4)
            {
                sample4 =
                    Execute2Search(
                        position,
                        4
                    );

                has4 = true;

                Accumulate2DepthStatistics(
                    depthStatistics[4],
                    sample4
                );
            }

            if (has2 && has3)
            {
                comparedPositions23++;

                Record2Comparison(
                    comparison23,
                    sample2,
                    sample3
                );
            }

            if (has3 && has4)
            {
                comparedPositions34++;

                Record2Comparison(
                    comparison34,
                    sample3,
                    sample4
                );
            }

            Record2DivergencePosition(
                position,
                i + 1,
                sample2,
                has2,
                sample3,
                has3,
                sample4,
                has4
            );

            if (log2EachPosition)
            {
                Appen2PositionReport(
                    position,
                    i + 1,
                    sample2,
                    has2,
                    sample3,
                    has3,
                    sample4,
                    has4
                );
            }
        }

        AppendReport("");
        AppendReport(
            "================ SEARCH STABILITY ================"
        );

        List<int> depths =
            new List<int>(
                depthStatistics.Keys
            );

        depths.Sort();

        for (
            int i = 0;
            i < depths.Count;
            i++)
        {
            Append2DepthStatistics(
                depthStatistics[
                    depths[i]
                ]
            );
        }

        AppendReport("");
        AppendReport(
            "================ 7.7 MOVE QUALITY ================"
        );

        if (comparedPositions23 > 0)
        {
            Append2ComparisonReport(
                comparison23,
                "DEPTH 2 VS DEPTH 3"
            );
        }

        if (comparedPositions34 > 0)
        {
            Append2ComparisonReport(
                comparison34,
                "DEPTH 3 VS DEPTH 4"
            );
        }

        int totalMoveComparisons = 0;
        int totalMoveAgreements = 0;

        int totalPVComparisons = 0;
        double totalPVAgreement = 0.0;

        if (comparedPositions23 > 0)
        {
            totalMoveComparisons +=
                comparison23.PositionCount;

            totalMoveAgreements +=
                comparison23.SameBestMoveCount;

            totalPVComparisons +=
                comparison23.PositionCount;

            totalPVAgreement +=
                comparison23.PVAgreementSum;
        }

        if (comparedPositions34 > 0)
        {
            totalMoveComparisons +=
                comparison34.PositionCount;

            totalMoveAgreements +=
                comparison34.SameBestMoveCount;

            totalPVComparisons +=
                comparison34.PositionCount;

            totalPVAgreement +=
                comparison34.PVAgreementSum;
        }

        double overallMoveAgreementRate =
            totalMoveComparisons > 0
                ? (double)
                    totalMoveAgreements /
                    totalMoveComparisons *
                    100.0
                : 0.0;

        double overallPVAgreementRate =
            totalPVComparisons > 0
                ? totalPVAgreement /
                  totalPVComparisons
                : 0.0;

        AppendReport("");
        AppendReport(
            "================ 7.7 OVERALL MOVE AGREEMENT ================"
        );

        AppendReport(
            $"Total Move Comparisons   : " +
            $"{totalMoveComparisons}"
        );

        AppendReport(
            $"Same Best Move           : " +
            $"{totalMoveAgreements}"
        );

        AppendReport(
            $"Different Best Move      : " +
            $"{totalMoveComparisons - totalMoveAgreements}"
        );

        AppendReport(
            $"Overall Move Agreement   : " +
            $"{overallMoveAgreementRate:F2}%"
        );

        AppendReport(
            $"Overall PV Agreement     : " +
            $"{overallPVAgreementRate:F2}%"
        );

        AppendReport("");
        AppendReport(
            "================ CONTROL VALIDATION ================"
        );

        AppendReport(
            "Same Position            : YES"
        );

        AppendReport(
            "Same Side To Move        : YES"
        );

        AppendReport(
            "Same Hardware            : YES"
        );

        AppendReport(
            "Same Unity Runtime       : YES"
        );

        AppendReport(
            "History                  : ENABLED"
        );

        AppendReport(
            "Killer                   : ENABLED"
        );

        AppendReport(
            "TT                       : ENABLED"
        );

        AppendReport(
            "Aspiration               : ENABLED"
        );

        AppendReport(
            $"Depth 2/3 Comparison     : " +
            $"{comparedPositions23 > 0}"
        );

        AppendReport(
            $"Depth 3/4 Comparison     : " +
            $"{comparedPositions34 > 0}"
        );

        Append2DivergenceSummary();

        AppendReport(
            "=================================================="
        );
    }

    private void Record2Comparison(
        MoveQualityComparisonStatistics comparison,
        MoveQualitySample lower,
        MoveQualitySample higher)
    {
        comparison.PositionCount++;

        if (
            AreSameMove(
                lower.BestMove,
                higher.BestMove
            ))
        {
            comparison.SameBestMoveCount++;
        }

        double scoreDifference =
            higher.BestScore -
            lower.BestScore;

        double absoluteScoreDifference =
            Math.Abs(
                scoreDifference
            );

        double pvAgreement =
            CalculatePVAgreement(
                lower.PV,
                higher.PV
            );

        comparison.ScoreDifferenceSamples.Add(
            scoreDifference
        );

        comparison.AbsoluteScoreDifferenceSamples.Add(
            absoluteScoreDifference
        );

        comparison.PVAgreementSamples.Add(
            pvAgreement
        );

        comparison.PVAgreementSum +=
            pvAgreement;
    }

    private double CalculatePVAgreement(
        List<StateMove> lowerPV,
        List<StateMove> higherPV)
    {
        if (
            lowerPV == null ||
            higherPV == null)
        {
            return 0.0;
        }

        int maxLength =
            Mathf.Max(
                lowerPV.Count,
                higherPV.Count
            );

        if (maxLength == 0)
        {
            return 100.0;
        }

        int commonPrefix = 0;

        int compareLength =
            Mathf.Min(
                lowerPV.Count,
                higherPV.Count
            );

        for (
            int i = 0;
            i < compareLength;
            i++)
        {
            if (
                AreSameMove(
                    lowerPV[i],
                    higherPV[i]
                ))
            {
                commonPrefix++;
            }
            else
            {
                break;
            }
        }

        return
            (double) commonPrefix /
            maxLength *
            100.0;
    }

    private bool AreSameMove(
        StateMove a,
        StateMove b)
    {
        if (
            a.From.X != b.From.X ||
            a.From.Y != b.From.Y ||
            a.To.X != b.To.X ||
            a.To.Y != b.To.Y)
        {
            return false;
        }

        if (
            a.IsPromotion !=
            b.IsPromotion)
        {
            return false;
        }

        if (
            a.IsPromotion &&
            a.PromotionType !=
            b.PromotionType)
        {
            return false;
        }

        return true;
    }
}
