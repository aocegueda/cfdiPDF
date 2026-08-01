using System.Threading;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace ApiARAConsultoria.Services.Pdf.Components;

public static class CfdiPdfStyles
{
    // AsyncLocal en vez de un campo estático plano: con requests concurrentes
    // generando PDFs al mismo tiempo, un campo estático normal permite que un
    // request pise el tema de otro a medio renderizar. AsyncLocal aísla el valor
    // por cada flujo async (1:1 con cada request de ASP.NET Core).
    private static readonly AsyncLocal<CfdiTheme?> _currentTheme = new();

    public static CfdiTheme CurrentTheme => _currentTheme.Value ?? CfdiTheme.Default;

    public static void SetTheme(CfdiTheme theme)
    {
        _currentTheme.Value = theme;
    }

    // Colores dinámicos derivados del tema activo
    public static string PrimaryColor => CurrentTheme.PrimaryColor;
    public static string SecondaryColor => CurrentTheme.SecondaryColor;
    public static string LightBg => CurrentTheme.LightBg;
    public static string BorderColor => "#E2E8F0";
    public const string TextDark = "#333333";

    // Estilos de texto
    public static TextStyle TitleStyle => TextStyle.Default
        .FontSize(13)
        .Bold()
        .FontColor(CurrentTheme.PrimaryColor);

    public static TextStyle HeaderStyle => TextStyle.Default
        .FontSize(9)
        .Bold()
        .FontColor(CurrentTheme.TextDark);

    public static TextStyle BodyStyle => TextStyle.Default
        .FontSize(8)
        .FontColor(CurrentTheme.TextDark);

    public static TextStyle CaptionStyle => TextStyle.Default
        .FontSize(7)
        .FontColor("#64748B");
}