using ApiARAConsultoria.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using QuestPDF.Helpers;

namespace ApiARAConsultoria.Services.Pdf.Components;

public class CfdiPdfDocument : IDocument
{
    private readonly CfdiDataDto _model;

    public CfdiPdfDocument(CfdiDataDto model)
    {
        _model = model;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.Letter);
            page.Margin(25);
            page.DefaultTextStyle(x => x.FontSize(8).FontFamily("Arial"));

            // Colores Corporativos (Paleta Rosy o Default Azul)
            var primaryColor = _model.UsarTemaCorporativo ? "#D81B60" : "#1565C0";
            var secondaryColor = "#424242";

            page.Header().Component(new CfdiHeaderComponent(_model, primaryColor));

            page.Content().PaddingVertical(10).Column(column =>
            {
                column.Spacing(8);

                // Receptores / Datos Fiscales
                column.Item().Component(new CfdiReceptorComponent(_model));

                // CFDIs Relacionados (Si existen)
                if (_model.CfdisRelacionados.Any())
                {
                    column.Item().Component(new CfdiRelacionadosComponent(_model.CfdisRelacionados, primaryColor));
                }

                // Tabla de Conceptos (Papelería / Artículos / Impuestos por partida)
                column.Item().Component(new CfdiConceptosComponent(_model.Conceptos, primaryColor));

                // Sección de Totales e Importe con Letra
                column.Item().Component(new CfdiTotalesComponent(_model, primaryColor));

                // Complemento de Pago / Nómina / Carta Porte (Condicional)
                if (_model.PagoInfo != null)
                    column.Item().Component(new CfdiComplementoPagoComponent(_model.PagoInfo, primaryColor));
            });

            page.Footer().Component(new CfdiTimbreFiscalComponent(_model, primaryColor));
        });
    }
}