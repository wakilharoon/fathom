namespace Fathom;

public record CalibrationBucket(
    int LowPercent,
    int HighPercent,
    int Count,
    double MeanConfidence,
    double CameTrueRate);

public record ScoreReport(
    int ResolvedCount,
    double BrierScore,
    double MeanConfidence,
    double CameTrueRate,
    double Reliability,
    double Resolution,
    double Uncertainty,
    IReadOnlyList<CalibrationBucket> Buckets);

public static class Scoring
{
    public static ScoreReport Compute(IEnumerable<Prediction> predictions)
    {
        var resolved = predictions
            .Where(p => p.Status is PredictionStatus.Happened or PredictionStatus.DidNotHappen)
            .ToList();

        if (resolved.Count == 0)
        {
            return new ScoreReport(0, 0, 0, 0, 0, 0, 0, new List<CalibrationBucket>());
        }

        double brierSum = 0;
        double confidenceSum = 0;
        var trueCount = 0;

        foreach (var p in resolved)
        {
            var prob = p.Confidence / 100.0;
            var outcome = p.Status == PredictionStatus.Happened ? 1.0 : 0.0;

            brierSum += (prob - outcome) * (prob - outcome);
            confidenceSum += p.Confidence;
            if (outcome == 1.0) trueCount++;
        }

        var brier = brierSum / resolved.Count;
        var meanConfidence = confidenceSum / resolved.Count;
        var cameTrueRate = 100.0 * trueCount / resolved.Count;

        var buckets = BuildBuckets(resolved);


        var baseRate = (double)trueCount / resolved.Count;
        var uncertainty = baseRate * (1 - baseRate);

        double reliability = 0;
        double resolution = 0;
        foreach (var b in buckets)
        {
            var forecast = b.MeanConfidence / 100.0;
            var observed = b.CameTrueRate / 100.0;
            reliability += b.Count * (forecast - observed) * (forecast - observed);
            resolution += b.Count * (observed - baseRate) * (observed - baseRate);
        }
        reliability /= resolved.Count;
        resolution /= resolved.Count;

        return new ScoreReport(
            resolved.Count, brier, meanConfidence, cameTrueRate,
            reliability, resolution, uncertainty, buckets);
    }

    private static List<CalibrationBucket> BuildBuckets(List<Prediction> resolved)
    {
        var groups = resolved
            .GroupBy(p => Math.Min(p.Confidence / 10, 9))
            .OrderBy(g => g.Key);

        var buckets = new List<CalibrationBucket>();
        foreach (var g in groups)
        {
            var members = g.ToList();
            var low = g.Key * 10;
            var meanConf = members.Average(p => (double)p.Confidence);
            var cameTrue = 100.0 * members.Count(p => p.Status == PredictionStatus.Happened) / members.Count;

            buckets.Add(new CalibrationBucket(low, low + 10, members.Count, meanConf, cameTrue));
        }
        return buckets;
    }
}