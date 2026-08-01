using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ApiARAConsultoria.DTOs;
using ApiARAConsultoria.Services.Pdf.Components;

namespace ApiARAConsultoria.Services.Pdf.Templates;

public class ComplementoPagoTemplate : IDocument
{
    private readonly CfdiDataDto _data;

    public ComplementoPagoTemplate(CfdiDataDto data)
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

            // 1. Header Estándar
            page.Header().Component(new CfdiHeaderComponent(_data, "COMPLEMENTO DE PAGO"));
            
            // 2. Contenido del Pago y Documentos Relacionados
            page.Content().Element(ComposeContent);

            // 3. Footer Estándar con QR y Sellos SAT
            page.Footer().Component(new CfdiTimbreSatComponent(_data));
        });
    }

    private void ComposeContent(IContainer container)
    {
        container.PaddingVertical(8).Column(col =>
        {
            // A. DATOS DEL RECEPTOR (CLIENTE)
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
                    c.Item().Text($"Uso CFDI: CP01 - Pagos").Style(CfdiPdfStyles.BodyStyle).Bold();
                    c.Item().Text("Exportación: 01 - No Aplica").Style(CfdiPdfStyles.BodyStyle);
                });
            });

            col.Item().PaddingVertical(6);

            // B. SECCIÓN INFORMACIÓN DEL PAGO RECIBIDO
            var pago = _data.PagoInfo ?? new ComplementoPagoDto();

            col.Item().Border(1).BorderColor(CfdiPdfStyles.SecondaryColor)
               .Background(CfdiPdfStyles.LightBg).Padding(6).Column(c =>
            {
                c.Item().Text("INFORMACIÓN DEL PAGO").Style(CfdiPdfStyles.HeaderStyle).FontColor(CfdiPdfStyles.PrimaryColor);
                c.Item().LineHorizontal(0.5f).LineColor(CfdiPdfStyles.BorderColor);
                
                c.Item().PaddingTop(4).Row(r =>
                {
                    r.RelativeItem().Text(text =>
                    {
                        text.Span("Fecha de Pago: ").Bold();
                        text.Span(pago.FechaPago);
                    });

                    r.RelativeItem().Text(text =>
                    {
                        text.Span("Forma de Pago: ").Bold();
                        text.Span(CfdiDataDto.ClaveConNombre(pago.FormaPago, pago.FormaPagoNombre));
                    });

                    r.RelativeItem().Text(text =>
                    {
                        text.Span("Moneda: ").Bold();
                        text.Span(pago.Moneda);
                    });
                });

                c.Item().PaddingTop(2).Row(r =>
                {
                    r.RelativeItem().Text(text =>
                    {
                        text.Span("Monto Recibido: ").Bold();
                        text.Span(pago.MontoTotal.ToString("C2"))
                            .FontColor(CfdiPdfStyles.PrimaryColor);
                    });

                    r.RelativeItem().Text(text =>
                    {
                        text.Span("No. Operación: ").Bold();
                        text.Span(string.IsNullOrEmpty(pago.NumOperacion)
                            ? "N/A"
                            : pago.NumOperacion);
                    });

                    r.RelativeItem().Text("");
                });

                // Impuestos trasladados del pago (pago20:Totales) -- se parseaba
                // en el DTO pero nunca se mostraba.
                if (pago.ImpuestosPPD.Count > 0)
                {
                    c.Item().PaddingTop(4).LineHorizontal(0.5f).LineColor(CfdiPdfStyles.BorderColor);
                    c.Item().PaddingTop(2).Row(r =>
                    {
                        foreach (var imp in pago.ImpuestosPPD)
                        {
                            r.RelativeItem().Text(text =>
                            {
                                text.Span($"{imp.Impuesto} Trasladado ({CfdiTotalesComponent.EtiquetaTasa(imp)}): ").Bold();
                                text.Span($"${imp.Importe:N2}");
                            });
                        }
                    });
                }
            });

            col.Item().PaddingVertical(8);

            // C. TABLA DE DOCUMENTOS RELACIONADOS (FACTURAS LIQUIDADAS)
            col.Item().Text("DOCUMENTOS RELACIONADOS (FACTURAS LIQUIDADAS)").Style(CfdiPdfStyles.HeaderStyle);
            col.Item().PaddingTop(2).Element(container => ComposeTablaDoctosRelacionados(container, pago.DoctosRelacionados));
        });
    }

    private void ComposeTablaDoctosRelacionados(IContainer container, List<DocumentoRelacionadoDto> doctos)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(3.5f); // UUID Factura
                columns.RelativeColumn(1.5f); // Serie/Folio
                columns.RelativeColumn(1f);   // Parcialidad
                columns.RelativeColumn(2f);   // Saldo Anterior
                columns.RelativeColumn(2f);   // Importe Pagado
                columns.RelativeColumn(2f);   // Saldo Insoluto
            });

            // Encabezado
            table.Header(header =>
            {
                header.Cell().Background(CfdiPdfStyles.PrimaryColor).Padding(4).Text("Folio Fiscal (UUID) Orig.").FontColor(Colors.White).Bold();
                header.Cell().Background(CfdiPdfStyles.PrimaryColor).Padding(4).Text("Serie / Folio").FontColor(Colors.White).Bold();
                header.Cell().Background(CfdiPdfStyles.PrimaryColor).Padding(4).AlignCenter().Text("Parc.").FontColor(Colors.White).Bold();
                header.Cell().Background(CfdiPdfStyles.PrimaryColor).Padding(4).AlignRight().Text("Saldo Ant.").FontColor(Colors.White).Bold();
                header.Cell().Background(CfdiPdfStyles.PrimaryColor).Padding(4).AlignRight().Text("Imp. Pagado").FontColor(Colors.White).Bold();
                header.Cell().Background(CfdiPdfStyles.PrimaryColor).Padding(4).AlignRight().Text("Saldo Insoluto").FontColor(Colors.White).Bold();
            });

            // Filas de Facturas Afectadas
            foreach (var doc in doctos)
            {
                table.Cell().BorderBottom(0.5f).BorderColor(CfdiPdfStyles.BorderColor).Padding(4)
                     .Text(doc.IdDocumento).Style(CfdiPdfStyles.CaptionStyle).Bold();

                table.Cell().BorderBottom(0.5f).BorderColor(CfdiPdfStyles.BorderColor).Padding(4)
                     .Text($"{doc.Serie}-{doc.Folio}");

                table.Cell().BorderBottom(0.5f).BorderColor(CfdiPdfStyles.BorderColor).Padding(4)
                     .AlignCenter().Text(doc.NumParcialidad.ToString());

                table.Cell().BorderBottom(0.5f).BorderColor(CfdiPdfStyles.BorderColor).Padding(4)
                     .AlignRight().Text(doc.ImpSaldoAnt.ToString("C2"));

                table.Cell().BorderBottom(0.5f).BorderColor(CfdiPdfStyles.BorderColor).Padding(4)
                     .AlignRight().Text(doc.ImpPagado.ToString("C2")).Bold();

                table.Cell().BorderBottom(0.5f).BorderColor(CfdiPdfStyles.BorderColor).Padding(4)
                     .AlignRight().Text(doc.ImpSaldoInsoluto.ToString("C2"));
            }
        });
    }
}