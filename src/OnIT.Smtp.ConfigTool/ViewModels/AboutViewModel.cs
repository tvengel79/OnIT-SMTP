using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OnIT.Smtp.ConfigTool.ViewModels;

public partial class AboutViewModel : ObservableObject
{
    private const int FirstCopyrightYear = 2025;

    public string ProductName => "OnIT-SMTP";

    public string Version =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) is { } version ? $"Version {version}" : string.Empty;

    /// <summary>"2025" in 2025, "2025-2026" from 2026 onward -- always current, never needs updating by hand.</summary>
    public string CopyrightYears => DateTime.Now.Year > FirstCopyrightYear
        ? $"{FirstCopyrightYear}-{DateTime.Now.Year}"
        : FirstCopyrightYear.ToString();
}
