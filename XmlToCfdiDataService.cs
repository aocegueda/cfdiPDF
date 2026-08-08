using System.Globalization;
using System.Xml.Linq;
using ApiARAConsultoria.DTOs;
using QRCoder;

namespace ApiARAConsultoria.Services.Pdf;

public interface IXmlToCfdiDataService
{
    CfdiDataDto ParsearXml(string xmlContent, byte[]? logoBytes = null, string? direccionSucursal = null);
}

public class XmlToCfdiDataService : IXmlToCfdiDataService
{
    // Namespaces oficiales SAT
    private static readonly XNamespace Cfdi = "http://www.sat.gob.mx/cfd/4";
    private static readonly XNamespace Tfd = "http://www.sat.gob.mx/TimbreFiscalDigital";
    private static readonly XNamespace Pago20 = "http://www.sat.gob.mx/Pagos20";
    private static readonly XNamespace Nomina12 = "http://www.sat.gob.mx/nomina12";
    private static readonly XNamespace CartaPorte31 = "http://www.sat.gob.mx/CartaPorte31";

    public CfdiDataDto ParsearXml(string xmlContent, byte[]? logoBytes = null, string? direccionSucursal = null)
    {
        if (string.IsNullOrWhiteSpace(xmlContent))
            throw new ArgumentException("El contenido XML no puede estar vacío.", nameof(xmlContent));

        var doc = XDocument.Parse(xmlContent);
        var root = doc.Root ?? throw new InvalidOperationException("El XML no contiene un nodo raíz válido.");

        var dto = new CfdiDataDto
        {
            LogoBytes = logoBytes,
            DireccionSucursal = direccionSucursal,
            UsarTemaCorporativo = true, // 👈 Se activa para aplicar la identidad de Papelería Rosy

            // Datos Generales
            Serie = GetAttr(root, "Serie"),
            Folio = GetAttr(root, "Folio"),
            Fecha = GetAttr(root, "Fecha"),
            TipoDeComprobante = GetAttr(root, "TipoDeComprobante", "I"),
            FormaPago = GetAttr(root, "FormaPago"),
            MetodoPago = GetAttr(root, "MetodoPago"),
            Moneda = GetAttr(root, "Moneda", "MXN"),
            TipoCambio = GetDecimal(root, "TipoCambio", 1.0m),
            LugarExpedicion = GetAttr(root, "LugarExpedicion"),
            Exportacion = GetAttr(root, "Exportacion", "01"),

            // Totales
            SubTotal = GetDecimal(root, "SubTotal"),
            Descuento = GetDecimal(root, "Descuento"),
            Total = GetDecimal(root, "Total")
        };

        // Generar importe con letra automáticamente
        dto.ImporteConLetra = NumeroALetrasConverter.Convertir(dto.Total, dto.Moneda);

        // 0. INFORMACIÓN GLOBAL (venta al público en general -- Factura Global) --
        // el DTO y el template ya tenían los campos/la sección lista para esto, pero
        // nunca se leía el nodo del XML, así que nunca se veía nada en el PDF.
        ParsearInformacionGlobal(root, dto);

        // 1. EMISOR Y RECEPTOR
        ParsearEmisorYReceptor(root, dto);

        // 2. TIMBRE FISCAL DIGITAL
        ParsearTimbreFiscal(root, dto);

        // 3. CFDIS RELACIONADOS
        ParsearCfdisRelacionados(root, dto);

        // 4. CONCEPTOS CON SUS IMPUESTOS POR PARTIDA
        ParsearConceptos(root, dto);

        // 5. RESUMEN GLOBAL DE IMPUESTOS (TOTALES)
        ParsearImpuestosGlobales(root, dto);

        // 6. COMPLEMENTOS ESPECIALES
        var complementoNode = root.Element(Cfdi + "Complemento");
        if (complementoNode != null)
        {
            ParsearComplementoPago(complementoNode, dto);
            ParsearComplementoNomina(complementoNode, dto);
            ParsearComplementoCartaPorte(complementoNode, dto);
        }

        // 7. CÓDIGO QR DE VERIFICACIÓN (obligatorio en la representación impresa del SAT)
        // Va al final porque en comprobantes tipo "P" necesita dto.PagoInfo ya parseado.
        if (!string.IsNullOrEmpty(dto.Uuid))
            dto.QrCodeBase64 = GenerarQrCfdi(dto);

        return dto;
    }

    #region Parsers Secundarios

    private static void ParsearInformacionGlobal(XElement root, CfdiDataDto dto)
    {
        var infoGlobal = root.Element(Cfdi + "InformacionGlobal");
        if (infoGlobal == null) return;

        dto.PeriodicidadGlobal = GetAttr(infoGlobal, "Periodicidad");
        dto.PeriodicidadGlobalNombre = ObtenerDescripcionPeriodicidad(dto.PeriodicidadGlobal);
        dto.MesesGlobal = GetAttr(infoGlobal, "Meses");
        dto.MesesGlobalNombre = ObtenerDescripcionMeses(dto.MesesGlobal);
        dto.AnioGlobal = GetAttr(infoGlobal, "Año");
    }

    private static string? ObtenerDescripcionPeriodicidad(string clave) => clave switch
    {
        "01" => "Diario",
        "02" => "Semanal",
        "03" => "Quincenal",
        "04" => "Mensual",
        "05" => "Bimestral",
        _ => null
    };

    private static string? ObtenerDescripcionMeses(string clave) => clave switch
    {
        "01" => "Enero", "02" => "Febrero", "03" => "Marzo", "04" => "Abril",
        "05" => "Mayo", "06" => "Junio", "07" => "Julio", "08" => "Agosto",
        "09" => "Septiembre", "10" => "Octubre", "11" => "Noviembre", "12" => "Diciembre",
        "13" => "Enero-Febrero", "14" => "Marzo-Abril", "15" => "Mayo-Junio",
        "16" => "Julio-Agosto", "17" => "Septiembre-Octubre", "18" => "Noviembre-Diciembre",
        _ => null
    };

    private static void ParsearEmisorYReceptor(XElement root, CfdiDataDto dto)
    {
        var emisor = root.Element(Cfdi + "Emisor");
        if (emisor != null)
        {
            dto.RfcEmisor = GetAttr(emisor, "Rfc");
            dto.NombreEmisor = GetAttr(emisor, "Nombre");
            dto.RegimenEmisor = GetAttr(emisor, "RegimenFiscal");
        }

        var receptor = root.Element(Cfdi + "Receptor");
        if (receptor != null)
        {
            dto.RfcReceptor = GetAttr(receptor, "Rfc");
            dto.NombreReceptor = GetAttr(receptor, "Nombre");
            dto.RegimenReceptor = GetAttr(receptor, "RegimenFiscalReceptor");
            dto.DomicilioReceptor = GetAttr(receptor, "DomicilioFiscalReceptor");
            dto.UsoCfdi = GetAttr(receptor, "UsoCFDI");
        }
    }

    private static void ParsearTimbreFiscal(XElement root, CfdiDataDto dto)
    {
        dto.NoCertificadoEmisor = GetAttr(root, "NoCertificado");

        var complemento = root.Element(Cfdi + "Complemento");
        var timbre = complemento?.Element(Tfd + "TimbreFiscalDigital");
        if (timbre != null)
        {
            dto.Uuid = GetAttr(timbre, "UUID");
            dto.NoCertificadoSAT = GetAttr(timbre, "NoCertificadoSAT");
            dto.FechaTimbrado = GetAttr(timbre, "FechaTimbrado");
            dto.SelloCFD = GetAttr(timbre, "SelloCFD");
            dto.SelloSAT = GetAttr(timbre, "SelloSAT");
            dto.RfcProvCertif = GetAttr(timbre, "RfcProvCertif");

            dto.CadenaOriginalSAT = $"||1.1|{dto.Uuid}|{dto.FechaTimbrado}|{dto.RfcProvCertif}|{dto.SelloCFD}|{dto.NoCertificadoSAT}||";
        }
    }

    private static void ParsearCfdisRelacionados(XElement root, CfdiDataDto dto)
    {
        var relsGroup = root.Elements(Cfdi + "CfdiRelacionados");
        foreach (var group in relsGroup)
        {
            var tipoRelacion = GetAttr(group, "TipoRelacion");
            var itemRel = new CfdiRelacionadoDto
            {
                TipoRelacion = tipoRelacion,
                TipoRelacionDescripcion = ObtenerDescripcionTipoRelacion(tipoRelacion)
            };

            foreach (var cfdiRel in group.Elements(Cfdi + "CfdiRelacionado"))
            {
                var uuid = GetAttr(cfdiRel, "UUID");
                if (!string.IsNullOrEmpty(uuid))
                    itemRel.Uuids.Add(uuid);
            }

            if (itemRel.Uuids.Any())
                dto.CfdisRelacionados.Add(itemRel);
        }
    }

    private static void ParsearConceptos(XElement root, CfdiDataDto dto)
    {
        var conceptosNode = root.Element(Cfdi + "Conceptos");
        if (conceptosNode == null) return;

        foreach (var item in conceptosNode.Elements(Cfdi + "Concepto"))
        {
            var concepto = new ConceptoDto
            {
                ClaveProdServ = GetAttr(item, "ClaveProdServ"),
                NoIdentificacion = GetAttr(item, "NoIdentificacion"),
                Cantidad = GetDecimal(item, "Cantidad"),
                ClaveUnidad = GetAttr(item, "ClaveUnidad"),
                Unidad = GetAttr(item, "Unidad"),
                Descripcion = GetAttr(item, "Descripcion"),
                ValorUnitario = GetDecimal(item, "ValorUnitario"),
                Descuento = GetDecimal(item, "Descuento"),
                Importe = GetDecimal(item, "Importe"),
                ObjetoImp = GetAttr(item, "ObjetoImp", "02")
            };

            // Extracción robusta de impuestos por partida
            var impConcepto = item.Element(Cfdi + "Impuestos");
            if (impConcepto != null)
            {
                // Traslados por partida
                var trasladosNode = impConcepto.Element(Cfdi + "Traslados");
                if (trasladosNode != null)
                {
                    foreach (var tras in trasladosNode.Elements(Cfdi + "Traslado"))
                    {
                        concepto.ImpuestosConcepto.Add(new ImpuestoPartidaDto
                        {
                            Tipo = "Traslado",
                            Impuesto = GetAttr(tras, "Impuesto", "002"),
                            TipoFactor = GetAttr(tras, "TipoFactor", "Tasa"),
                            Base = GetDecimal(tras, "Base"),
                            TasaOCuota = GetDecimal(tras, "TasaOCuota"),
                            Importe = GetDecimal(tras, "Importe")
                        });
                    }
                }

                // Retenciones por partida
                var retencionesNode = impConcepto.Element(Cfdi + "Retenciones");
                if (retencionesNode != null)
                {
                    foreach (var ret in retencionesNode.Elements(Cfdi + "Retencion"))
                    {
                        concepto.ImpuestosConcepto.Add(new ImpuestoPartidaDto
                        {
                            Tipo = "Retencion",
                            Impuesto = GetAttr(ret, "Impuesto", "001"),
                            TipoFactor = GetAttr(ret, "TipoFactor", "Tasa"),
                            Base = GetDecimal(ret, "Base"),
                            TasaOCuota = GetDecimal(ret, "TasaOCuota"),
                            Importe = GetDecimal(ret, "Importe")
                        });
                    }
                }
            }

            dto.Conceptos.Add(concepto);
        }
    }

    private static void ParsearImpuestosGlobales(XElement root, CfdiDataDto dto)
    {
        var impGlobales = root.Element(Cfdi + "Impuestos");
        if (impGlobales == null) return;

        dto.TotalImpuestosTrasladados = GetDecimal(impGlobales, "TotalImpuestosTrasladados");
        dto.TotalImpuestosRetenidos = GetDecimal(impGlobales, "TotalImpuestosRetenidos");

        // Traslados Globales (Totales)
        var traslados = impGlobales.Element(Cfdi + "Traslados");
        if (traslados != null)
        {
            foreach (var tras in traslados.Elements(Cfdi + "Traslado"))
            {
                dto.ImpuestosTrasladados.Add(new ImpuestoResumenDto
                {
                    Impuesto = GetAttr(tras, "Impuesto", "002"),
                    TipoFactor = GetAttr(tras, "TipoFactor", "Tasa"),
                    TasaOCuota = GetDecimal(tras, "TasaOCuota"),
                    Base = GetDecimal(tras, "Base"),
                    Importe = GetDecimal(tras, "Importe")
                });
            }
        }

        // Retenciones Globales (Totales)
        var retenciones = impGlobales.Element(Cfdi + "Retenciones");
        if (retenciones != null)
        {
            foreach (var ret in retenciones.Elements(Cfdi + "Retencion"))
            {
                dto.ImpuestosRetenidos.Add(new ImpuestoResumenDto
                {
                    Impuesto = GetAttr(ret, "Impuesto", "001"),
                    TipoFactor = "Tasa",
                    TasaOCuota = GetDecimal(ret, "TasaOCuota"),
                    Base = GetDecimal(ret, "Base"),
                    Importe = GetDecimal(ret, "Importe")
                });
            }
        }
    }

    private static void ParsearComplementoPago(XElement complementoNode, CfdiDataDto dto)
    {
        var pagosNode = complementoNode.Element(Pago20 + "Pagos");
        if (pagosNode == null) return;

        var pagoItem = pagosNode.Element(Pago20 + "Pago");
        if (pagoItem == null) return;

        var pagoDto = new ComplementoPagoDto
        {
            FechaPago = GetAttr(pagoItem, "FechaPago"),
            FormaPago = GetAttr(pagoItem, "FormaDePagoP"),
            Moneda = GetAttr(pagoItem, "MonedaP", "MXN"),
            TipoCambio = GetDecimal(pagoItem, "TipoCambioP", 1.0m),
            MontoTotal = GetDecimal(pagoItem, "Monto"),
            NumOperacion = GetAttr(pagoItem, "NumOperacion"),
            RfcEmisorCtaOrd = GetAttr(pagoItem, "RfcEmisorCtaOrd"),
            NomBancoOrdExt = GetAttr(pagoItem, "NomBancoOrdExt"),
            CtaOrdenante = GetAttr(pagoItem, "CtaOrdenante"),
            RfcEmisorCtaBen = GetAttr(pagoItem, "RfcEmisorCtaBen"),
            CtaBeneficiario = GetAttr(pagoItem, "CtaBeneficiario")
        };

        foreach (var docRel in pagoItem.Elements(Pago20 + "DoctoRelacionado"))
        {
            pagoDto.DoctosRelacionados.Add(new DocumentoRelacionadoDto
            {
                IdDocumento = GetAttr(docRel, "IdDocumento"),
                Serie = GetAttr(docRel, "Serie"),
                Folio = GetAttr(docRel, "Folio"),
                MonedaDR = GetAttr(docRel, "MonedaDR", "MXN"),
                NumParcialidad = int.TryParse(GetAttr(docRel, "NumParcialidad"), out var np) ? np : 1,
                ImpSaldoAnt = GetDecimal(docRel, "ImpSaldoAnt"),
                ImpPagado = GetDecimal(docRel, "ImpPagado"),
                ImpSaldoInsoluto = GetDecimal(docRel, "ImpSaldoInsoluto"),
                ObjetoImpDR = GetAttr(docRel, "ObjetoImpDR", "02")
            });
        }

        // Resumen global de impuestos del pago (pago20:Totales) -- existía el
        // campo ImpuestosPPD en el DTO pero nunca se llenaba. El nodo Totales usa
        // atributos fijos por tasa (IVA16/IVA8/IVA0/IVAExento) en vez de una lista,
        // así que se arma la lista aquí a partir de esos buckets.
        var totalesNode = pagosNode.Element(Pago20 + "Totales");
        if (totalesNode != null)
        {
            pagoDto.MontoTotalPagos = GetDecimal(totalesNode, "MontoTotalPagos");
            AgregarImpuestoPPDSiAplica(totalesNode, pagoDto, "IVA16", 0.160000m, "Tasa");
            AgregarImpuestoPPDSiAplica(totalesNode, pagoDto, "IVA8", 0.080000m, "Tasa");
            AgregarImpuestoPPDSiAplica(totalesNode, pagoDto, "IVA0", 0.000000m, "Tasa");
            AgregarImpuestoPPDSiAplica(totalesNode, pagoDto, "IVAExento", 0.000000m, "Exento");
        }

        dto.PagoInfo = pagoDto;
    }

    private static void AgregarImpuestoPPDSiAplica(XElement totalesNode, ComplementoPagoDto pagoDto, string sufijo, decimal tasa, string tipoFactor)
    {
        var baseVal = GetDecimal(totalesNode, $"TotalTrasladosBase{sufijo}");
        if (baseVal <= 0) return;

        pagoDto.ImpuestosPPD.Add(new ImpuestoResumenDto
        {
            Impuesto = "002",
            TipoFactor = tipoFactor,
            TasaOCuota = tasa,
            Base = baseVal,
            Importe = GetDecimal(totalesNode, $"TotalTrasladosImpuesto{sufijo}")
        });
    }

    private static void ParsearComplementoNomina(XElement complementoNode, CfdiDataDto dto)
    {
        var nominaNode = complementoNode.Element(Nomina12 + "Nomina");
        if (nominaNode == null) return;

        var receptor = nominaNode.Element(Nomina12 + "Receptor");
        var percepciones = nominaNode.Element(Nomina12 + "Percepciones");
        var deducciones = nominaNode.Element(Nomina12 + "Deducciones");
        var otrosPagos = nominaNode.Element(Nomina12 + "OtrosPagos");

        var nomDto = new ComplementoNominaDto
        {
            TipoNomina = GetAttr(nominaNode, "TipoNomina", "O"),
            FechaPago = GetAttr(nominaNode, "FechaPago"),
            FechaInicialPago = GetAttr(nominaNode, "FechaInicialPago"),
            FechaFinalPago = GetAttr(nominaNode, "FechaFinalPago"),
            DiasPagados = GetDecimal(nominaNode, "NumDiasPagados"),
            TotalPercepciones = GetDecimal(nominaNode, "TotalPercepciones"),
            TotalDeducciones = GetDecimal(nominaNode, "TotalDeducciones"),
            TotalOtrosPagos = GetDecimal(nominaNode, "TotalOtrosPagos"),

            NumEmpleado = GetAttr(receptor, "NumEmpleado"),
            Curp = GetAttr(receptor, "Curp"),
            Nss = GetAttr(receptor, "Nss"),
            Puesto = GetAttr(receptor, "Puesto"),
            Departamento = GetAttr(receptor, "Departamento"),
            TipoContrato = GetAttr(receptor, "TipoContrato"),
            TipoJornada = GetAttr(receptor, "TipoJornada"),
            TipoRegimen = GetAttr(receptor, "TipoRegimen"),
            PeriodicidadPago = GetAttr(receptor, "PeriodicidadPago"),
            ClaveEntFed = GetAttr(receptor, "ClaveEntFed"),
            FechaInicioRelLaboral = GetAttr(receptor, "FechaInicioRelLaboral")
        };

        if (percepciones != null)
        {
            foreach (var p in percepciones.Elements(Nomina12 + "Percepcion"))
            {
                nomDto.Percepciones.Add(new DetalleNominaItemDto
                {
                    Clave = GetAttr(p, "Clave"),
                    TipoClave = GetAttr(p, "TipoPercepcion"),
                    Concepto = GetAttr(p, "Concepto"),
                    ImporteGravado = GetDecimal(p, "ImporteGravado"),
                    ImporteExento = GetDecimal(p, "ImporteExento"),
                    Importe = GetDecimal(p, "ImporteGravado") + GetDecimal(p, "ImporteExento")
                });
            }
        }

        if (deducciones != null)
        {
            foreach (var d in deducciones.Elements(Nomina12 + "Deduccion"))
            {
                nomDto.Deducciones.Add(new DetalleNominaItemDto
                {
                    Clave = GetAttr(d, "Clave"),
                    TipoClave = GetAttr(d, "TipoDeduccion"),
                    Concepto = GetAttr(d, "Concepto"),
                    Importe = GetDecimal(d, "Importe")
                });
            }
        }

        if (otrosPagos != null)
        {
            foreach (var op in otrosPagos.Elements(Nomina12 + "OtroPago"))
            {
                nomDto.OtrosPagos.Add(new DetalleNominaItemDto
                {
                    Clave = GetAttr(op, "Clave"),
                    TipoClave = GetAttr(op, "TipoOtroPago"),
                    Concepto = GetAttr(op, "Concepto"),
                    Importe = GetDecimal(op, "Importe")
                });
            }
        }

        dto.NominaInfo = nomDto;
    }

    private static void ParsearComplementoCartaPorte(XElement complementoNode, CfdiDataDto dto)
    {
        var cpNode = complementoNode.Element(CartaPorte31 + "CartaPorte");
        if (cpNode == null) return;

        var cpDto = new ComplementoCartaPorteDto
        {
            IdCCP = GetAttr(cpNode, "IdCCP"),
            TranspInternac = GetAttr(cpNode, "TranspInternac", "No"),
            TotalDistRec = GetAttr(cpNode, "TotalDistRec", "0")
        };

        var mercancias = cpNode.Element(CartaPorte31 + "Mercancias");
        if (mercancias != null)
        {
            var autotransporte = mercancias.Element(CartaPorte31 + "Autotransporte");
            if (autotransporte != null)
            {
                cpDto.ConfigVehicular = GetAttr(autotransporte, "ConfigVehicular");
                var idVehiculo = autotransporte.Element(CartaPorte31 + "IdentificacionVehicular");
                if (idVehiculo != null)
                {
                    cpDto.Placas = GetAttr(idVehiculo, "PlacaVM");
                    cpDto.Modelo = GetAttr(idVehiculo, "AnioModeloVM");
                }

                var seguros = autotransporte.Element(CartaPorte31 + "Seguros");
                if (seguros != null)
                {
                    cpDto.Aseguradora = GetAttr(seguros, "AseguraRespCivil");
                    cpDto.Poliza = GetAttr(seguros, "PolizaRespCivil");
                }
            }

            foreach (var m in mercancias.Elements(CartaPorte31 + "Mercancia"))
            {
                cpDto.Mercancias.Add(new MercanciaItemDto
                {
                    BienesTransp = GetAttr(m, "BienesTransp"),
                    Descripcion = GetAttr(m, "Descripcion"),
                    Cantidad = GetDecimal(m, "Cantidad"),
                    ClaveUnidad = GetAttr(m, "ClaveUnidad"),
                    PesoEnKg = GetDecimal(m, "PesoEnKg"),
                    ValorMercancia = GetDecimal(m, "ValorMercancia")
                });
            }
        }

        dto.CartaPorteInfo = cpDto;
    }

    #endregion

    #region Código QR

    /// <summary>
    /// Arma la URL oficial de verificación del SAT y genera el QR correspondiente
    /// (formato definido en el Anexo 20: id, re, rr, tt a 6 decimales, fe = últimos 8
    /// caracteres del sello digital del emisor). Usa PngByteQRCode porque es 100%
    /// managed -- QRCode/Bitmap de QRCoder depende de System.Drawing y no corre en
    /// el VPS Linux donde se despliega esto.
    ///
    /// En comprobantes tipo "P" (Complemento de Pago) Comprobante/@Total siempre es 0
    /// -- el SAT no reconoce el folio en la verificación por URL si "tt" va en 0, hay
    /// que mandar la suma real de los pagos (pago20:Totales/@MontoTotalPagos). Confirmado
    /// probando ambas variantes contra el sitio real del SAT.
    /// </summary>
    private static string GenerarQrCfdi(CfdiDataDto dto)
    {
        string selloCorto = dto.SelloCFD.Length >= 8 ? dto.SelloCFD[^8..] : dto.SelloCFD;
        decimal totalParaQr = dto.TipoDeComprobante == "P" && dto.PagoInfo != null
            ? dto.PagoInfo.MontoTotalPagos
            : dto.Total;

        string url = "https://verificacfdi.facturaelectronica.sat.gob.mx/default.aspx" +
            $"?id={dto.Uuid}" +
            $"&re={Uri.EscapeDataString(dto.RfcEmisor)}" +
            $"&rr={Uri.EscapeDataString(dto.RfcReceptor)}" +
            $"&tt={totalParaQr.ToString("F6", CultureInfo.InvariantCulture)}" +
            $"&fe={Uri.EscapeDataString(selloCorto)}";

        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
        var pngQr = new PngByteQRCode(qrCodeData);
        return Convert.ToBase64String(pngQr.GetGraphic(20));
    }

    #endregion

    #region Helpers de XML y Diccionarios

    private static string GetAttr(XElement? element, string attributeName, string defaultValue = "")
    {
        return element?.Attribute(attributeName)?.Value ?? defaultValue;
    }

    private static decimal GetDecimal(XElement? element, string attributeName, decimal defaultValue = 0m)
    {
        var val = element?.Attribute(attributeName)?.Value;
        return decimal.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out var res) ? res : defaultValue;
    }

    private static string ObtenerDescripcionTipoRelacion(string clave) => clave switch
    {
        "01" => "01 - Nota de crédito de los documentos relacionados",
        "02" => "02 - Nota de débito de los documentos relacionados",
        "03" => "03 - Devolución de mercancía sobre facturas previas",
        "04" => "04 - Sustitución de los CFDI previos",
        "05" => "05 - Traslados de mercancías efectuados con anterioridad",
        "06" => "06 - Factura generada por los traslados previos",
        "07" => "07 - CFDI por aplicación de anticipo",
        _ => $"{clave} - Documento Relacionado"
    };

    #endregion
}

public static class NumeroALetrasConverter
{
    public static string Convertir(decimal numero, string moneda = "MXN")
    {
        long entero = (long)Math.Floor(numero);
        int centavos = (int)Math.Round((numero - entero) * 100);

        string textoEntero = ConvertirEntero(entero);
        string sufijoMoneda = moneda.ToUpper() == "USD" ? "DÓLARES" : "PESOS";
        string abreviaturaMoneda = moneda.ToUpper() == "USD" ? "USD" : "M.N.";

        return $"({textoEntero} {sufijoMoneda} {centavos:D2}/100 {abreviaturaMoneda})".ToUpper();
    }

    private static string ConvertirEntero(long n)
    {
        if (n == 0) return "CERO";
        if (n < 0) return "MENOS " + ConvertirEntero(Math.Abs(n));

        if (n == 100) return "CIEN";
        if (n < 100)
        {
            string[] decenas = { "", "DIEZ", "VEINTE", "TREINTA", "CUARENTA", "CINCUENTA", "SESENTA", "SETENTA", "OCHENTA", "NOVENTA" };
            string[] unidades = { "", "UN", "DOS", "TRES", "CUATRO", "CINCO", "SEIS", "SIETE", "OCHO", "NUEVE" };
            string[] especiales = { "DIEZ", "ONCE", "DOCE", "TRECE", "CATORCE", "QUINCE", "DIECISÉIS", "DIECISIETE", "DIECIOCHO", "DIECINUEVE" };

            if (n < 10) return unidades[n];
            if (n >= 10 && n < 20) return especiales[n - 10];
            if (n >= 21 && n <= 29) return "VEINTI" + unidades[n - 20];

            long d = n / 10;
            long u = n % 10;
            return decenas[d] + (u > 0 ? " Y " + unidades[u] : "");
        }

        if (n < 1000)
        {
            // Corregido "OCHOCIENTOS"
            string[] cientos = { "", "CIENTO", "DOSCIENTOS", "TRESCIENTOS", "CUATROCIENTOS", "QUINIENTOS", "SEISCIENTOS", "SETECIENTOS", "OCHOCIENTOS", "NOVECIENTOS" };
            long c = n / 100;
            long resto = n % 100;
            return cientos[c] + (resto > 0 ? " " + ConvertirEntero(resto) : "");
        }

        if (n < 1000000)
        {
            long miles = n / 1000;
            long resto = n % 1000;
            string prefix = miles == 1 ? "MIL" : ConvertirEntero(miles) + " MIL";
            return prefix + (resto > 0 ? " " + ConvertirEntero(resto) : "");
        }

        if (n < 1000000000)
        {
            long millones = n / 1000000;
            long resto = n % 1000000;
            string prefix = millones == 1 ? "UN MILLÓN" : ConvertirEntero(millones) + " MILLONES";
            return prefix + (resto > 0 ? " " + ConvertirEntero(resto) : "");
        }

        return n.ToString();
    }
}