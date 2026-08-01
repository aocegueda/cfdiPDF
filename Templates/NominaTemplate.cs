using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ApiARAConsultoria.DTOs;
using ApiARAConsultoria.Services.Pdf.Components;

namespace ApiARAConsultoria.Services.Pdf.Templates;

public class NominaTemplate : IDocument
{
    private readonly CfdiDataDto _data;

    public NominaTemplate(CfdiDataDto data)
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
            page.Header().Component(new CfdiHeaderComponent(_data, "RECIBO DE NÓMINA"));

            // 2. Contenido Específico de Nómina
            page.Content().Element(ComposeContent);

            // 3. Footer Estándar con QR y Timbre SAT
            page.Footer().Component(new CfdiTimbreSatComponent(_data));
        });
    }

    private void ComposeContent(IContainer container)
    {
        var nom = _data.NominaInfo ?? new ComplementoNominaDto();

        container.PaddingVertical(6).Column(col =>
        {
            // A. DATOS LABORALES DEL EMPLEADO
            col.Item().Border(1).BorderColor(CfdiPdfStyles.BorderColor)
               .Background(CfdiPdfStyles.LightBg).Padding(5).Column(c =>
            {
                c.Item().Text($"DATOS DEL EMPLEADO: {_data.NombreReceptor}").Style(CfdiPdfStyles.HeaderStyle);
                c.Item().LineHorizontal(0.5f).LineColor(CfdiPdfStyles.BorderColor);

                c.Item().PaddingTop(3).Row(r =>
                {
                    r.RelativeItem().Text(t => { t.Span("RFC: ").Bold(); t.Span(_data.RfcReceptor); });
                    r.RelativeItem().Text(t => { t.Span("CURP: ").Bold(); t.Span(nom.Curp); });
                    r.RelativeItem().Text(t => { t.Span("NSS: ").Bold(); t.Span(nom.Nss); });
                });

                c.Item().PaddingTop(2).Row(r =>
                {
                    r.RelativeItem().Text(t => { t.Span("No. Empleado: ").Bold(); t.Span(nom.NumEmpleado); });
                    r.RelativeItem().Text(t => { t.Span("Puesto: ").Bold(); t.Span(nom.Puesto); });
                    r.RelativeItem().Text(t => { t.Span("Departamento: ").Bold(); t.Span(nom.Departamento); });
                });

                c.Item().PaddingTop(2).Row(r =>
                {
                    r.RelativeItem().Text(t => { t.Span("Inicio Rel. Lab: ").Bold(); t.Span(nom.FechaInicioRelLaboral); });
                    r.RelativeItem().Text(t => { t.Span("Días Pagados: ").Bold(); t.Span(nom.DiasPagados.ToString("N1")); });
                    r.RelativeItem().Text(t => { t.Span("Régimen: ").Bold(); t.Span(CfdiDataDto.ClaveConNombre(_data.RegimenReceptor, _data.RegimenReceptorNombre)); });
                });

                // Datos del periodo y tipo de nómina -- se parseaban del XML pero
                // no se mostraban en ningún lado.
                c.Item().PaddingTop(2).Row(r =>
                {
                    r.RelativeItem().Text(t => { t.Span("Tipo Nómina: ").Bold(); t.Span(GetTipoNominaDesc(nom.TipoNomina)); });
                    r.RelativeItem().Text(t => { t.Span("Periodicidad: ").Bold(); t.Span(GetPeriodicidadDesc(nom.PeriodicidadPago)); });
                    r.RelativeItem().Text(t => { t.Span("Fecha de Pago: ").Bold(); t.Span(nom.FechaPago); });
                });

                c.Item().PaddingTop(2).Row(r =>
                {
                    r.RelativeItem().Text(t => { t.Span("Periodo Pagado: ").Bold(); t.Span($"{nom.FechaInicialPago} a {nom.FechaFinalPago}"); });
                    r.RelativeItem().Text(t => { t.Span("Tipo Contrato: ").Bold(); t.Span(nom.TipoContrato); });
                    r.RelativeItem().Text(t => { t.Span("Tipo Jornada: ").Bold(); t.Span(nom.TipoJornada); });
                    r.RelativeItem().Text(t => { t.Span("Entidad Federativa: ").Bold(); t.Span(nom.ClaveEntFed); });
                });
            });

            col.Item().PaddingVertical(6);

            // B. TABLA A DOS COLUMNAS: PERCEPCIONES VS DEDUCCIONES
            col.Item().Row(r =>
            {
                // Columna Izquierda: Percepciones
                r.RelativeItem().PaddingRight(4).Column(c =>
                {
                    c.Item().Text("PERCEPCIONES").Style(CfdiPdfStyles.HeaderStyle).FontColor(CfdiPdfStyles.PrimaryColor);
                    c.Item().PaddingTop(2).Element(cont => ComposeTablaDetalle(cont, nom.Percepciones));
                });

                // Columna Derecha: Deducciones
                r.RelativeItem().PaddingLeft(4).Column(c =>
                {
                    c.Item().Text("DEDUCCIONES").Style(CfdiPdfStyles.HeaderStyle).FontColor(Colors.Red.Darken2);
                    c.Item().PaddingTop(2).Element(cont => ComposeTablaDetalle(cont, nom.Deducciones));
                });
            });

            // B.1 OTROS PAGOS (ej. subsidio para el empleo) -- se parseaba del XML
            // pero no se mostraba en ningún lado. Solo ocupa espacio si trae algo.
            if (nom.OtrosPagos.Count > 0)
            {
                col.Item().PaddingVertical(4);
                col.Item().Text("OTROS PAGOS").Style(CfdiPdfStyles.HeaderStyle).FontColor(CfdiPdfStyles.SecondaryColor);
                col.Item().PaddingTop(2).Element(cont => ComposeTablaDetalle(cont, nom.OtrosPagos));
            }

            col.Item().PaddingVertical(4);

            // C. RESUMEN DE TOTALES Y NETO A RECIBIR
            col.Item().Border(1).BorderColor(CfdiPdfStyles.SecondaryColor)
               .Background(CfdiPdfStyles.LightBg).Padding(6).Row(r =>
            {
                r.RelativeItem(3).Column(c =>
                {
                    c.Item().Text($"Total Percepciones: {nom.TotalPercepciones:C2}").Bold();
                    c.Item().Text($"Total Deducciones: {nom.TotalDeducciones:C2}").Bold();

                    if (nom.TotalOtrosPagos > 0)
                        c.Item().Text($"Total Otros Pagos: {nom.TotalOtrosPagos:C2}").Bold();
                });

                r.RelativeItem(2).Column(c =>
                {
                    // El SAT exige sumar TotalOtrosPagos (ej. subsidio al empleo) al
                    // neto -- antes se ignoraba y el neto salía por debajo del real.
                    decimal neto = nom.TotalPercepciones - nom.TotalDeducciones + nom.TotalOtrosPagos;
                    c.Item().AlignRight().Text("NETO A RECIBIR:").Style(CfdiPdfStyles.HeaderStyle);
                    c.Item().AlignRight().Text(neto.ToString("C2")).Style(CfdiPdfStyles.TitleStyle).FontSize(13).FontColor(CfdiPdfStyles.PrimaryColor);
                });
            });
        });
    }

    private void ComposeTablaDetalle(IContainer container, List<DetalleNominaItemDto> items)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(1.5f); // Clave
                columns.RelativeColumn(4);    // Concepto
                columns.RelativeColumn(2.5f); // Importe
            });

            // Encabezado
            table.Header(header =>
            {
                header.Cell().Background(CfdiPdfStyles.PrimaryColor).Padding(3).Text("Clave").FontColor(Colors.White).Bold();
                header.Cell().Background(CfdiPdfStyles.PrimaryColor).Padding(3).Text("Concepto").FontColor(Colors.White).Bold();
                header.Cell().Background(CfdiPdfStyles.PrimaryColor).Padding(3).AlignRight().Text("Importe").FontColor(Colors.White).Bold();
            });

            // Filas
            foreach (var item in items)
            {
                table.Cell().BorderBottom(0.5f).BorderColor(CfdiPdfStyles.BorderColor).Padding(3)
                     .Text(item.Clave).Style(CfdiPdfStyles.CaptionStyle);

                table.Cell().BorderBottom(0.5f).BorderColor(CfdiPdfStyles.BorderColor).Padding(3)
                     .Text(item.Concepto);

                table.Cell().BorderBottom(0.5f).BorderColor(CfdiPdfStyles.BorderColor).Padding(3)
                     .AlignRight().Text(item.Importe.ToString("C2"));
            }
        });
    }

    // Catálogos SAT chicos y fijos (c_TipoNomina, c_PeriodicidadPago) -- no viven
    // en BD como Régimen/UsoCFDI/FormaPago, así que se resuelven aquí igual que
    // TipoDeComprobante en CfdiHeaderComponent.
    private static string GetTipoNominaDesc(string tipo) => tipo switch
    {
        "O" => "O - Ordinaria",
        "E" => "E - Extraordinaria",
        _ => tipo
    };

    private static string GetPeriodicidadDesc(string clave) => clave switch
    {
        "01" => "01 - Diario",
        "02" => "02 - Semanal",
        "03" => "03 - Catorcenal",
        "04" => "04 - Quincenal",
        "05" => "05 - Mensual",
        "06" => "06 - Bimestral",
        "07" => "07 - Unidad de obra",
        "08" => "08 - Comisión",
        "09" => "09 - Precio alzado",
        "10" => "10 - Decenal",
        "99" => "99 - Otra periodicidad",
        _ => clave
    };
}