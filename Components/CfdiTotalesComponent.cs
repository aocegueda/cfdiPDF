using ApiARAConsultoria.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ApiARAConsultoria.Services.Pdf.Components;

public class CfdiTotalesComponent : IComponent
{
    private readonly CfdiDataDto _dto;
    private readonly string _primaryColor;

    public CfdiTotalesComponent(CfdiDataDto dto, string primaryColor)
    {
        _dto = dto;
        _primaryColor = primaryColor;
    }

    public void Compose(IContainer container)
    {
        container.Width(200).Border(0.5f).BorderColor(Colors.Grey.Medium).Column(col =>
        {
            // Encabezado del cuadro con el color institucional
            col.Item().Background(_primaryColor).Padding(3).AlignCenter()
               .Text("TOTALES").Bold().FontColor(Colors.White).FontSize(8.5f);

            col.Item().Padding(4).Column(totalesCol =>
            {
                // Subtotal
                totalesCol.Item().Row(row =>
                {
                    row.RelativeItem().Text("Subtotal").FontSize(8.5f);
                    row.RelativeItem().AlignRight().Text($"${_dto.SubTotal:N2}").Bold().FontSize(8.5f);
                });

                // Impuestos Trasladados -- uno por cada combinación de clave (002, 003...)
                // y tasa (0%, 8%, 16%) que traiga el XML; si el tipo de factor es
                // Exento se marca como tal en vez de mostrar "0.00%".
                foreach (var tras in _dto.ImpuestosTrasladados)
                {
                    totalesCol.Item().PaddingVertical(2).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"{tras.Impuesto} Trasladado").FontSize(7.5f);
                            c.Item().Text(EtiquetaTasa(tras)).Bold().FontSize(7.5f);
                        });

                        row.RelativeItem().AlignRight().AlignMiddle()
                           .Text($"${tras.Importe:N2}").Bold().FontSize(8.5f);
                    });
                }

                // Impuestos Retenidos (Si aplica)
                foreach (var ret in _dto.ImpuestosRetenidos)
                {
                    totalesCol.Item().PaddingVertical(2).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"{ret.Impuesto} Retenido").FontSize(7.5f);
                            c.Item().Text($"{(ret.TasaOCuota * 100):0.00}%").Bold().FontSize(7.5f);
                        });

                        row.RelativeItem().AlignRight().AlignMiddle()
                           .Text($"-${ret.Importe:N2}").Bold().FontSize(8.5f);
                    });
                }

                totalesCol.Item().PaddingVertical(2).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);

                // Total
                totalesCol.Item().Row(row =>
                {
                    row.RelativeItem().Text("Total:").Bold().FontSize(9);
                    row.RelativeItem().AlignRight().Text($"${_dto.Total:N2}").Bold().FontSize(9);
                });
            });
        });
    }

    public static string EtiquetaTasa(ImpuestoResumenDto imp) =>
        imp.TipoFactor.Equals("Exento", StringComparison.OrdinalIgnoreCase)
            ? "EXENTO"
            : $"{(imp.TasaOCuota * 100):0.00}%";
}