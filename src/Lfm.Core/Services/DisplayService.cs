using Lfm.Core.Models;
using Lfm.Core.Models.Results;
using static Lfm.Core.Configuration.SearchConstants;

namespace Lfm.Core.Services;

public interface IDisplayService
{
    // Existing data display methods
    void DisplayArtists(List<Artist> artists, int startRank);
    void DisplayTracksForUser(List<Track> tracks, int startRank);
    void DisplayTracksForArtist(List<Track> tracks, int startRank);
    void DisplayAlbums(List<Album> albums, int startRank);
    void DisplayRangeInfo(string itemType, int startIndex, int endIndex, int actualCount, string total, bool verbose = false);
    void DisplayTotalInfo(string itemType, string total, bool verbose = false);
    
    // New standardized progress and status methods
    void DisplayOperationStart(string operation, string user, string period, int? limit = null, int? startIndex = null, int? endIndex = null, bool verbose = false);
    void DisplayAnalysisProgress(string itemType, int analyzed, int found, int? maxItems = null, bool verbose = false);
    void DisplayOperationComplete(string itemType, int totalSearched, bool verbose = false);
    
    // Error and status messaging
    void DisplayError(string message);
    void DisplayValidationError(string message);
    void DisplayWarning(string message);
    void DisplaySuccess(string message);
    
    // Result-based error display
    void DisplayError(ErrorResult error, ISymbolProvider symbols);
    void DisplayResult<T>(Result<T> result, ISymbolProvider symbols, Action<T>? onSuccess = null);
    void DisplayResult(Result result, ISymbolProvider symbols, Action? onSuccess = null);
    
    // Timing and performance display
    void DisplayTimingResults(List<TimingResult> results, ISymbolProvider symbols);
    void DisplayExecutionTime(long milliseconds, ISymbolProvider symbols);
    
    // Search-specific display methods
    void DisplaySearchStart(string itemType, string artist, bool unlimited, int? maxItems = null, int? timeout = null, bool verbose = false);
    void DisplaySearchCancelled(bool userCancelled, int timeoutSeconds, int itemsSearched, int matchesFound, ISymbolProvider symbols);
    void DisplaySearchProgress(int searched, int found, string itemType, bool verbose = false);
    
    // Complex results display
    void DisplayRecommendations(List<RecommendationResult> recommendations, int filter, int tracksPerArtist, bool verbose, ISymbolProvider symbols);
}

// Timing result model for display
public class TimingResult
{
    public string Method { get; set; } = string.Empty;
    public bool CacheHit { get; set; }
    public long ElapsedMs { get; set; }
    public string Details { get; set; } = string.Empty;
}

public class DisplayService : IDisplayService
{
    public void DisplayArtists(List<Artist> artists, int startRank)
    {
        Console.WriteLine();
        Console.WriteLine($"{"Rank",-4} {"Artist",-40} {"Plays",-10}");
        Console.WriteLine(new string('-', 60));

        for (int i = 0; i < artists.Count; i++)
        {
            var artist = artists[i];
            var rank = (startRank + i).ToString();
            var plays = int.TryParse(artist.PlayCount, out var playCount) ? playCount.ToString("N0") : artist.PlayCount;
            
            Console.WriteLine($"{rank,-4} {TruncateString(artist.Name, Display.ArtistNameMaxLength),-40} {plays,-10}");
        }
        Console.WriteLine();
    }

    public void DisplayTracksForUser(List<Track> tracks, int startRank)
    {
        Console.WriteLine();
        Console.WriteLine($"{"Rank",-4} {"Track",-40} {"Artist",-30} {"Plays",-10}");
        Console.WriteLine(new string('-', 90));

        for (int i = 0; i < tracks.Count; i++)
        {
            var track = tracks[i];
            var rank = (startRank + i).ToString();
            var plays = int.TryParse(track.PlayCount, out var playCount) ? playCount.ToString("N0") : track.PlayCount;
            
            Console.WriteLine($"{rank,-4} {TruncateString(track.Name, Display.TrackNameMaxLength),-40} {TruncateString(track.Artist.Name, Display.SecondaryArtistNameMaxLength),-30} {plays,-10}");
        }
        Console.WriteLine();
    }

    public void DisplayTracksForArtist(List<Track> tracks, int startRank)
    {
        Console.WriteLine();
        Console.WriteLine($"{"Rank",-4} {"Track",-40} {"Artist",-30}");
        Console.WriteLine(new string('-', 80));

        for (int i = 0; i < tracks.Count; i++)
        {
            var track = tracks[i];
            var rank = (startRank + i).ToString();
            
            Console.WriteLine($"{rank,-4} {TruncateString(track.Name, Display.TrackNameMaxLength),-40} {TruncateString(track.Artist.Name, Display.SecondaryArtistNameMaxLength),-30}");
        }
        Console.WriteLine();
    }

    public void DisplayAlbums(List<Album> albums, int startRank)
    {
        Console.WriteLine();
        Console.WriteLine($"{"Rank",-4} {"Album",-40} {"Artist",-30} {"Plays",-10}");
        Console.WriteLine(new string('-', 90));

        for (int i = 0; i < albums.Count; i++)
        {
            var album = albums[i];
            var rank = (startRank + i).ToString();
            var plays = int.TryParse(album.PlayCount, out var playCount) ? playCount.ToString("N0") : album.PlayCount;
            
            Console.WriteLine($"{rank,-4} {TruncateString(album.Name, Display.AlbumNameMaxLength),-40} {TruncateString(album.Artist.Name, Display.SecondaryArtistNameMaxLength),-30} {plays,-10}");
        }
        Console.WriteLine();
    }

    public void DisplayRangeInfo(string itemType, int startIndex, int endIndex, int actualCount, string total, bool verbose = false)
    {
        if (verbose)
        {
            Console.WriteLine($"\nShowing {itemType} {startIndex}-{Math.Min(endIndex, startIndex + actualCount - 1)} of {total}");
        }
    }

    public void DisplayTotalInfo(string itemType, string total, bool verbose = false)
    {
        if (verbose)
        {
            Console.WriteLine($"\nTotal {itemType}: {total}");
        }
    }

    // New standardized progress and status methods
    public void DisplayOperationStart(string operation, string user, string period, int? limit = null, int? startIndex = null, int? endIndex = null, bool verbose = false)
    {
        if (!verbose) return;

        var message = operation switch
        {
            "artists" when startIndex.HasValue && endIndex.HasValue => 
                $"Getting artists {startIndex}-{endIndex} for {user} ({period})...",
            "tracks" when startIndex.HasValue && endIndex.HasValue => 
                $"Getting tracks {startIndex}-{endIndex} for {user} ({period})...",
            "albums" when startIndex.HasValue && endIndex.HasValue => 
                $"Getting albums {startIndex}-{endIndex} for {user} ({period})...",
            "artists" => 
                $"Getting top {limit} artists for {user} ({period})...",
            "tracks" => 
                $"Getting top {limit} tracks for {user} ({period})...",
            "albums" => 
                $"Getting top {limit} albums for {user} ({period})...",
            "recommendations" => 
                $"Analyzing top {limit} artists for {user} ({period})...",
            _ => 
                $"Processing {operation} for {user} ({period})..."
        };

        Console.WriteLine($"{message}\n");
    }

    public void DisplayAnalysisProgress(string itemType, int analyzed, int found, int? maxItems = null, bool verbose = false)
    {
        if (!verbose) return;

        var progressMessage = maxItems.HasValue 
            ? $"Analyzed {analyzed} {itemType}, found {found} matches (max: {maxItems})"
            : $"Analyzed {analyzed} {itemType}, found {found} matches";
            
        Console.WriteLine(progressMessage);
    }

    public void DisplayOperationComplete(string itemType, int totalSearched, bool verbose = false)
    {
        if (!verbose) return;
        Console.WriteLine($"Search complete: {totalSearched} {itemType} processed");
    }

    // Error and status messaging
    public void DisplayError(string message)
    {
        Console.WriteLine(message);
    }

    public void DisplayValidationError(string message)
    {
        Console.WriteLine(message);
    }

    public void DisplayWarning(string message)
    {
        Console.WriteLine(message);
    }

    public void DisplaySuccess(string message)
    {
        Console.WriteLine(message);
    }

    // Result-based error display
    public void DisplayError(ErrorResult error, ISymbolProvider symbols)
    {
        var symbol = error.GetDisplaySymbol(symbols != null);
        Console.WriteLine($"{symbol} {error.Message}");
        
        if (!string.IsNullOrEmpty(error.TechnicalDetails))
        {
            Console.WriteLine($"   Technical details: {error.TechnicalDetails}");
        }

        // Provide helpful hints based on error type
        if (symbols != null)
        {
            switch (error.Type)
            {
                case ErrorType.ConfigurationError:
                    Console.WriteLine($"   {symbols.Tip} Use 'lfm config set api-key YOUR_KEY' to configure your API key");
                    break;
                case ErrorType.ValidationError:
                    Console.WriteLine($"   {symbols.Tip} Check your command parameters and try again");
                    break;
                case ErrorType.RateLimitError:
                    Console.WriteLine($"   {symbols.Tip} Wait a moment and try again, or use --delay to throttle requests");
                    break;
                case ErrorType.NetworkError:
                    Console.WriteLine($"   {symbols.Tip} Check your internet connection and try again");
                    break;
            }
        }
    }

    public void DisplayResult<T>(Result<T> result, ISymbolProvider symbols, Action<T>? onSuccess = null)
    {
        if (result.Success && result.Data != null)
        {
            onSuccess?.Invoke(result.Data);
        }
        else if (result.Error != null)
        {
            DisplayError(result.Error, symbols);
        }
    }

    public void DisplayResult(Result result, ISymbolProvider symbols, Action? onSuccess = null)
    {
        if (result.Success)
        {
            onSuccess?.Invoke();
        }
        else if (result.Error != null)
        {
            DisplayError(result.Error, symbols);
        }
    }

    // Timing and performance display
    public void DisplayTimingResults(List<TimingResult> results, ISymbolProvider symbols)
    {
        if (!results.Any()) return;

        Console.WriteLine($"\n{symbols.Timer} API Timing Results:");
        Console.WriteLine("Method              | Cache | Time (ms) | Details");
        Console.WriteLine("--------------------+-------+-----------+----------");
        
        var totalTime = 0L;
        var cacheHits = 0;
        var totalCalls = results.Count;
        
        foreach (var timing in results)
        {
            var status = timing.CacheHit ? " HIT " : "MISS";
            Console.WriteLine($"{timing.Method,-19} | {status} | {timing.ElapsedMs,8} | {timing.Details}");
            totalTime += timing.ElapsedMs;
            if (timing.CacheHit) cacheHits++;
        }
        
        Console.WriteLine("--------------------+-------+-----------+----------");
        Console.WriteLine($"Total: {totalCalls} calls, {cacheHits} cache hits ({(double)cacheHits/totalCalls*100:F1}%), {totalTime}ms");
    }

    public void DisplayExecutionTime(long milliseconds, ISymbolProvider symbols)
    {
        Console.WriteLine($"\n{symbols.Timer} Total execution time: {milliseconds}ms");
    }

    // Search-specific display methods
    public void DisplaySearchStart(string itemType, string artist, bool unlimited, int? maxItems = null, int? timeout = null, bool verbose = false)
    {
        if (!verbose) return;

        Console.WriteLine($"Getting your top {itemType} by {artist}...");
        if (unlimited)
        {
            Console.WriteLine($"(Unlimited search through ALL your {itemType})");
        }
        else if (maxItems.HasValue)
        {
            Console.WriteLine($"(Searching up to {maxItems} {itemType})");
        }
    }

    public void DisplaySearchCancelled(bool userCancelled, int timeoutSeconds, int itemsSearched, int matchesFound, ISymbolProvider symbols)
    {
        if (userCancelled)
        {
            Console.WriteLine($"\n{symbols.StopSign} Search cancelled by user.");
        }
        else
        {
            Console.WriteLine($"\n{symbols.Timer} Search timed out after {timeoutSeconds} seconds.");
        }
        Console.WriteLine($"Searched {itemsSearched} items, found {matchesFound} matches.");
    }

    public void DisplaySearchProgress(int searched, int found, string itemType, bool verbose = false)
    {
        if (!verbose) return;
        Console.WriteLine($"Searched {searched} {itemType}, found {found} matches so far...");
    }

    // Complex results display
    public void DisplayRecommendations(List<RecommendationResult> recommendations, int filter, int tracksPerArtist, bool verbose, ISymbolProvider symbols)
    {
        var playlistHeader = tracksPerArtist > 0 
            ? $"{symbols.Music} Playlist: Top {recommendations.Count} Artists with {tracksPerArtist} tracks each (filter: >= {filter} plays)\n"
            : $"{symbols.Music} Top {recommendations.Count} Recommendations (filter: >= {filter} plays)\n";
        
        Console.WriteLine($"\n{playlistHeader}");
        
        var index = 1;
        var totalTracks = 0;
        
        foreach (var rec in recommendations)
        {
            Console.WriteLine($"{index,3}. {rec.ArtistName}");
            Console.WriteLine($"     Match: {rec.AverageSimilarity:P0} | Similar to {rec.OccurrenceCount} of your top artists");

            if (rec.SourceArtists.Count > 0)
            {
                if (verbose)
                {
                    // In verbose mode, show all artists
                    Console.WriteLine($"     Similar to: {string.Join(", ", rec.SourceArtists)}");
                }
                else if (rec.SourceArtists.Count <= 5)
                {
                    // Show all artists when 5 or fewer
                    Console.WriteLine($"     Similar to: {string.Join(", ", rec.SourceArtists)}");
                }
                else
                {
                    // Show first 5 with "and X more" format when more than 5
                    var firstFive = rec.SourceArtists.Take(5);
                    var remaining = rec.SourceArtists.Count - 5;
                    Console.WriteLine($"     Similar to: {string.Join(", ", firstFive)}... and {remaining} more");
                }
            }

            Console.WriteLine($"     Your plays: {rec.UserPlayCount}");

            // Display tracks if requested
            if (tracksPerArtist > 0 && rec.TopTracks != null)
            {
                Console.WriteLine($"     Top tracks:");
                var trackIndex = 1;
                foreach (var track in rec.TopTracks.Take(tracksPerArtist))
                {
                    Console.WriteLine($"       {trackIndex}. {track.Name} ({track.PlayCount} plays)");
                    trackIndex++;
                    totalTracks++;
                }
            }
            
            Console.WriteLine();
            index++;
        }

        if (tracksPerArtist > 0)
        {
            Console.WriteLine($"{symbols.Clipboard} Playlist Summary: {totalTracks} tracks from {recommendations.Count} artists");
        }
        
        if (verbose)
        {
            var totalFound = recommendations.Count;
            Console.WriteLine($"Statistics: Found {totalFound} recommendations after filtering");
        }
    }

    private static string TruncateString(string str, int maxLength)
    {
        if (string.IsNullOrEmpty(str) || str.Length <= maxLength)
            return str;
        
        return str[..(maxLength - Display.TruncationSuffixLength)] + Display.TruncationSuffix;
    }
}