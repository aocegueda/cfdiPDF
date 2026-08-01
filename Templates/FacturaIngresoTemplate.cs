using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ApiARAConsultoria.DTOs;
using ApiARAConsultoria.Services.Pdf.Components;

namespace ApiARAConsultoria.Services.Pdf.Templates;

public class FacturaIngresoTemplate : IDocument
{
    private readonly CfdiDataDto _data;

    public FacturaIngresoTemplate(CfdiDataDto data)
    {
        _data = data;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.Letter);
            page.Margin(25);
            page.DefaultTextStyle(CfdiPdfStyles.BodyStyle);

            string titulo = _data.TipoDeComprobante == "E" ? "NOTA DE CRÉDITO" : "FACTURA ELECTRÓNICA";
            page.Header().Component(new CfdiHeaderComponent(_data, titulo));
            page.Content().Element(ComposeContent);
            page.Footer().Component(new CfdiTimbreSatComponent(_data));
        });
    }

    private void ComposeContent(IContainer container)
    {
        container.PaddingVertical(8).Column(col =>
        {
            // 1. Datos Receptor (Cliente)
            col.Item().Border(1).BorderColor(CfdiPdfStyles.BorderColor)
               .Background(CfdiPdfStyles.LightBg).Padding(6).Row(row =>
            {
                row.RelativeItem(3).Column(c =>
                {
                    c.Item().Text($"RECEPTOR / CLIENTE: {_data.NombreReceptor}").Style(CfdiPdfStyles.HeaderStyle);
                    c.Item().Text($"RFC: {_data.RfcReceptor}  |  Domicilio Fiscal (C.P.): {_data.DomicilioReceptor}");
                    c.Item().Text($"Régimen Fiscal: {CfdiDataDto.ClaveConNombre(_data.RegimenReceptor, _data.RegimenReceptorNombre)}");
                });

                row.RelativeItem(2).Column(c =>
                {
                    c.Item().Text($"Uso CFDI: {CfdiDataDto.ClaveConNombre(_data.UsoCfdi, _data.UsoCfdiNombre)}").Style(CfdiPdfStyles.BodyStyle);
                    c.Item().Text("Exportación: 01 - No Aplica").Style(CfdiPdfStyles.BodyStyle);
                });
            });

            // Informacion Factura Global (Mostrador)
            if (!string.IsNullOrEmpty(_data.PeriodicidadGlobal))
            {
                col.Item().PaddingTop(4).Border(1).BorderColor(Colors.Orange.Medium)
                   .Background(Colors.Orange.Lighten5).Padding(4)
                   .Text($"Factura Global -> Periodicidad: {_data.PeriodicidadGlobal} | Meses: {_data.MesesGlobal} | Año: {_data.AnioGlobal}")
                   .Bold().FontSize(7);
            }

            col.Item().PaddingVertical(6);

            // 2. Tabla de Conceptos (Artículos de Papelería) -- usa el componente
            // compartido (Clave SAT sin No.Id + desglose de impuesto por partida)
            col.Item().Component(new CfdiConceptosComponent(_data.Conceptos, CfdiPdfStyles.PrimaryColor));

            col.Item().PaddingVertical(4);

            // 3. Formas de Pago y Totales
            col.Item().Element(ComposeTotales);
        });
    }

    private void ComposeTotales(IContainer container)
    {
        container.Row(row =>
        {
            // Datos Fiscales de Pago
            row.RelativeItem(3).Column(col =>
            {
                col.Item().Text($"Forma de Pago: {CfdiDataDto.ClaveConNombre(_data.FormaPago, _data.FormaPagoNombre)}");
                col.Item().Text($"Método de Pago: {CfdiDataDto.ClaveConNombre(_data.MetodoPago, _data.MetodoPagoNombre)}");
                col.Item().Text($"Moneda: {_data.Moneda}");
            });

            // Resumen numérico -- componente compartido: desglosa cada impuesto
            // trasladado por clave (002, 003...) y tasa (0%, 8%, 16%), marcando
            // los que vienen como Exento en vez de una tasa.
            row.RelativeItem(2).AlignRight().Component(new CfdiTotalesComponent(_data, CfdiPdfStyles.PrimaryColor));
        });
    }
}