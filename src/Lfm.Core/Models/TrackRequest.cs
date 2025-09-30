namespace Lfm.Core.Models;

public class TrackRequest
{
    public string Artist { get; init; } = string.Empty;
    public string Track { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;

    public TrackRequest(string artist, string track, string source = "")
    {
        Artist = artist?.Trim() ?? string.Empty;
        Track = track?.Trim() ?? string.Empty;
        Source = source;
    }

    public bool IsValid => !string.IsNullOrWhiteSpace(Artist) && !string.IsNullOrWhiteSpace(Track);

    public override string ToString() => $"{Artist} - {Track}";
}

public class ValidationResult
{
    public bool IsValid { get; init; }
    public List<string> Errors { get; init; } = new();

    public static ValidationResult Success() => new() { IsValid = true };
    public static ValidationResult Failure(params string[] errors) => new() { IsValid = false, Errors = errors.ToList() };
}