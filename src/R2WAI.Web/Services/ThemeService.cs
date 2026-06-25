using MudBlazor;

namespace R2WAI.Web.Services;

public class ThemeService
{
    public bool IsDarkMode { get; private set; }
    public event Action? OnChange;

    public MudTheme CurrentTheme { get; } = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#7C3AED",
            Secondary = "#3B82F6",
            Tertiary = "#10B981",
            AppbarBackground = "#FFFFFF",
            AppbarText = "#1F2937",
            Background = "#F9FAFB",
            Surface = "#FFFFFF",
            DrawerBackground = "#111827",
            DrawerText = "#D1D5DB",
            TextPrimary = "#111827",
            TextSecondary = "#6B7280",
            ActionDefault = "#6B7280",
            ActionDisabled = "#D1D5DB",
            ActionDisabledBackground = "#F3F4F6",
            Success = "#059669",
            Warning = "#D97706",
            Error = "#DC2626",
            Info = "#3B82F6",
            Divider = "#E5E7EB",
            LinesDefault = "#E5E7EB",
            TableLines = "#F3F4F6",
            TableStriped = "#F9FAFB",
            TableHover = "#F3F4F6",
            HoverOpacity = 0.04,
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#A78BFA",
            Secondary = "#60A5FA",
            Tertiary = "#34D399",
            AppbarBackground = "#111827",
            AppbarText = "#F9FAFB",
            Background = "#030712",
            Surface = "#111827",
            DrawerBackground = "#0F172A",
            DrawerText = "#94A3B8",
            TextPrimary = "#F9FAFB",
            TextSecondary = "#9CA3AF",
            ActionDefault = "#9CA3AF",
            ActionDisabled = "#4B5563",
            ActionDisabledBackground = "#1F2937",
            Success = "#34D399",
            Warning = "#FBBF24",
            Error = "#F87171",
            Info = "#60A5FA",
            Divider = "#1F2937",
            LinesDefault = "#1F2937",
            TableLines = "#1F2937",
            TableStriped = "#0F172A",
            TableHover = "#1E293B",
            HoverOpacity = 0.08,
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = new[] { "Inter", "-apple-system", "BlinkMacSystemFont", "Segoe UI", "Helvetica", "Arial", "sans-serif" },
                FontSize = ".875rem",
                FontWeight = "400",
                LineHeight = "1.5",
                LetterSpacing = "normal",
            },
            H4 = new H4Typography { FontWeight = "700", FontSize = "1.75rem", LineHeight = "1.3" },
            H5 = new H5Typography { FontWeight = "700", FontSize = "1.375rem", LineHeight = "1.3" },
            H6 = new H6Typography { FontWeight = "600", FontSize = "1.125rem", LineHeight = "1.4" },
            Subtitle1 = new Subtitle1Typography { FontWeight = "600", FontSize = "1rem" },
            Subtitle2 = new Subtitle2Typography { FontWeight = "600", FontSize = ".875rem" },
            Body1 = new Body1Typography { FontSize = ".9375rem", LineHeight = "1.6" },
            Body2 = new Body2Typography { FontSize = ".8125rem", LineHeight = "1.5" },
            Button = new ButtonTypography { FontWeight = "600", FontSize = ".8125rem", LetterSpacing = ".02em" },
            Caption = new CaptionTypography { FontSize = ".75rem", LineHeight = "1.4" },
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "10px",
            AppbarHeight = "56px",
        },
    };

    public void ToggleDarkMode()
    {
        IsDarkMode = !IsDarkMode;
        OnChange?.Invoke();
    }

    public void SetDarkMode(bool value)
    {
        IsDarkMode = value;
        OnChange?.Invoke();
    }
}
