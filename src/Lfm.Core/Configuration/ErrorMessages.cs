namespace Lfm.Core.Configuration;

/// <summary>
/// Centralized error messages for consistent user experience across the application
/// </summary>
public static class ErrorMessages
{
    // Configuration related errors
    public const string NoApiKey = "❌ No API key configured. Run 'lfm config set-api-key <your-api-key>' first.";
    public const string ApiKeyInfo = "Get your API key from: https://www.last.fm/api/account/create";
    public const string NoUsername = "❌ No username specified. Use --user option or set default with 'lfm config set-user <username>'";
    public const string EmptyApiKey = "❌ API key cannot be empty.";
    public const string EmptyUsername = "❌ Username cannot be empty.";
    
    // Range validation errors
    public const string InvalidRangeFormat = "❌ Invalid range format. Use format: --range 10-20";
    public const string InvalidRangeValues = "❌ Invalid range. Start must be >= 1 and end must be >= start";
    
    // Artist validation errors
    public const string EmptyArtistName = "❌ Artist name cannot be empty.";
    
    // Command specific errors
    public const string RangeNotSupportedWithArtist = "❌ Range option is not supported when using --artist. Use --limit instead.";
    
    // Data not found messages
    public const string NoArtistsFound = "No artists found.";
    public const string NoTracksFound = "No tracks found.";
    public const string NoAlbumsFound = "No albums found.";
    public const string NoItemsInRange = "No {0} found in the specified range.";
    public const string NoArtistItemsFound = "No {0} found in your listening history for artist: {1}";
    
    // Success messages
    public const string ApiKeySaved = "✅ API key saved successfully.";
    public const string UsernameSaved = "✅ Default username saved successfully.";
    public const string ConfigSavedTo = "Config saved to: {0}";
    
    // Informational messages
    public const string ArtistSearchSuggestion = "💡 Try using a different spelling or make sure you've listened to this artist";
    
    // Generic error format
    public const string GenericError = "❌ Error: {0}";
    
    /// <summary>
    /// Formats a message with parameters
    /// </summary>
    public static string Format(string message, params object[] args)
    {
        return string.Format(message, args);
    }
}