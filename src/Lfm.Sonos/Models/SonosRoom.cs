namespace Lfm.Sonos.Models;

public class SonosRoom
{
    public string Name { get; set; } = string.Empty;
    public string Coordinator { get; set; } = string.Empty;
    public List<string> Members { get; set; } = new();
}
