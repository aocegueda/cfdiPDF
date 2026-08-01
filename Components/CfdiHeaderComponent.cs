using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using ApiARAConsultoria.DTOs;

namespace ApiARAConsultoria.Services.Pdf.Components;

public class CfdiHeaderComponent : IComponent
{
    private readonly CfdiDataDto _data;
    private readonly string _tipoComprobanteTitulo;

    public CfdiHeaderComponent(CfdiDataDto data, string tipoComprobanteTitulo)
    {
        _data = data;
        _tipoComprobanteTitulo = tipoComprobanteTitulo;
    }

    public void Compose(IContainer container)
    {
        container.Row(row =>
        {
            if (_data.LogoBytes != null && _data.LogoBytes.Length > 0)
            {
                row.ConstantItem(65)
                .PaddingRight(8)
                .AlignMiddle()
                .Image(_data.LogoBytes);
            }
            // Columna 1: Datos Emisor (Papelería Rosy)
            row.RelativeItem(3).Column(col =>
            {
                col.Item().Text(_data.NombreEmisor).Style(CfdiPdfStyles.TitleStyle);
                col.Item().Text($"RFC: {_data.RfcEmisor}").Style(CfdiPdfStyles.HeaderStyle);
                col.Item().Text($"Régimen Fiscal: {CfdiDataDto.ClaveConNombre(_data.RegimenEmisor, _data.RegimenEmisorNombre)}").Style(CfdiPdfStyles.BodyStyle);
                //col.Item().Text($"Lugar de Expedición (C.P.): {_data.LugarExpedicion}").Style(CfdiPdfStyles.BodyStyle);
                // 📍 Dirección física de la sucursal (si se proporcionó)
                if (!string.IsNullOrWhiteSpace(_data.DireccionSucursal))
                {
                    col.Item().Text($"Dirección: {_data.DireccionSucursal}").Style(CfdiPdfStyles.BodyStyle);
                }

                // 📮 Código Postal extraído del XML
                col.Item().Text($"Lugar de Expedición (C.P.): {_data.LugarExpedicion}").Style(CfdiPdfStyles.BodyStyle);
            });

            // Columna 2: Recuadro Oficial del CFDI
            row.RelativeItem(2).Border(1)
                .BorderColor(CfdiPdfStyles.BorderColor)
                .Background(CfdiPdfStyles.LightBg)
                .Padding(6)
                .Column(col =>
                {
                    col.Item().AlignCenter().Text(_tipoComprobanteTitulo).Style(CfdiPdfStyles.HeaderStyle).FontColor(CfdiPdfStyles.PrimaryColor);
                    col.Item().LineHorizontal(0.5f).LineColor(CfdiPdfStyles.BorderColor);
                    
                    col.Item().PaddingTop(2).Text(text =>
                    {
                        text.Span("FOLIO FISCAL (UUID):\n").Style(CfdiPdfStyles.CaptionStyle).Bold();
                        text.Span(_data.Uuid).Style(CfdiPdfStyles.CaptionStyle).FontColor(CfdiPdfStyles.TextDark);
                    });

                    col.Item().Text(text =>
                    {
                        text.Span("Serie/Folio: ")
                            .Style(CfdiPdfStyles.BodyStyle)
                            .Bold();

                        text.Span($"{_data.Serie}-{_data.Folio}")
                            .Style(CfdiPdfStyles.BodyStyle);
                    });

                    col.Item().Text(text =>
                    {
                        text.Span("Fecha Emisión: ")
                            .Style(CfdiPdfStyles.BodyStyle)
                            .Bold();

                        text.Span(_data.Fecha)
                            .Style(CfdiPdfStyles.BodyStyle);
                    });

                    col.Item().Text(text =>
                    {
                        text.Span("Tipo Comprobante: ")
                            .Style(CfdiPdfStyles.BodyStyle)
                            .Bold();

                        text.Span($"{_data.TipoDeComprobante} - {GetTipoDesc(_data.TipoDeComprobante)}")
                            .Style(CfdiPdfStyles.BodyStyle);
                    });
                });
        });
    }

    private string GetTipoDesc(string tipo) => tipo switch
    {
        "I" => "Ingreso",
        "E" => "Egreso",
        "P" => "Pago",
        "N" => "Nómina",
        "T" => "Traslado",
        _ => "Comprobante"
    };
}