using Lfm.Core.Models;

namespace Lfm.Core.Services;

public interface IPlaylistInputParser
{
    /// <summary>
    /// Parse comma-separated track list: "artist,track;artist2,track2"
    /// </summary>
    ValidationResult<List<TrackRequest>> ParseCommaSeparated(string input);

    /// <summary>
    /// Parse JSON track list: [{"artist": "...", "track": "..."}, ...]
    /// </summary>
    ValidationResult<List<TrackRequest>> ParseJson(string json);

    /// <summary>
    /// Validate individual track request
    /// </summary>
    ValidationResult ValidateTrackRequest(TrackRequest track);
}

public class ValidationResult<T>
{
    public bool IsValid { get; init; }
    public T? Data { get; init; }
    public List<string> Errors { get; init; } = new();

    public static ValidationResult<T> Success(T data) => new() { IsValid = true, Data = data };
    public static ValidationResult<T> Failure(params string[] errors) => new() { IsValid = false, Errors = errors.ToList() };
}