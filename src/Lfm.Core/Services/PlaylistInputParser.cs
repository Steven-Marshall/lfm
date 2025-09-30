using System.Text.Json;
using System.Text.Json.Serialization;
using Lfm.Core.Models;

namespace Lfm.Core.Services;

public class PlaylistInputParser : IPlaylistInputParser
{
    public ValidationResult<List<TrackRequest>> ParseCommaSeparated(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return ValidationResult<List<TrackRequest>>.Failure("Input cannot be empty");

        var tracks = new List<TrackRequest>();
        var errors = new List<string>();

        var trackEntries = input.Split(';', StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < trackEntries.Length; i++)
        {
            var entry = trackEntries[i].Trim();
            var source = $"entry {i + 1}";

            var parts = entry.Split(',', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 2)
            {
                errors.Add($"{source}: Expected format 'artist,track' but got '{entry}'");
                continue;
            }

            var track = new TrackRequest(parts[0], parts[1], source);
            var validation = ValidateTrackRequest(track);

            if (!validation.IsValid)
            {
                errors.AddRange(validation.Errors.Select(e => $"{source}: {e}"));
                continue;
            }

            tracks.Add(track);
        }

        if (errors.Any())
            return ValidationResult<List<TrackRequest>>.Failure(errors.ToArray());

        if (!tracks.Any())
            return ValidationResult<List<TrackRequest>>.Failure("No valid tracks found in input");

        return ValidationResult<List<TrackRequest>>.Success(tracks);
    }

    public ValidationResult<List<TrackRequest>> ParseJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return ValidationResult<List<TrackRequest>>.Failure("JSON input cannot be empty");

        try
        {
            var jsonTracks = JsonSerializer.Deserialize<JsonTrack[]>(json);

            if (jsonTracks == null || jsonTracks.Length == 0)
                return ValidationResult<List<TrackRequest>>.Failure("No tracks found in JSON");

            var tracks = new List<TrackRequest>();
            var errors = new List<string>();

            for (int i = 0; i < jsonTracks.Length; i++)
            {
                var jsonTrack = jsonTracks[i];
                var source = $"track {i + 1}";

                var track = new TrackRequest(jsonTrack.Artist ?? "", jsonTrack.Track ?? "", source);
                var validation = ValidateTrackRequest(track);

                if (!validation.IsValid)
                {
                    errors.AddRange(validation.Errors.Select(e => $"{source}: {e}"));
                    continue;
                }

                tracks.Add(track);
            }

            if (errors.Any())
                return ValidationResult<List<TrackRequest>>.Failure(errors.ToArray());

            if (!tracks.Any())
                return ValidationResult<List<TrackRequest>>.Failure("No valid tracks found in JSON");

            return ValidationResult<List<TrackRequest>>.Success(tracks);
        }
        catch (JsonException ex)
        {
            return ValidationResult<List<TrackRequest>>.Failure($"Invalid JSON format: {ex.Message}");
        }
    }

    public ValidationResult ValidateTrackRequest(TrackRequest track)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(track.Artist))
            errors.Add("Artist name cannot be empty");

        if (string.IsNullOrWhiteSpace(track.Track))
            errors.Add("Track name cannot be empty");

        if (track.Artist.Length > 200)
            errors.Add("Artist name too long (max 200 characters)");

        if (track.Track.Length > 200)
            errors.Add("Track name too long (max 200 characters)");

        return errors.Any() ? ValidationResult.Failure(errors.ToArray()) : ValidationResult.Success();
    }

    private class JsonTrack
    {
        [JsonPropertyName("artist")]
        public string? Artist { get; set; }

        [JsonPropertyName("track")]
        public string? Track { get; set; }
    }
}