using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public partial class AIBenchmarkManager
{
    protected void AppendReport(
        string text)
    {
        _benchmarkReport.AppendLine(
            text
        );
    }

    protected string FormatMove(
        StateMove move)
    {
        return
            $"({move.From.X},{move.From.Y}) -> " +
            $"({move.To.X},{move.To.Y})";
    }

    protected double CalculateMean(
        List<double> values)
    {
        if (
            values == null ||
            values.Count == 0)
        {
            return 0.0;
        }

        double sum = 0.0;

        for (
            int i = 0;
            i < values.Count;
            i++)
        {
            sum += values[i];
        }

        return
            sum /
            values.Count;
    }

    protected double CalculateSampleStandardDeviation(
        List<double> values)
    {
        if (
            values == null ||
            values.Count < 2)
        {
            return 0.0;
        }

        double mean =
            CalculateMean(
                values
            );

        double sumSquared = 0.0;

        for (
            int i = 0;
            i < values.Count;
            i++)
        {
            double difference =
                values[i] - mean;

            sumSquared +=
                difference *
                difference;
        }

        return Math.Sqrt(
            sumSquared /
            (
                values.Count - 1
            )
        );
    }

    protected double CalculateConfidenceInterval95(
        List<double> values)
    {
        if (
            values == null ||
            values.Count < 2)
        {
            return 0.0;
        }

        int n =
            values.Count;

        double sampleStdDev =
            CalculateSampleStandardDeviation(
                values
            );

        double standardError =
            sampleStdDev /
            Math.Sqrt(n);

        double tCritical =
            GetTCritical95(
                n - 1
            );

        return
            tCritical *
            standardError;
    }

    protected double GetTCritical95(
        int degreesOfFreedom)
    {
        switch (degreesOfFreedom)
        {
            case 1: return 12.706;
            case 2: return 4.303;
            case 3: return 3.182;
            case 4: return 2.776;
            case 5: return 2.571;
            case 6: return 2.447;
            case 7: return 2.365;
            case 8: return 2.306;
            case 9: return 2.262;
            case 10: return 2.228;
            case 11: return 2.201;
            case 12: return 2.179;
            case 13: return 2.160;
            case 14: return 2.145;
            case 15: return 2.131;
            case 16: return 2.120;
            case 17: return 2.110;
            case 18: return 2.101;
            case 19: return 2.093;
            case 20: return 2.086;
            case 21: return 2.080;
            case 22: return 2.074;
            case 23: return 2.069;
            case 24: return 2.064;
            case 25: return 2.060;
            case 26: return 2.056;
            case 27: return 2.052;
            case 28: return 2.048;
            case 29: return 2.045;
            case 30: return 2.042;
            default: return 1.960;
        }
    }

    protected void ResetStatistics()
    {
        _ablationStatistics.Clear();
        _totalPositions = 0;
    }

    protected void FinishBenchmark()
    {
        _benchmarkRunning = false;
    }

    protected void SaveBenchmarkReportToFile()
    {
        try
        {
            string folder =
                Path.Combine(
                    Application.dataPath,
                    "Experimental results"
                );

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(
                    folder
                );
            }

            string timestamp =
                DateTime.Now.ToString(
                    "yyyyMMdd_HHmmss"
                );

            string fileName =
                $"AI_Benchmark_{timestamp}.txt";

            string filePath =
                Path.Combine(
                    folder,
                    fileName
                );

            File.WriteAllText(
                filePath,
                _benchmarkReport.ToString(),
                Encoding.UTF8
            );

            ShafranDebug.Log(
                "[AIBenchmark] Full report saved:\n" +
                filePath
            );
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[AIBenchmark] Failed to save report: " +
                exception.Message
            );
        }
    }
}
