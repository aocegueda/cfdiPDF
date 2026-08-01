using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using ApiARAConsultoria.DTOs;
using ApiARAConsultoria.Services.Pdf.Templates;
using ApiARAConsultoria.Services.Pdf.Components;

namespace ApiARAConsultoria.Services.Pdf;

public interface ICfdiPdfGeneratorService
{
    byte[] GenerarPdfCfdi(CfdiDataDto data);
    Task<byte[]> GenerarPdfDesdeXmlAsync(string xmlContent, byte[]? logoBytes = null, string? direccionSucursal = null);
}

public class CfdiPdfGeneratorService : ICfdiPdfGeneratorService
{
    private readonly IXmlToCfdiDataService _xmlParserService;
    private readonly ICfdiPdfCatalogProvider _catalogProvider;

    public CfdiPdfGeneratorService(
        IXmlToCfdiDataService xmlParserService,
        ICfdiPdfCatalogProvider catalogProvider)
    {
        _xmlParserService = xmlParserService;
        _catalogProvider = catalogProvider;
    }

    public byte[] GenerarPdfCfdi(CfdiDataDto data)
    {
        var tema = data.UsarTemaCorporativo ? CfdiTheme.RosyTheme : CfdiTheme.Default;
        CfdiPdfStyles.SetTheme(tema);

        // Se decide por el complemento presente, no solo por TipoDeComprobante:
        // Carta Porte puede venir en un CFDI tipo "I" (flete cobrado, el caso más
        // común) o "T" (traslado puro) -- antes solo se detectaba "T" y un "I" con
        // Carta Porte caía en FacturaIngresoTemplate, perdiendo toda la sección de
        // ubicaciones/mercancías/autotransporte.
        IDocument document = data switch
        {
            { PagoInfo: not null } => new ComplementoPagoTemplate(data),
            { NominaInfo: not null } => new NominaTemplate(data),
            { CartaPorteInfo: not null } => new CartaPorteTemplate(data),
            _ => new FacturaIngresoTemplate(data)
        };

        return document.GeneratePdf();
    }

    public async Task<byte[]> GenerarPdfDesdeXmlAsync(string xmlContent, byte[]? logoBytes = null, string? direccionSucursal = null)
    {
        // 1. Usar tu parser original (IXmlToCfdiDataService)
        var dto = _xmlParserService.ParsearXml(xmlContent);

        // 2. Asignar los datos visuales adicionales al DTO -- si no mandan un logo
        // explícito, se usa el logo default que provea el host (ICfdiPdfCatalogProvider).
        dto.LogoBytes = logoBytes ?? _catalogProvider.GetLogoDefault();
        dto.DireccionSucursal = direccionSucursal;
        dto.UsarTemaCorporativo = true;

        // 2.1 Completar clave con nombre (Régimen Fiscal, Uso CFDI, Forma/Método
        // de Pago) vía el provider del host -- si alguna clave no matchea (catálogo
        // desactualizado, dato legado, o el host no tiene esos catálogos como
        // PapeleriaCentral) se deja el campo *Nombre en null y el PDF cae de vuelta
        // a mostrar solo la clave.
        await EnriquecerCatalogosAsync(dto);

        // 3. Renderizar el PDF llamando a la plantilla correspondiente
        return GenerarPdfCfdi(dto);
    }

    private async Task EnriquecerCatalogosAsync(CfdiDataDto dto)
    {
        if (!string.IsNullOrWhiteSpace(dto.RegimenEmisor))
            dto.RegimenEmisorNombre = await _catalogProvider.GetRegimenFiscalNombreAsync(dto.RegimenEmisor);

        if (!string.IsNullOrWhiteSpace(dto.RegimenReceptor))
            dto.RegimenReceptorNombre = await _catalogProvider.GetRegimenFiscalNombreAsync(dto.RegimenReceptor);

        if (!string.IsNullOrWhiteSpace(dto.UsoCfdi))
            dto.UsoCfdiNombre = await _catalogProvider.GetUsoCfdiNombreAsync(dto.UsoCfdi);

        if (!string.IsNullOrWhiteSpace(dto.FormaPago))
            dto.FormaPagoNombre = await _catalogProvider.GetFormaPagoNombreAsync(dto.FormaPago);

        if (!string.IsNullOrWhiteSpace(dto.MetodoPago))
            dto.MetodoPagoNombre = await _catalogProvider.GetMetodoPagoNombreAsync(dto.MetodoPago);

        // Forma de pago propia del Complemento de Pago (FormaPagoP) -- es un
        // catálogo independiente del FormaPago del comprobante principal.
        if (dto.PagoInfo != null && !string.IsNullOrWhiteSpace(dto.PagoInfo.FormaPago))
            dto.PagoInfo.FormaPagoNombre = await _catalogProvider.GetFormaPagoNombreAsync(dto.PagoInfo.FormaPago);
    }
}
