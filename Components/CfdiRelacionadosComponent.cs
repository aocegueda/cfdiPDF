using ApiARAConsultoria.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ApiARAConsultoria.Services.Pdf.Components;

public class CfdiRelacionadosComponent : IComponent
{
    private readonly List<CfdiRelacionadoDto> _relacionados;
    private readonly string _primaryColor;

    public CfdiRelacionadosComponent(List<CfdiRelacionadoDto> relacionados, string primaryColor)
    {
        _relacionados = relacionados;
        _primaryColor = primaryColor;
    }

    public void Compose(IContainer container)
    {
        container.Border(0.5f).BorderColor(_primaryColor).Padding(4).Column(col =>
        {
            col.Item().Text("CFDI(S) RELACIONADO(S)").Bold().FontSize(7).FontColor(_primaryColor);

            foreach (var rel in _relacionados)
            {
                col.Item().Text($"Tipo Relación: {rel.TipoRelacionDescripcion}").Bold().FontSize(7);
                foreach (var uuid in rel.Uuids)
                {
                    col.Item().Text($"  • UUID Relacionado: {uuid}").FontSize(6.5f);
                }
            }
        });
    }
}