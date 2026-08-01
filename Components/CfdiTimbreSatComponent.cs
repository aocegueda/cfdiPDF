using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using ApiARAConsultoria.DTOs;

namespace ApiARAConsultoria.Services.Pdf.Components;

public class CfdiTimbreSatComponent : IComponent
{
    private readonly CfdiDataDto _data;

    public CfdiTimbreSatComponent(CfdiDataDto data)
    {
        _data = data;
    }

    public void Compose(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().LineHorizontal(0.5f).LineColor(CfdiPdfStyles.BorderColor);

            col.Item().PaddingTop(4).Row(row =>
            {
                // Código QR (80x80px)
                if (!string.IsNullOrEmpty(_data.QrCodeBase64))
                {
                    byte[] qrBytes = Convert.FromBase64String(_data.QrCodeBase64);
                    row.ConstantItem(75).Height(75).Image(qrBytes);
                }

                // Bloque de Certificados y Sellos SAT
                row.RelativeItem().PaddingLeft(6).Column(c =>
                {
                    c.Item().Text(t =>
                    {
                        t.Span("No. Certificado Emisor: ").Style(CfdiPdfStyles.CaptionStyle).Bold();
                        t.Span($"{_data.NoCertificadoEmisor}  |  ").Style(CfdiPdfStyles.CaptionStyle);
                        t.Span("No. Certificado SAT: ").Style(CfdiPdfStyles.CaptionStyle).Bold();
                        t.Span($"{_data.NoCertificadoSAT}  |  ").Style(CfdiPdfStyles.CaptionStyle);
                        t.Span("Fecha Certificación: ").Style(CfdiPdfStyles.CaptionStyle).Bold();
                        t.Span(_data.FechaTimbrado).Style(CfdiPdfStyles.CaptionStyle);
                    });

                    c.Item().PaddingTop(2).Text("Cadena Original del Complemento de Certificación Digital del SAT:").Style(CfdiPdfStyles.CaptionStyle).Bold();
                    c.Item().Text(_data.CadenaOriginalSAT).Style(CfdiPdfStyles.CaptionStyle);

                    c.Item().PaddingTop(2).Text("Sello Digital del Emisor:").Style(CfdiPdfStyles.CaptionStyle).Bold();
                    c.Item().Text(_data.SelloCFD).Style(CfdiPdfStyles.CaptionStyle);

                    c.Item().PaddingTop(2).Text("Sello Digital del SAT:").Style(CfdiPdfStyles.CaptionStyle).Bold();
                    c.Item().Text(_data.SelloSAT).Style(CfdiPdfStyles.CaptionStyle);
                });
            });

            col.Item().PaddingTop(4).AlignCenter()
               .Text("Este documento es una representación impresa de un CFDI v4.0")
               .Style(CfdiPdfStyles.CaptionStyle).Bold();
        });
    }
}