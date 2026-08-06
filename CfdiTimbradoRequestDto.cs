namespace PapeleriaRosy.CfdiPdf;

/// <summary>
/// Datos neutrales para timbrar un CFDI de ingreso (ticket individual) ante SW Sapien.
/// Cada proyecto host (ApiARAConsultoria, PapeleriaCentral) mapea sus propias entidades
/// (Venta/Cliente, VentaSync/ReceptorFacturaDto) a este DTO antes de llamar a
/// <see cref="ISwSapienTimbradoService.TimbrarAsync"/>.
/// </summary>
public class CfdiTimbradoRequestDto
{
    public string Serie { get; set; } = string.Empty;
    public string Folio { get; set; } = string.Empty;
    public string FormaPago { get; set; } = "01";
    public string MetodoPago { get; set; } = "PUE";

    // XAXX010101000 + "PUBLICO EN GENERAL" son los defaults SAT para receptor genérico.
    public string RfcReceptor { get; set; } = "XAXX010101000";
    public string NombreReceptor { get; set; } = "PUBLICO EN GENERAL";
    public string? CodigoPostalReceptor { get; set; }
    public string RegimenFiscalReceptor { get; set; } = "616";
    public string UsoCfdi { get; set; } = "S01";

    public List<ConceptoTimbradoDto> Conceptos { get; set; } = new();
}

public class ConceptoTimbradoDto
{
    public string ClaveProdServ { get; set; } = "01010101";
    public string NoIdentificacion { get; set; } = "S/C";
    public decimal Cantidad { get; set; }
    public string ClaveUnidad { get; set; } = "H87";
    public string Descripcion { get; set; } = "PRODUCTO";
    public decimal PrecioUnitario { get; set; }
    public decimal Descuento { get; set; }

    // "Tasa" o "Exento". Si es "Tasa", TasaImpuesto es el porcentaje (16 = 16%).
    public string TipoFactor { get; set; } = "Tasa";

    // 002 = IVA, 003 = IEPS.
    public string ImpuestoClave { get; set; } = "002";
    public decimal TasaImpuesto { get; set; } = 16m;
}
