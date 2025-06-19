using Lfm.Core.Configuration;
using System.Runtime.InteropServices;

namespace Lfm.Core.Services;

public interface ISymbolProvider
{
    string Error { get; }
    string Success { get; }
    string Tip { get; }
    string Timer { get; }
    string Stats { get; }
    string Settings { get; }
    string Cleanup { get; }
}

public class SymbolProvider : ISymbolProvider
{
    private readonly bool _useUnicode;
    private readonly IConfigurationManager _configManager;

    public SymbolProvider(IConfigurationManager configManager)
    {
        _configManager = configManager;
        var shouldUseUnicode = ShouldUseUnicode();
        
        // If we determined we should use Unicode, try to ensure UTF-8 encoding
        if (shouldUseUnicode)
        {
            var encodingSuccess = EnsureUtf8Encoding();
            _useUnicode = encodingSuccess; // Only use Unicode if encoding was successful
        }
        else
        {
            _useUnicode = false;
        }
    }

    private bool ShouldUseUnicode()
    {
        try
        {
            var config = _configManager.LoadAsync().GetAwaiter().GetResult();
            return config.UnicodeSymbols switch
            {
                UnicodeSupport.Enabled => true,
                UnicodeSupport.Disabled => false,
                UnicodeSupport.Auto => DetectUnicodeSupport(),
                _ => DetectUnicodeSupport()
            };
        }
        catch
        {
            // If config loading fails, fall back to auto-detection
            return DetectUnicodeSupport();
        }
    }

    public string Error => _useUnicode ? "❌" : "[X]";
    public string Success => _useUnicode ? "✅" : "[OK]";
    public string Tip => _useUnicode ? "💡" : "[TIP]";
    public string Timer => _useUnicode ? "⏱️" : "[TIME]";
    public string Stats => _useUnicode ? "📊" : "[STATS]";
    public string Settings => _useUnicode ? "⚙️" : "[SETTINGS]";
    public string Cleanup => _useUnicode ? "🧹" : "[CLEANUP]";


    private static bool DetectUnicodeSupport()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Check if we're in Windows Terminal (excellent Unicode support)
            var wtSession = Environment.GetEnvironmentVariable("WT_SESSION");
            if (!string.IsNullOrEmpty(wtSession))
            {
                return true; // Windows Terminal always supports Unicode well
            }

            // Check PowerShell version through environment
            var psEdition = Environment.GetEnvironmentVariable("PSEdition");
            if (psEdition == "Core")
            {
                return true; // PowerShell 7+ Core has good Unicode support
            }

            // Check for PowerShell 5.x indicators (Desktop edition)
            if (psEdition == "Desktop")
            {
                return false; // PowerShell 5.x has Unicode issues
            }

            // Check if we're in PowerShell at all
            var psModulePath = Environment.GetEnvironmentVariable("PSModulePath");
            if (!string.IsNullOrEmpty(psModulePath))
            {
                // We're in PowerShell, but can't determine version from PSEdition
                // Try to detect PowerShell version through other means
                
                // PowerShell 7+ typically sets POWERSHELL_DISTRIBUTION_CHANNEL
                var psDistribution = Environment.GetEnvironmentVariable("POWERSHELL_DISTRIBUTION_CHANNEL");
                if (!string.IsNullOrEmpty(psDistribution))
                {
                    return true; // PowerShell 7+ detected
                }
                
                // Check console encoding as fallback
                try
                {
                    var encoding = Console.OutputEncoding;
                    return encoding.CodePage == 65001; // UTF-8
                }
                catch
                {
                    return false;
                }
            }

            // Not in PowerShell, check console encoding
            try
            {
                var encoding = Console.OutputEncoding;
                return encoding.CodePage == 65001; // UTF-8
            }
            catch
            {
                return false;
            }
        }

        // Linux/macOS generally support Unicode in modern terminals
        return true;
    }

    private static bool EnsureUtf8Encoding()
    {
        try
        {
            var currentEncoding = Console.OutputEncoding;
            
            // If we're already UTF-8, we're good
            if (currentEncoding.CodePage == 65001)
            {
                return true;
            }
            
            // Try to set UTF-8 encoding
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            
            // Verify it worked
            var newEncoding = Console.OutputEncoding;
            return newEncoding.CodePage == 65001;
        }
        catch
        {
            // Failed to set encoding, will use ASCII fallback
            return false;
        }
    }
}