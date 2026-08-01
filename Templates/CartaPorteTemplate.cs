using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ApiARAConsultoria.DTOs;
using ApiARAConsultoria.Services.Pdf.Components;

namespace ApiARAConsultoria.Services.Pdf.Templates;

public class CartaPorteTemplate : IDocument
{
    private readonly CfdiDataDto _data;

    public CartaPorteTemplate(CfdiDataDto data)
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
            string titulo = _data.TipoDeComprobante == "T" ? "CARTA PORTE - TRASLADO" : "CARTA PORTE - INGRESO";
            page.Header().Component(new CfdiHeaderComponent(_data, titulo));

            // 2. Contenido Específico de Carta Porte 3.1
            page.Content().Element(ComposeContent);

            // 3. Footer Estándar con QR y Timbre SAT
            page.Footer().Component(new CfdiTimbreSatComponent(_data));
        });
    }

    private void ComposeContent(IContainer container)
    {
        var cp = _data.CartaPorteInfo ?? new ComplementoCartaPorteDto();

        container.PaddingVertical(6).Column(col =>
        {
            // A. DATOS DEL RECEPTOR / CLIENTE
            col.Item().Border(1).BorderColor(CfdiPdfStyles.BorderColor)
               .Background(CfdiPdfStyles.LightBg).Padding(5).Row(row =>
            {
                row.RelativeItem(3).Column(c =>
                {
                    c.Item().Text($"RECEPTOR / DESTINATARIO: {_data.NombreReceptor}").Style(CfdiPdfStyles.HeaderStyle);
                    c.Item().Text($"RFC: {_data.RfcReceptor}  |  Domicilio Fiscal (C.P.): {_data.DomicilioReceptor}");
                    c.Item().Text($"Régimen Fiscal: {CfdiDataDto.ClaveConNombre(_data.RegimenReceptor, _data.RegimenReceptorNombre)}");
                });

                row.RelativeItem(2).Column(c =>
                {
                    c.Item().Text($"Uso CFDI: {CfdiDataDto.ClaveConNombre(_data.UsoCfdi, _data.UsoCfdiNombre)}").Style(CfdiPdfStyles.BodyStyle);
                    c.Item().Text("Versión Carta Porte: 3.1").Style(CfdiPdfStyles.BodyStyle).Bold();
                    c.Item().Text($"IdCCP: {cp.IdCCP}").Style(CfdiPdfStyles.CaptionStyle);
                });
            });

            col.Item().PaddingVertical(4);

            // A.1 CONCEPTOS Y TOTALES -- solo cuando el CFDI realmente cobra el
            // flete (Ingreso/Egreso). Un Traslado puro ("T") no factura nada, así
            // que esta sección no aplica y se omite.
            if (_data.TipoDeComprobante is "I" or "E")
            {
                col.Item().Component(new CfdiConceptosComponent(_data.Conceptos, CfdiPdfStyles.PrimaryColor));
                col.Item().PaddingVertical(4);
                col.Item().AlignRight().Component(new CfdiTotalesComponent(_data, CfdiPdfStyles.PrimaryColor));
                col.Item().PaddingVertical(4);
            }

            // B. UBICACIONES (ORIGEN Y DESTINO)
            col.Item().Border(1).BorderColor(CfdiPdfStyles.SecondaryColor)
               .Background(CfdiPdfStyles.LightBg).Padding(5).Column(c =>
            {
                c.Item().Text("UBICACIONES DEL TRASLADO").Style(CfdiPdfStyles.HeaderStyle).FontColor(CfdiPdfStyles.PrimaryColor);
                c.Item().LineHorizontal(0.5f).LineColor(CfdiPdfStyles.BorderColor);

                c.Item().PaddingTop(3).Row(r =>
                {
                    // Origen
                    r.RelativeItem().Column(origen =>
                    {
                        origen.Item().Text("ORIGEN (Salida):").Bold().FontColor(CfdiPdfStyles.PrimaryColor);
                        origen.Item().Text(string.IsNullOrEmpty(cp.OrigenDireccion) ? "Almacén Central Papelería Rosy" : cp.OrigenDireccion);
                        origen.Item().Text($"Fecha/Hora Salida: {cp.FechaSalida}").Style(CfdiPdfStyles.CaptionStyle).Bold();
                    });

                    r.ConstantItem(15); // Separador

                    // Destino
                    r.RelativeItem().Column(destino =>
                    {
                        destino.Item().Text("DESTINO (Llegada):").Bold().FontColor(CfdiPdfStyles.PrimaryColor);
                        destino.Item().Text(string.IsNullOrEmpty(cp.DestinoDireccion) ? "Domicilio Cliente / Sucursal" : cp.DestinoDireccion);
                        destino.Item().Text($"Fecha/Hora Llegada: {cp.FechaLlegada}").Style(CfdiPdfStyles.CaptionStyle).Bold();
                    });
                });
            });

            col.Item().PaddingVertical(4);

            // C. IDENTIFICACIÓN VEHICULAR Y SEGUROS
            col.Item().Border(1).BorderColor(CfdiPdfStyles.BorderColor).Padding(5).Column(c =>
            {
                c.Item().Text("DATOS DEL AUTOTRANSPORTE FEDERAL").Style(CfdiPdfStyles.HeaderStyle);
                c.Item().LineHorizontal(0.5f).LineColor(CfdiPdfStyles.BorderColor);

                c.Item().PaddingTop(3).Row(r =>
                {
                    r.RelativeItem().Text(t =>
                    {
                        t.Span("Config. Vehicular: ").Bold();
                        t.Span(cp.ConfigVehicular);
                    });
                    r.RelativeItem().Text(t =>
                    {
                        t.Span("Placas: ").Bold();
                        t.Span(cp.Placas);
                    });
                    r.RelativeItem().Text(t =>
                    {
                        t.Span("Año / Modelo: ").Bold();
                        t.Span(cp.Modelo);
                    });
                    r.RelativeItem().Text(t =>
                    {
                        t.Span("Aseguradora: ").Bold();
                        t.Span(cp.Aseguradora);
                    });
                    r.RelativeItem().Text(t =>
                    {
                        t.Span("Póliza Resp. Civil: ").Bold();
                        t.Span(cp.Poliza);
                    });
                });

                c.Item().PaddingTop(2).Row(r =>
                {
                    r.RelativeItem().Text(t =>
                    {
                        t.Span("Transporte Internacional: ").Bold();
                        t.Span(cp.TranspInternac);
                    });
                    r.RelativeItem().Text(t =>
                    {
                        t.Span("Distancia Recorrida: ").Bold();
                        t.Span($"{cp.TotalDistRec} km");
                    });
                });
            });

            col.Item().PaddingVertical(4);

            // D. TABLA DE MERCANCÍAS TRANSPORTADAS
            col.Item().Text("DETALLE DE MERCANCÍAS").Style(CfdiPdfStyles.HeaderStyle);
            col.Item().PaddingTop(2).Element(container => ComposeTablaMercancias(container, cp.Mercancias));

            // E. TOTALES O RESUMEN DE PESO
            col.Item().PaddingTop(4).Row(r =>
            {
                decimal pesoTotal = cp.Mercancias.Sum(m => m.PesoEnKg);
                decimal valorTotal = cp.Mercancias.Sum(m => m.ValorMercancia);

                r.RelativeItem().Text($"Total Peso Estimado: {pesoTotal:N2} Kg").Bold();
                r.RelativeItem().AlignRight().Text($"Valor Declarado Mercancías: {valorTotal:C2}").Bold().FontColor(CfdiPdfStyles.PrimaryColor);
            });
        });
    }

    private void ComposeTablaMercancias(IContainer container, List<MercanciaItemDto> mercancias)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(5); // Descripción
                columns.RelativeColumn(2); // Cantidad
                columns.RelativeColumn(2); // Peso en Kg
                columns.RelativeColumn(3); // Valor Estimado
            });

            // Encabezado
            table.Header(header =>
            {
                header.Cell().Background(CfdiPdfStyles.PrimaryColor).Padding(4).Text("Descripción de la Mercancía").FontColor(Colors.White).Bold();
                header.Cell().Background(CfdiPdfStyles.PrimaryColor).Padding(4).AlignCenter().Text("Cantidad").FontColor(Colors.White).Bold();
                header.Cell().Background(CfdiPdfStyles.PrimaryColor).Padding(4).AlignRight().Text("Peso (Kg)").FontColor(Colors.White).Bold();
                header.Cell().Background(CfdiPdfStyles.PrimaryColor).Padding(4).AlignRight().Text("Valor Mercancía").FontColor(Colors.White).Bold();
            });

            // Filas
            foreach (var item in mercancias)
            {
                table.Cell().BorderBottom(0.5f).BorderColor(CfdiPdfStyles.BorderColor).Padding(4)
                     .Text(item.Descripcion);

                table.Cell().BorderBottom(0.5f).BorderColor(CfdiPdfStyles.BorderColor).Padding(4)
                     .AlignCenter().Text(item.Cantidad.ToString("N2"));

                table.Cell().BorderBottom(0.5f).BorderColor(CfdiPdfStyles.BorderColor).Padding(4)
                     .AlignRight().Text(item.PesoEnKg.ToString("N2"));

                table.Cell().BorderBottom(0.5f).BorderColor(CfdiPdfStyles.BorderColor).Padding(4)
                     .AlignRight().Text(item.ValorMercancia.ToString("C2"));
            }
        });
    }
}