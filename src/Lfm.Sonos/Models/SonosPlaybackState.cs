namespace Lfm.Sonos.Models;

public class SonosPlaybackState
{
    public string CurrentTrack { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Album { get; set; } = string.Empty;
    public string PlaybackState { get; set; } = string.Empty; // PLAYING, PAUSED, STOPPED
    public int Volume { get; set; }
    public int TrackNumber { get; set; }
    public int? ElapsedTime { get; set; }
    public int? Duration { get; set; }
}
