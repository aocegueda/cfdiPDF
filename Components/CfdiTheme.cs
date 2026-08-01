using QuestPDF.Helpers;

namespace ApiARAConsultoria.Services.Pdf.Components;

public class CfdiTheme
{
    public string PrimaryColor { get; set; } = "#1B365D";   // Color encabezados principales
    public string SecondaryColor { get; set; } = "#4B6B94"; // Color bordes / subencabezados
    public string AccentColor { get; set; } = "#E6F0FA";    // Color destacados / badges
    public string LightBg { get; set; } = "#F8FAFC";        // Fondos de recuadros
    public string TableHeaderBg { get; set; } = "#1B365D";  // Header de tablas
    public string TableHeaderFont { get; set; } = "#FFFFFF";
    public string TextDark { get; set; } = "#1A202C";

    // 🔵 TEMA DEFAULT (Azul institucional)
    public static CfdiTheme Default => new CfdiTheme
    {
        PrimaryColor = "#1B365D",
        SecondaryColor = "#4B6B94",
        AccentColor = "#E6F0FA",
        LightBg = "#F8FAFC",
        TableHeaderBg = "#1B365D",
        TableHeaderFont = "#FFFFFF",
        TextDark = "#1A202C"
    };

    // 🌸 TEMA ROSY CORPORATIVO (Basado en la paleta del logo)
    public static CfdiTheme RosyTheme => new CfdiTheme
    {
        PrimaryColor = "#FA2376",    // Rosa Magenta emblemático de ROSY
        SecondaryColor = "#FFB300",  // Amarillo Lápiz
        AccentColor = "#FFF8E1",     // Amarillo muy suave para destacados
        LightBg = "#FDF2F7",         // Rosa pastel ultra claro para recuadros
        TableHeaderBg = "#FA2376",   // Encabezados de tabla en Magenta
        TableHeaderFont = "#FFFFFF",
        TextDark = "#212121"         // Carbón de alta legibilidad
    };
}