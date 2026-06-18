namespace Fathom;

public enum PredictionStatus
{
    Open,
    Happened,
    DidNotHappen,
    Cancelled
}

public record Prediction(
    int Id,
    string Statement,
    int Confidence,
    DateOnly Created,
    DateOnly? ResolveBy,
    PredictionStatus Status);
