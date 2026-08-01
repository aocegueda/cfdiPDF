using ApiARAConsultoria.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ApiARAConsultoria.Services.Pdf.Components;

public class CfdiConceptosComponent : IComponent
{
    private readonly List<ConceptoDto> _conceptos;
    private readonly string _primaryColor;

    public CfdiConceptosComponent(List<ConceptoDto> conceptos, string primaryColor)
    {
        _conceptos = conceptos;
        _primaryColor = primaryColor;
    }

    public void Compose(IContainer container)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(55);  // Cantidad
                columns.ConstantColumn(65);  // Clave Unidad
                columns.ConstantColumn(75);  // ClaveProdServ
                columns.RelativeColumn();    // Descripción
                columns.ConstantColumn(65);  // Precio Unitario
                columns.ConstantColumn(60);  // Descuento
                columns.ConstantColumn(70);  // Importe
            });

            // Encabezado con color institucional de Papelería Rosy
            table.Header(header =>
            {
                header.Cell().Background(_primaryColor).Padding(4).Text("Cantidad").Bold().FontColor(Colors.White).FontSize(8);
                header.Cell().Background(_primaryColor).Padding(4).Text("Clave Unidad").Bold().FontColor(Colors.White).FontSize(8);
                header.Cell().Background(_primaryColor).Padding(4).Text("ClaveProd Serv").Bold().FontColor(Colors.White).FontSize(8);
                header.Cell().Background(_primaryColor).Padding(4).Text("Descripción").Bold().FontColor(Colors.White).FontSize(8);
                header.Cell().Background(_primaryColor).Padding(4).AlignRight().Text("Precio").Bold().FontColor(Colors.White).FontSize(8);
                header.Cell().Background(_primaryColor).Padding(4).AlignRight().Text("Descuento").Bold().FontColor(Colors.White).FontSize(8);
                header.Cell().Background(_primaryColor).Padding(4).AlignRight().Text("Importe").Bold().FontColor(Colors.White).FontSize(8);
            });

            // Filas de Conceptos
            foreach (var item in _conceptos)
            {
                // 1. Fila Principal del Producto
                table.Cell().Padding(3).Text($"{item.Cantidad:N2}").FontSize(8);
                table.Cell().Padding(3).Text(item.ClaveUnidad).FontSize(8);
                table.Cell().Padding(3).Text(item.ClaveProdServ).FontSize(8);
                table.Cell().Padding(3).Text(item.Descripcion).FontSize(8);
                table.Cell().Padding(3).AlignRight().Text($"${item.ValorUnitario:N2}").FontSize(8);
                table.Cell().Padding(3).AlignRight().Text(item.Descuento > 0 ? $"${item.Descuento:N2}" : "").FontSize(8);
                table.Cell().Padding(3).AlignRight().Text($"${item.Importe:N2}").FontSize(8);

                // 2. Fila Secundaria de Impuestos (Exactamente como la primera imagen)
                if (item.ImpuestosConcepto != null && item.ImpuestosConcepto.Any())
                {
                    foreach (var imp in item.ImpuestosConcepto)
                    {
                        // Espaciador izquierdo (bajo Cantidad)
                        table.Cell().PaddingBottom(4); 

                        // Span de 5 columnas para alinear 002, Base, Tasa e Importe
                        table.Cell().ColumnSpan(5).PaddingBottom(4).Row(impRow =>
                        {
                            impRow.RelativeItem(1).Text(imp.Impuesto).FontSize(7.5f);
                            impRow.RelativeItem(2.5f).Text($"Base: ${imp.Base:N2}").FontSize(7.5f);
                            impRow.RelativeItem(2.5f).Text($"Tasa: {imp.TasaOCuota:0.000000}").FontSize(7.5f);
                            impRow.RelativeItem(2.5f).Text($"Importe: ${imp.Importe:N2}").FontSize(7.5f);
                        });

                        // Espaciador derecho (bajo Importe principal)
                        table.Cell().PaddingBottom(4);
                    }
                }
            }
        });
    }
}