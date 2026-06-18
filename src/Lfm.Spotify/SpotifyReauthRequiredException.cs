namespace Lfm.Spotify;

/// Thrown when Spotify needs re-authentication and the current context cannot
/// run the interactive OAuth flow (refresh token expired or revoked, plus stdin
/// is redirected — e.g. invoked via the MCP server). Callers should surface a
/// structured "needs re-auth" error to the user rather than hanging on stdin.
public class SpotifyReauthRequiredException : Exception
{
    public SpotifyReauthRequiredException(string message) : base(message) { }
    public SpotifyReauthRequiredException(string message, Exception inner) : base(message, inner) { }
}
