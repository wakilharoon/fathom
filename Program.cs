using Fathom;
using System.CommandLine;
using System.Globalization;

// ADD

var statementArg = new Argument<string>("statement")
{
    Description = "The prediction, phrased so it can resolve by yes or no"
};

var confidenceOption = new Option<int>("--confidence", "-c")
{
    Description = "How confident you are the statement is true (0–100)",
    Required = true,
    CustomParser = result =>
    {
        var raw = result.Tokens[0].Value;
        
        if (!int.TryParse(raw, out var value))
        {
            result.AddError($"Confidence must be a whole number, but got '{raw}'.");
            return 0;
        }
        
        if (value < 0 || value > 100)
        {
            result.AddError($"Confidence must be between 0 and 100, but got {value}.");
            return 0;
        }

        return value;
    }
};

var dateExample = new DateOnly(2026, 12, 31).ToString("d", CultureInfo.CurrentCulture);

var byOption = new Option<DateOnly?>("--by")
{
    Description = $"Date you expect to know the outcome, in your local format (e.g. {dateExample})",
    CustomParser = result =>
    {
        var raw = result.Tokens[0].Value;

        if (DateOnly.TryParse(raw, CultureInfo.CurrentCulture, out var date))
        {
            return date;
        }

        result.AddError($"Couldn't read '{raw}' as a date. Use your local format, e.g. {dateExample}.");
        return null;
    }
};

var addCommand = new Command("add", "Record a new prediction")
{
    Arguments = { statementArg },
    Options = { confidenceOption, byOption }
};

addCommand.SetAction(parseResult =>
{
    var statement = parseResult.GetValue(statementArg)!;
    var confidence = parseResult.GetValue(confidenceOption);
    var by = parseResult.GetValue(byOption);

    return WithPredictions((predictions, store) =>
    {
        var nextId = predictions.Count == 0 ? 1 : predictions.Max(p => p.Id) + 1;

        var prediction = new Prediction(
            Id: nextId,
            Statement: statement,
            Confidence: confidence,
            Created: DateOnly.FromDateTime(DateTime.Now),
            ResolveBy: by,
            Status: PredictionStatus.Open
        );

        predictions.Add(prediction);
        store.Save(predictions);

        Console.WriteLine($"Added #{prediction.Id}: \"{statement}\" at {confidence}% confidence"
                          + (by is DateOnly d ? $", resolve by {d:d}" : ""));
        return 0;
    });
});

// OPEN

var openCommand = new Command("open", "List every unresolved prediction, dated or not");

openCommand.SetAction(_ => WithPredictions((predictions, _) =>
{
    var today = DateOnly.FromDateTime(DateTime.Now);

    var open = predictions
        .Where(p => p.Status == PredictionStatus.Open)
        .OrderBy(p => p.ResolveBy is null)            // dated ones first…
        .ThenBy(p => p.ResolveBy)                     // …oldest date at the top
        .ThenBy(p => p.Id)                            // stable order for the undated tail
        .ToList();

    if (open.Count == 0)
    {
        Console.WriteLine("No open predictions. Nothing hanging over you.");
        return 0;
    }

    Console.WriteLine($"{open.Count} open prediction(s):");
    Console.WriteLine();

    foreach (var p in open)
    {
        string when;
        if (p.ResolveBy is DateOnly date)
        {
            var days = today.DayNumber - date.DayNumber;
            when = days > 0 ? $"due {days} day(s) ago"
                 : days == 0 ? "due today"
                 : $"due in {-days} day(s)";
        }
        else
        {
            when = "no date set";
        }

        Console.WriteLine($"  #{p.Id}  {p.Confidence,3}%  {p.Statement}");
        Console.WriteLine($"         {when}");
    }

    return 0;
}));

// RESOLVE

var idArg = new Argument<int>("id")
{
    Description = "Which prediction to resolve (see 'fathom due')"
};

var outcomeArg = new Argument<bool>("outcome")
{
    Description = "Did it happen? yes/no (also y/n, true/false)",
    CustomParser = result =>
    {
        var raw = result.Tokens[0].Value.Trim().ToLowerInvariant();
        switch (raw)
        {
            case "yes":
            case "y":
            case "true":
                return true;
            case "no":
            case "n":
            case "false":
                return false;
            default:
                result.AddError($"Outcome must be yes or no, but got '{result.Tokens[0].Value}'.");
                return false;
        }
    }
};

var resolveCommand = new Command("resolve", "Record whether a prediction came true")
{
    Arguments = { idArg, outcomeArg }
};

resolveCommand.SetAction(parseResult =>
{
    var id = parseResult.GetValue(idArg);
    var happened = parseResult.GetValue(outcomeArg);

    return WithPredictions((predictions, store) =>
    {
        var index = predictions.FindIndex(p => p.Id == id);
        if (index < 0)
        {
            Console.Error.WriteLine($"fathom: no prediction with id #{id}.");
            return 1;
        }

        var prediction = predictions[index];
        if (prediction.Status != PredictionStatus.Open)
        {
            Console.Error.WriteLine($"fathom: #{id} is already resolved ({prediction.Status}).");
            return 1;
        }

        var newStatus = happened ? PredictionStatus.Happened : PredictionStatus.DidNotHappen;
        predictions[index] = prediction with { Status = newStatus };
        store.Save(predictions);

        Console.WriteLine($"Resolved #{id}: \"{prediction.Statement}\" → {newStatus}");
        return 0;
    });
});

// UNRESOLVE

var unresolveIdArg = new Argument<int>("id")
{
    Description = "Which prediction to send back to open (to fix a wrong resolution)"
};

var unresolveCommand = new Command("unresolve", "Reopen a resolved prediction so you can resolve it again")
{
    Arguments = { unresolveIdArg }
};

unresolveCommand.SetAction(parseResult =>
{
    var id = parseResult.GetValue(unresolveIdArg);

    return WithPredictions((predictions, store) =>
    {
        var index = predictions.FindIndex(p => p.Id == id);
        if (index < 0)
        {
            Console.Error.WriteLine($"fathom: no prediction with id #{id}.");
            return 1;
        }

        var prediction = predictions[index];
        if (prediction.Status == PredictionStatus.Open)
        {
            Console.Error.WriteLine($"fathom: #{id} is already open; nothing to undo.");
            return 1;
        }

        predictions[index] = prediction with { Status = PredictionStatus.Open };
        store.Save(predictions);

        Console.WriteLine($"Reopened #{id}: \"{prediction.Statement}\" (was {prediction.Status}).");
        return 0;
    });
});

// CANCEL

var cancelIdArg = new Argument<int>("id")
{
    Description = "Which prediction to cancel (became unanswerable; won't count toward your score)"
};

var cancelCommand = new Command("cancel", "Mark a prediction unanswerable so it leaves your lists without scoring")
{
    Arguments = { cancelIdArg }
};

cancelCommand.SetAction(parseResult =>
{
    var id = parseResult.GetValue(cancelIdArg);

    return WithPredictions((predictions, store) =>
    {
        var index = predictions.FindIndex(p => p.Id == id);
        if (index < 0)
        {
            Console.Error.WriteLine($"fathom: no prediction with id #{id}.");
            return 1;
        }

        var prediction = predictions[index];
        if (prediction.Status == PredictionStatus.Cancelled)
        {
            Console.Error.WriteLine($"fathom: #{id} is already cancelled.");
            return 1;
        }

        predictions[index] = prediction with { Status = PredictionStatus.Cancelled };
        store.Save(predictions);

        Console.WriteLine($"Cancelled #{id}: \"{prediction.Statement}\" (was {prediction.Status}).");
        return 0;
    });
});

// SCORE

var scoreCommand = new Command("score", "Show your calibration and accuracy");

scoreCommand.SetAction(_ => WithPredictions((predictions, _) =>
{
    var report = Scoring.Compute(predictions);

    if (report.ResolvedCount == 0)
    {
        Console.WriteLine("No resolved predictions yet. Resolve some with 'fathom resolve <id> yes|no'.");
        return 0;
    }

    const int minForVerdict = 10;

    Console.WriteLine($"Score  ({report.ResolvedCount} resolved prediction(s))");
    Console.WriteLine();
    Console.WriteLine($"Brier score: {report.BrierScore:F3}   (0 = perfect, 0.25 = coin flip, 1 = always confidently wrong)");
    Console.WriteLine();

    Console.WriteLine("Calibration  (when you say X%, does X% actually happen?)");
    Console.WriteLine("  confidence     n    you said   came true");
    foreach (var b in report.Buckets)
    {
        var note = b.Count < 5 ? "   (small sample)" : "";
        Console.WriteLine(
            $"  {b.LowPercent,3}–{b.HighPercent,3}%  {b.Count,4}      {b.MeanConfidence,3:F0}%       {b.CameTrueRate,3:F0}%{note}");
    }
    Console.WriteLine();

    if (report.ResolvedCount >= minForVerdict)
    {
        var said = report.MeanConfidence;
        var right = report.CameTrueRate;
        var gap = said - right;

        if (Math.Abs(gap) < 5)
            Console.WriteLine($"  In aggregate you said {said:F0}% and {right:F0}% came true — nicely calibrated.");
        else if (gap > 0)
            Console.WriteLine($"  In aggregate you said {said:F0}% but only {right:F0}% came true — you lean overconfident.");
        else
            Console.WriteLine($"  In aggregate you said {said:F0}% but {right:F0}% came true — you lean underconfident.");
        Console.WriteLine();
    }

    Console.WriteLine("Discrimination  (do your confidence levels separate what happens from what doesn't?)");
    Console.WriteLine($"  Resolution:  {report.Resolution:F3}   (higher is better)");
    Console.WriteLine($"  Uncertainty: {report.Uncertainty:F3}   (inherent difficulty; resolution can't exceed this)");

    if (report.Uncertainty == 0)
    {
        Console.WriteLine("  Every resolved prediction had the same outcome, so there's nothing to discriminate yet.");
    }
    else if (report.ResolvedCount >= minForVerdict)
    {
        var captured = 100.0 * report.Resolution / report.Uncertainty;
        Console.WriteLine($"  You captured roughly {captured:F0}% of the available signal.");
        if (captured < 10)
            Console.WriteLine("  Near zero means your confidences barely distinguish outcomes — the 'same number to everything' trap.");
    }

    if (report.ResolvedCount < minForVerdict)
    {
        Console.WriteLine();
        Console.WriteLine($"Too few resolved predictions ({report.ResolvedCount}) to judge reliably — aim for at least {minForVerdict}.");
    }

    return 0;
}));

// DUE

var dueCommand = new Command("due", "List predictions ready to resolve");

dueCommand.SetAction(_ => WithPredictions((predictions, _) =>
{
    var today = DateOnly.FromDateTime(DateTime.Now);

    var due = predictions
        .Where(p => p.Status == PredictionStatus.Open
                    && p.ResolveBy is DateOnly date
                    && date <= today)
        .OrderBy(p => p.ResolveBy)
        .ToList();

    if (due.Count == 0)
    {
        Console.WriteLine("Nothing ready to resolve. You're all caught up.");
        return 0;
    }

    Console.WriteLine($"{due.Count} prediction(s) ready to resolve:");
    Console.WriteLine();

    foreach (var p in due)
    {
        var dueDate = p.ResolveBy!.Value;
        var days = today.DayNumber - dueDate.DayNumber;
        var when = days == 0 ? "today"
                 : days == 1 ? "yesterday"
                 : $"{days} days ago";

        Console.WriteLine($"  #{p.Id}  {p.Confidence,3}%  {p.Statement}");
        Console.WriteLine($"         due {when} ({dueDate:d})");
    }

    return 0;
}));

// ROOT

var rootCommand = new RootCommand("fathom: log predictions and measure how well-calibrated you are")
{
    Subcommands = { addCommand, openCommand, scoreCommand, dueCommand, resolveCommand, unresolveCommand, cancelCommand }
};

return rootCommand.Parse(args).Invoke();


static int WithPredictions(Func<List<Prediction>, Store, int> work)
{
    var store = new Store();
    try
    {
        var predictions = store.Load();
        return work(predictions, store);
    }
    catch (StoreException ex)
    {
        Console.Error.WriteLine($"fathom: {ex.Message}");
        return 1;
    }
}
