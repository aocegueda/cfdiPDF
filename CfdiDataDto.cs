namespace ApiARAConsultoria.DTOs;

/// <summary>
/// Modelo principal que contiene los datos requeridos para renderizar
/// las representaciones impresas (PDFs) de los CFDIs de Papelería Rosy.
/// </summary>
public class CfdiDataDto
{
    // ==========================================
    // CONFIGURACIÓN VISUAL Y DE DISEÑO
    // ==========================================
    public string? DireccionSucursal { get; set; }
    public bool UsarTemaCorporativo { get; set; } = true; // true = Paleta Rosy | false = Default Azul
    public byte[]? LogoBytes { get; set; }

    // ==========================================
    // 1. DATOS DEL EMISOR (Papelería Rosy)
    // ==========================================
    public string RfcEmisor { get; set; } = string.Empty;
    public string NombreEmisor { get; set; } = string.Empty;
    public string RegimenEmisor { get; set; } = string.Empty;
    public string? RegimenEmisorNombre { get; set; } // [AGREGADO]: descripción del catálogo SAT (CatRegimenFiscal)
    public string LugarExpedicion { get; set; } = string.Empty; // C.P. de expedición

    // ==========================================
    // 2. DATOS DEL RECEPTOR (Cliente / Empleado)
    // ==========================================
    public string RfcReceptor { get; set; } = string.Empty;
    public string NombreReceptor { get; set; } = string.Empty;
    public string RegimenReceptor { get; set; } = string.Empty;
    public string? RegimenReceptorNombre { get; set; } // [AGREGADO]
    public string DomicilioReceptor { get; set; } = string.Empty; // C.P. del receptor (Obligatorio en CFDI 4.0)
    public string UsoCfdi { get; set; } = string.Empty;
    public string? UsoCfdiNombre { get; set; } // [AGREGADO]: descripción del catálogo SAT (CatUsoCFDI)

    // ==========================================
    // 3. DATOS GENERALES DEL COMPROBANTE
    // ==========================================
    public string Serie { get; set; } = string.Empty;
    public string Folio { get; set; } = string.Empty;
    public string Fecha { get; set; } = string.Empty;
    public string TipoDeComprobante { get; set; } = string.Empty; // I, E, P, N, T
    public string FormaPago { get; set; } = string.Empty;
    public string? FormaPagoNombre { get; set; } // [AGREGADO]: descripción del catálogo (CatFormasPago)
    public string MetodoPago { get; set; } = string.Empty;
    public string? MetodoPagoNombre { get; set; } // [AGREGADO]: descripción del catálogo (CatMetodosPago)
    public string Moneda { get; set; } = "MXN";
    public decimal TipoCambio { get; set; } = 1.0m; // [AGREGADO]: Requerido si es en USD u otra moneda
    public string ImporteConLetra { get; set; } = string.Empty; // [AGREGADO]: Indispensable en representaciones impresas
    public string Exportacion { get; set; } = "01"; // [AGREGADO]: Requerido CFDI 4.0 (01 = No aplica)

    // Totales Globales
    public decimal SubTotal { get; set; }
    public decimal Descuento { get; set; }
    public decimal TotalImpuestosTrasladados { get; set; } // [AGREGADO]
    public decimal TotalImpuestosRetenidos { get; set; }   // [AGREGADO]
    public decimal Total { get; set; }

    // Datos Factura Global (Si aplica para Venta Mostrador en Papelería)
    public string? PeriodicidadGlobal { get; set; }
    public string? PeriodicidadGlobalNombre { get; set; } // [AGREGADO]: descripción del catálogo SAT (c_Periodicidad)
    public string? MesesGlobal { get; set; }
    public string? MesesGlobalNombre { get; set; } // [AGREGADO]: descripción del catálogo SAT (c_Meses)
    public string? AnioGlobal { get; set; }

    // ==========================================
    // 4. TIMBRE FISCAL DIGITAL (Respuesta PAC/SAT)
    // ==========================================
    public string Uuid { get; set; } = string.Empty;
    public string NoCertificadoEmisor { get; set; } = string.Empty;
    public string NoCertificadoSAT { get; set; } = string.Empty;
    public string FechaTimbrado { get; set; } = string.Empty;
    public string RfcProvCertif { get; set; } = string.Empty; // [AGREGADO]: Obligatorio mostrar el RFC del PAC
    public string CadenaOriginalSAT { get; set; } = string.Empty;
    public string SelloCFD { get; set; } = string.Empty;
    public string SelloSAT { get; set; } = string.Empty;
    public string QrCodeBase64 { get; set; } = string.Empty; // Renderizado en imagen Base64

    // ==========================================
    // 5. COLECCIONES, IMPUESTOS Y COMPLEMENTOS
    // ==========================================

    // [AGREGADO]: CFDIs Relacionados (Ej: Notas de crédito, cancelación previas, sustitución)
    public List<CfdiRelacionadoDto> CfdisRelacionados { get; set; } = new();

    // Conceptos (Factura Normal / Traslado)
    public List<ConceptoDto> Conceptos { get; set; } = new();

    // [AGREGADO]: Resumen Global de Impuestos
    public List<ImpuestoResumenDto> ImpuestosTrasladados { get; set; } = new();
    public List<ImpuestoResumenDto> ImpuestosRetenidos { get; set; } = new();

    // Complemento de Pago 2.0 (Solo si TipoDeComprobante == "P")
    public ComplementoPagoDto? PagoInfo { get; set; }

    // Complemento Nómina 1.2 (Solo si TipoDeComprobante == "N")
    public ComplementoNominaDto? NominaInfo { get; set; }

    // Complemento Carta Porte 3.1 (Solo si aplica Traslado)
    public ComplementoCartaPorteDto? CartaPorteInfo { get; set; }

    /// <summary>
    /// "clave - nombre" cuando el catálogo se pudo resolver; si no, solo la clave
    /// (nunca deja el renglón vacío por un catálogo que no matcheó o no se consultó).
    /// </summary>
    public static string ClaveConNombre(string clave, string? nombre) =>
        string.IsNullOrWhiteSpace(nombre) ? clave : $"{clave} - {nombre}";
}

#region Sub-Classes DTOs

public class ConceptoDto
{
    public string ClaveProdServ { get; set; } = string.Empty;
    public string NoIdentificacion { get; set; } = string.Empty; // Código interno/Código de barras
    public decimal Cantidad { get; set; }
    public string ClaveUnidad { get; set; } = string.Empty;
    public string Unidad { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public decimal ValorUnitario { get; set; }
    public decimal Descuento { get; set; }
    public decimal Importe { get; set; }
    
    public string ObjetoImp { get; set; } = "02"; // [AGREGADO] 01 = No Objeto, 02 = Sí Objeto, 03 = Sí Objeto no obligado

    // [AGREGADO]: Impuestos a nivel de Partida/Concepto (Exigido en CFDI 4.0)
    public List<ImpuestoPartidaDto> ImpuestosConcepto { get; set; } = new();
}

/// <summary>
/// [AGREGADO]: Estructura para mostrar impuestos por cada concepto en la tabla
/// </summary>
public class ImpuestoPartidaDto
{
    public string Tipo { get; set; } = "Traslado"; // Traslado o Retencion
    public string Impuesto { get; set; } = "002"; // 002 = IVA, 001 = ISR, 003 = IEPS
    public string TipoFactor { get; set; } = "Tasa"; // Tasa, Cuota, Exento
    public decimal Base { get; set; }
    public decimal TasaOCuota { get; set; }
    public decimal Importe { get; set; }
}

/// <summary>
/// [AGREGADO]: Estructura para la tabla final de desglose de totales de impuestos
/// </summary>
public class ImpuestoResumenDto
{
    public string Impuesto { get; set; } = "002"; // 002 = IVA, 001 = ISR
    public string TipoFactor { get; set; } = "Tasa";
    public decimal TasaOCuota { get; set; } // 0.160000, 0.080000, 0.000000
    public decimal Base { get; set; }
    public decimal Importe { get; set; }
}

/// <summary>
/// [AGREGADO]: Para manejar relación de CFDIs (Notas de Crédito, Cancelaciones, Parcialidades)
/// </summary>
public class CfdiRelacionadoDto
{
    public string TipoRelacion { get; set; } = string.Empty; // Ej: "01" (Nota de crédito), "04" (Sustitución)
    public string TipoRelacionDescripcion { get; set; } = string.Empty;
    public List<string> Uuids { get; set; } = new();
}

public class ComplementoPagoDto
{
    public string FechaPago { get; set; } = string.Empty;
    public string FormaPago { get; set; } = string.Empty;
    public string? FormaPagoNombre { get; set; } // [AGREGADO]: descripción del catálogo (CatFormasPago)
    public string Moneda { get; set; } = "MXN";
    public decimal TipoCambio { get; set; } = 1.0m;
    public decimal MontoTotal { get; set; }
    public decimal MontoTotalPagos { get; set; } // Suma de todos los pagos del complemento (pago20:Totales) -- es el valor que exige el QR de verificación del SAT en comprobantes tipo "P", ya que Comprobante/@Total siempre es 0
    public string? NumOperacion { get; set; }
    
    // [AGREGADO]: Datos bancarios opcionales en Pago 2.0
    public string? RfcEmisorCtaOrd { get; set; }
    public string? NomBancoOrdExt { get; set; }
    public string? CtaOrdenante { get; set; }
    public string? RfcEmisorCtaBen { get; set; }
    public string? CtaBeneficiario { get; set; }

    public List<DocumentoRelacionadoDto> DoctosRelacionados { get; set; } = new();
    
    // [AGREGADO]: Impuestos Totales del Pago
    public List<ImpuestoResumenDto> ImpuestosPPD { get; set; } = new();
}

public class DocumentoRelacionadoDto
{
    public string IdDocumento { get; set; } = string.Empty; // UUID Factura Original
    public string Serie { get; set; } = string.Empty;
    public string Folio { get; set; } = string.Empty;
    public string MonedaDR { get; set; } = "MXN"; // [AGREGADO]
    public int NumParcialidad { get; set; }
    public decimal ImpSaldoAnt { get; set; }
    public decimal ImpPagado { get; set; }
    public decimal ImpSaldoInsoluto { get; set; }
    public string ObjetoImpDR { get; set; } = "02"; // [AGREGADO]
}

public class ComplementoNominaDto
{
    public string TipoNomina { get; set; } = "O"; // [AGREGADO]: O = Ordinaria, E = Extraordinaria
    public string FechaPago { get; set; } = string.Empty; // [AGREGADO]
    public string FechaInicialPago { get; set; } = string.Empty; // [AGREGADO]
    public string FechaFinalPago { get; set; } = string.Empty; // [AGREGADO]
    public string NumEmpleado { get; set; } = string.Empty;
    public string Curp { get; set; } = string.Empty;
    public string Nss { get; set; } = string.Empty;
    public string Puesto { get; set; } = string.Empty;
    public string Departamento { get; set; } = string.Empty;
    public string TipoContrato { get; set; } = string.Empty; // [AGREGADO]
    public string TipoJornada { get; set; } = string.Empty; // [AGREGADO]
    public string TipoRegimen { get; set; } = string.Empty; // [AGREGADO]
    public string PeriodicidadPago { get; set; } = string.Empty; // [AGREGADO]: Quincenal, Semanal, etc.
    public string ClaveEntFed { get; set; } = string.Empty; // [AGREGADO]: Estado donde labora
    public string FechaInicioRelLaboral { get; set; } = string.Empty;
    public decimal DiasPagados { get; set; }
    public decimal TotalPercepciones { get; set; }
    public decimal TotalDeducciones { get; set; }
    public decimal TotalOtrosPagos { get; set; } // [AGREGADO]: Subsidio al empleo, devueltos, etc.

    public List<DetalleNominaItemDto> Percepciones { get; set; } = new();
    public List<DetalleNominaItemDto> Deducciones { get; set; } = new();
    public List<DetalleNominaItemDto> OtrosPagos { get; set; } = new(); // [AGREGADO]
}

public class DetalleNominaItemDto
{
    public string Clave { get; set; } = string.Empty;
    public string TipoClave { get; set; } = string.Empty;
    public string Concepto { get; set; } = string.Empty;
    public decimal ImporteExento { get; set; } // [AGREGADO]: Importante para exenciones de ISR
    public decimal ImporteGravado { get; set; } // [AGREGADO]
    public decimal Importe { get; set; }
}

public class ComplementoCartaPorteDto
{
    public string IdCCP { get; set; } = string.Empty; // [AGREGADO]: Requerido en CCP 3.0 / 3.1
    public string TranspInternac { get; set; } = "No"; // [AGREGADO]
    public string TotalDistRec { get; set; } = "0"; // [AGREGADO]
    public string OrigenDireccion { get; set; } = string.Empty;
    public string DestinoDireccion { get; set; } = string.Empty;
    public string FechaSalida { get; set; } = string.Empty;
    public string FechaLlegada { get; set; } = string.Empty;
    public string ConfigVehicular { get; set; } = string.Empty; // [AGREGADO]: Clave autotransporte
    public string Placas { get; set; } = string.Empty;
    public string Modelo { get; set; } = string.Empty;
    public string Aseguradora { get; set; } = string.Empty;
    public string Poliza { get; set; } = string.Empty;
    public List<MercanciaItemDto> Mercancias { get; set; } = new();
}

public class MercanciaItemDto
{
    public string BienesTransp { get; set; } = string.Empty; // [AGREGADO]: Clave SAT de mercancía
    public string Descripcion { get; set; } = string.Empty;
    public decimal Cantidad { get; set; }
    public string ClaveUnidad { get; set; } = string.Empty; // [AGREGADO]
    public decimal PesoEnKg { get; set; }
    public decimal ValorMercancia { get; set; }
}

#endregion