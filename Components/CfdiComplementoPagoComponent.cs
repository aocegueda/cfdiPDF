using ApiARAConsultoria.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace ApiARAConsultoria.Services.Pdf.Components;

public class CfdiComplementoPagoComponent : IComponent
{
    private readonly ComplementoPagoDto _pago;
    private readonly string _primaryColor;

    public CfdiComplementoPagoComponent(ComplementoPagoDto pago, string primaryColor)
    {
        _pago = pago;
        _primaryColor = primaryColor;
    }

    public void Compose(IContainer container)
    {
        container.Border(0.5f).BorderColor(_primaryColor).Padding(5).Column(col =>
        {
            col.Item().Text("INFORMACIÓN DEL PAGO").Bold().FontSize(7).FontColor(_primaryColor);
            col.Item().Text($"Fecha Pago: {_pago.FechaPago} | Forma Pago: {_pago.FormaPago} | Moneda: {_pago.Moneda}");
            col.Item().Text($"Monto Total: ${_pago.MontoTotal:N2} | Num. Operación: {_pago.NumOperacion ?? "N/A"}");
        });
    }
}