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
            Primary = "#2563EB",
            Secondary = "#64748B",
            Tertiary = "#10B981",
            AppbarBackground = "#FFFFFF",
            AppbarText = "#0F172A",
            Background = "#F8FAFC",
            Surface = "#FFFFFF",
            DrawerBackground = "#FFFFFF",
            DrawerText = "#475569",
            DrawerIcon = "#64748B",
            TextPrimary = "#0F172A",
            TextSecondary = "#64748B",
            ActionDefault = "#64748B",
            ActionDisabled = "#CBD5E1",
            ActionDisabledBackground = "#F1F5F9",
            Success = "#16A34A",
            Warning = "#D97706",
            Error = "#DC2626",
            Info = "#2563EB",
            Divider = "#E2E8F0",
            LinesDefault = "#E2E8F0",
            TableLines = "#F1F5F9",
            TableStriped = "#F8FAFC",
            TableHover = "#F1F5F9",
            HoverOpacity = 0.04,
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#60A5FA",
            Secondary = "#94A3B8",
            Tertiary = "#34D399",
            AppbarBackground = "#0F172A",
            AppbarText = "#F8FAFC",
            Background = "#0B1220",
            Surface = "#111C33",
            DrawerBackground = "#0B1424",
            DrawerText = "#94A3B8",
            DrawerIcon = "#94A3B8",
            TextPrimary = "#F8FAFC",
            TextSecondary = "#94A3B8",
            ActionDefault = "#94A3B8",
            ActionDisabled = "#475569",
            ActionDisabledBackground = "#1E293B",
            Success = "#34D399",
            Warning = "#FBBF24",
            Error = "#F87171",
            Info = "#60A5FA",
            Divider = "#1E293B",
            LinesDefault = "#1E293B",
            TableLines = "#1E293B",
            TableStriped = "#0F1B2E",
            TableHover = "#17233B",
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
            H4 = new H4Typography { FontFamily = new[] { "Inter", "sans-serif" }, FontWeight = "700", FontSize = "1.75rem", LineHeight = "1.3" },
            H5 = new H5Typography { FontFamily = new[] { "Inter", "sans-serif" }, FontWeight = "700", FontSize = "1.375rem", LineHeight = "1.3" },
            H6 = new H6Typography { FontFamily = new[] { "Inter", "sans-serif" }, FontWeight = "600", FontSize = "1.125rem", LineHeight = "1.4" },
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
