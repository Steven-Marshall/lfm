using Lfm.Core.Services;
using Lfm.Sonos;
using Microsoft.Extensions.Logging;

namespace Lfm.Cli.Commands;

public class SonosRoomsCommand
{
    private readonly ISonosStreamer _sonosStreamer;
    private readonly ISymbolProvider _symbols;
    private readonly ILogger<SonosRoomsCommand> _logger;

    public SonosRoomsCommand(
        ISonosStreamer sonosStreamer,
        ISymbolProvider symbolProvider,
        ILogger<SonosRoomsCommand> logger)
    {
        _sonosStreamer = sonosStreamer ?? throw new ArgumentNullException(nameof(sonosStreamer));
        _symbols = symbolProvider ?? throw new ArgumentNullException(nameof(symbolProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ExecuteAsync()
    {
        try
        {
            if (!await _sonosStreamer.IsAvailableAsync())
            {
                Console.WriteLine($"{_symbols.Error} Sonos bridge not available.");
                Console.WriteLine($"{_symbols.Tip} Check your Sonos API URL configuration with: lfm config show");
                return;
            }

            Console.WriteLine($"{_symbols.Music} Discovering Sonos rooms...");

            var rooms = await _sonosStreamer.GetRoomsAsync();
            if (!rooms.Any())
            {
                Console.WriteLine($"{_symbols.StopSign} No Sonos rooms found.");
                Console.WriteLine($"{_symbols.Tip} Ensure your Sonos system is powered on and connected to the network.");
                return;
            }

            Console.WriteLine($"🏠 Found {rooms.Count} Sonos room{(rooms.Count == 1 ? "" : "s")}:");
            Console.WriteLine();

            foreach (var room in rooms.OrderBy(r => r.Name))
            {
                Console.WriteLine($"  🔊 {room.Name}");

                if (room.Members.Count > 1)
                {
                    Console.WriteLine($"      Grouped with: {string.Join(", ", room.Members.Where(m => m != room.Name))}");
                }

                Console.WriteLine($"      Coordinator: {room.Coordinator}");
                Console.WriteLine();
            }

            Console.WriteLine($"{_symbols.Tip} Use 'lfm config set-sonos-default-room \"Room Name\"' to set a default room.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing Sonos rooms");
            Console.WriteLine($"{_symbols.Error} Error: {ex.Message}");
        }
    }
}
