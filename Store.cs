using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fathom;

public class Store
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _path;

    public Store()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "fathom");
        _path = Path.Combine(dir, "predictions.json");
    }

    public List<Prediction> Load()
    {
        if (!File.Exists(_path))
        {
            return new List<Prediction>();
        }

        List<Prediction>? predictions;
        try
        {
            var json = File.ReadAllText(_path);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<Prediction>();
            }
            predictions = JsonSerializer.Deserialize<List<Prediction>>(json, JsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            throw new StoreException($"Couldn't read predictions from {_path}: {ex.Message}", ex);
        }

        if (predictions is null)
        {
            return new List<Prediction>();
        }

        Validate(predictions);
        return predictions;
    }

    public void Save(List<Prediction> predictions)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var json = JsonSerializer.Serialize(predictions, JsonOptions);

            var tmp = _path + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, _path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new StoreException($"Couldn't save predictions to {_path}: {ex.Message}", ex);
        }
    }

    private void Validate(List<Prediction> predictions)
    {
        var seenIds = new HashSet<int>();

        for (var i = 0; i < predictions.Count; i++)
        {
            var p = predictions[i];
            var where = $"entry #{i + 1} in {_path}";

            if (p is null)
            {
                throw new StoreException($"{where} is empty/null.", new InvalidDataException());
            }

            if (p.Id <= 0)
            {
                throw new StoreException($"{where} has an invalid id ({p.Id}); ids must be positive.",
                    new InvalidDataException());
            }

            if (!seenIds.Add(p.Id))
            {
                throw new StoreException($"{where} repeats id #{p.Id}; ids must be unique.",
                    new InvalidDataException());
            }

            if (string.IsNullOrWhiteSpace(p.Statement))
            {
                throw new StoreException($"prediction #{p.Id} has an empty statement.",
                    new InvalidDataException());
            }

            if (p.Confidence < 0 || p.Confidence > 100)
            {
                throw new StoreException(
                    $"prediction #{p.Id} has confidence {p.Confidence}; must be between 0 and 100.",
                    new InvalidDataException());
            }

            if (p.Statement.Length > 1000)
            {
                throw new StoreException(
                    $"prediction #{p.Id} has a statement of {p.Statement.Length} characters; max is 1000.",
                    new InvalidDataException());
            }
        }
    }
}

public class StoreException(string message, Exception inner) : Exception(message, inner) { }