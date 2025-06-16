using Lfm.Core.Models;
using static Lfm.Core.Configuration.SearchConstants;

namespace Lfm.Core.Services;

public interface IDisplayService
{
    void DisplayArtists(List<Artist> artists, int startRank);
    void DisplayTracksForUser(List<Track> tracks, int startRank);
    void DisplayTracksForArtist(List<Track> tracks, int startRank);
    void DisplayAlbums(List<Album> albums, int startRank);
    void DisplayRangeInfo(string itemType, int startIndex, int endIndex, int actualCount, string total, bool verbose = false);
    void DisplayTotalInfo(string itemType, string total, bool verbose = false);
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

    private static string TruncateString(string str, int maxLength)
    {
        if (string.IsNullOrEmpty(str) || str.Length <= maxLength)
            return str;
        
        return str[..(maxLength - Display.TruncationSuffixLength)] + Display.TruncationSuffix;
    }
}