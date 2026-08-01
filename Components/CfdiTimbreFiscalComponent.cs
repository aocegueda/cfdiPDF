using ApiARAConsultoria.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace ApiARAConsultoria.Services.Pdf.Components;

public class CfdiTimbreFiscalComponent : IComponent
{
    private readonly CfdiDataDto _model;
    private readonly string _primaryColor;

    public CfdiTimbreFiscalComponent(CfdiDataDto model, string primaryColor)
    {
        _model = model;
        _primaryColor = primaryColor;
    }

    public void Compose(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().Text($"Folio Fiscal (UUID): {_model.Uuid}").Bold().FontSize(7);
            col.Item().Text($"No. Certificado SAT: {_model.NoCertificadoSAT} | No. Certificado Emisor: {_model.NoCertificadoEmisor}");
            col.Item().Text($"Fecha Timbrado: {_model.FechaTimbrado} | RFC PAC: {_model.RfcProvCertif}");
            col.Item().Text($"Sello Digital CFD: {_model.SelloCFD}").FontSize(6);
            col.Item().Text($"Sello Digital SAT: {_model.SelloSAT}").FontSize(6);
            col.Item().Text($"Cadena Original SAT: {_model.CadenaOriginalSAT}").FontSize(6);
        });
    }
}