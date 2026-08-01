using ApiARAConsultoria.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace ApiARAConsultoria.Services.Pdf.Components;

public class CfdiReceptorComponent : IComponent
{
    private readonly CfdiDataDto _model;

    public CfdiReceptorComponent(CfdiDataDto model)
    {
        _model = model;
    }

    public void Compose(IContainer container)
    {
        container.Border(0.5f).BorderColor("#CCCCCC").Padding(5).Column(col =>
        {
            col.Item().Text("DATOS DEL RECEPTOR").Bold().FontSize(7);
            col.Item().Text($"RFC: {_model.RfcReceptor} | Nombre: {_model.NombreReceptor}");
            col.Item().Text($"Uso CFDI: {_model.UsoCfdi} | Regimen Fiscal: {_model.RegimenReceptor}");
            col.Item().Text($"Domicilio Fiscal (C.P.): {_model.DomicilioReceptor}");
        });
    }
}