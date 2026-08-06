using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace PapeleriaRosy.CfdiPdf;

public class RespuestaTimbradoDto
{
    public bool Exito { get; set; }
    public string? Uuid { get; set; }
    public string? CadenaOriginalSAT { get; set; }
    public string? XmlTimbrado { get; set; }
    public string? MensajeError { get; set; }
}

public interface ISwSapienTimbradoService
{
    Task<RespuestaTimbradoDto> TimbrarAsync(CfdiTimbradoRequestDto datos);
    Task<byte[]> GenerarPdfAsync(string xmlContent);
}

/// <summary>
/// Timbrado de un CFDI de ingreso (ticket individual) contra SW Sapien -- lógica que
/// antes vivía duplicada en ApiARAConsultoria/Services/SWPacService.cs
/// (TimbrarConCertificadoEnPanelAsync) y PapeleriaCentral/Services/SwPacTimbradoService.cs
/// (un "puerto" manual documentado como tal). Esa duplicación ya causó un bug real --
/// el nodo InformacionGlobal para receptores de público en general se corrigió en cada
/// copia por separado. Esta es ahora la única fuente de verdad para ambos proyectos;
/// cada uno mapea sus propias entidades (Venta/Cliente, VentaSync/ReceptorFacturaDto) a
/// <see cref="CfdiTimbradoRequestDto"/> antes de llamar aquí.
/// </summary>
public class SwSapienTimbradoService : ISwSapienTimbradoService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<SwSapienTimbradoService> _logger;

    public SwSapienTimbradoService(HttpClient httpClient, IConfiguration config, ILogger<SwSapienTimbradoService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
    }

    public async Task<RespuestaTimbradoDto> TimbrarAsync(CfdiTimbradoRequestDto datos)
    {
        string logSerie = datos.Serie.Trim();
        string logFolio = datos.Folio.Trim();

        try
        {
            string token = await ObtenerTokenAsync();

            var conceptosValidos = datos.Conceptos.Where(c => c.PrecioUnitario > 0).ToList();
            if (conceptosValidos.Count == 0)
                return new RespuestaTimbradoDto { Exito = false, MensajeError = "El comprobante no contiene conceptos válidos." };

            // ── Desglose fiscal por línea ────────────────────────────────────────
            var conceptosCalculados = conceptosValidos.Select(CalcularConcepto).ToList();

            var subTotalFactura = conceptosCalculados.Sum(c => c.ImporteBruto);
            var descuentoTotalFact = conceptosCalculados.Sum(c => c.DescuentoBase);

            var listaTrasladosGlobales = GenerarTrasladosGlobales(conceptosCalculados);

            decimal totalTraslados = 0m;
            foreach (var trasladoObj in listaTrasladosGlobales)
            {
                string jsonTemporal = JsonSerializer.Serialize(trasladoObj);
                using var docTemporal = JsonDocument.Parse(jsonTemporal);
                if (docTemporal.RootElement.TryGetProperty("Importe", out var elementoImporte) &&
                    decimal.TryParse(elementoImporte.GetString(), out decimal importeParseado))
                {
                    totalTraslados += importeParseado;
                }
            }
            totalTraslados = Math.Round(totalTraslados, 2, MidpointRounding.AwayFromZero);

            object? nodoImpuestosGlobal = null;
            if (listaTrasladosGlobales.Count > 0)
            {
                nodoImpuestosGlobal = totalTraslados > 0
                    ? new { TotalImpuestosTrasladados = totalTraslados.ToString("F2"), Traslados = listaTrasladosGlobales }
                    : (object)new { Traslados = listaTrasladosGlobales };
            }

            // El SAT exige el nodo InformacionGlobal (CFDI40130) siempre que el receptor
            // sea el RFC genérico de público en general -- ver comentario de clase.
            bool esPublicoGeneral = string.IsNullOrWhiteSpace(datos.RfcReceptor)
                || datos.RfcReceptor.Trim().ToUpper() == "XAXX010101000";

            object? nodoInformacionGlobal = esPublicoGeneral
                ? new { Periodicidad = "01", Meses = DateTime.Now.ToString("MM"), Año = DateTime.Now.Year.ToString() }
                : null;

            var requestBody = new
            {
                Version = "4.0",
                Serie = logSerie,
                Folio = logFolio,
                Fecha = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
                FormaPago = string.IsNullOrWhiteSpace(datos.FormaPago) ? "01" : datos.FormaPago,
                MetodoPago = string.IsNullOrWhiteSpace(datos.MetodoPago) ? "PUE" : datos.MetodoPago,
                TipoDeComprobante = "I",
                Moneda = "MXN",
                LugarExpedicion = _config["SWServices:LugarExpedicion"] ?? "00000",
                Exportacion = "01",
                SubTotal = subTotalFactura.ToString("F2"),
                Descuento = descuentoTotalFact > 0 ? descuentoTotalFact.ToString("F2") : null,
                Total = (subTotalFactura - descuentoTotalFact + totalTraslados).ToString("F2"),
                Emisor = new
                {
                    Rfc = _config["SWServices:RfcEmisor"] ?? "",
                    Nombre = _config["SWServices:NombreEmisor"] ?? "",
                    RegimenFiscal = _config["SWServices:RegimenEmisor"] ?? ""
                },
                Receptor = new
                {
                    Rfc = esPublicoGeneral ? "XAXX010101000" : datos.RfcReceptor.Trim().ToUpper(),
                    Nombre = esPublicoGeneral ? "PUBLICO EN GENERAL" : LimpiarNombreSAT(datos.NombreReceptor),
                    DomicilioFiscalReceptor = (esPublicoGeneral || string.IsNullOrEmpty(datos.CodigoPostalReceptor))
                        ? _config["SWServices:LugarExpedicion"]
                        : datos.CodigoPostalReceptor,
                    RegimenFiscalReceptor = esPublicoGeneral ? "616" : (string.IsNullOrWhiteSpace(datos.RegimenFiscalReceptor) ? "616" : datos.RegimenFiscalReceptor),
                    UsoCFDI = esPublicoGeneral ? "S01" : (string.IsNullOrWhiteSpace(datos.UsoCfdi) ? "S01" : datos.UsoCfdi)
                },
                Conceptos = conceptosCalculados.Select(c =>
                {
                    var trasladosConcepto = GenerarTrasladosConcepto(c);
                    decimal cantidad = c.Origen.Cantidad > 0 ? c.Origen.Cantidad : 1m;
                    decimal valorUnitarioCalculado = cantidad > 0
                        ? Math.Round(c.ImporteBruto / cantidad, 6, MidpointRounding.AwayFromZero)
                        : 0m;

                    return new
                    {
                        ClaveProdServ = c.Origen.ClaveProdServ,
                        NoIdentificacion = c.Origen.NoIdentificacion,
                        Cantidad = cantidad.ToString("F2"),
                        ClaveUnidad = c.Origen.ClaveUnidad,
                        Descripcion = string.IsNullOrWhiteSpace(c.Origen.Descripcion) ? "PRODUCTO" : c.Origen.Descripcion,
                        ValorUnitario = valorUnitarioCalculado.ToString("F6"),
                        Importe = c.ImporteBruto.ToString("F2"),
                        Descuento = c.DescuentoBase > 0 ? c.DescuentoBase.ToString("F2") : null,
                        ObjetoImp = c.ObjetoImp,
                        Impuestos = (c.ObjetoImp == "02" && trasladosConcepto.Count > 0) ? new { Traslados = trasladosConcepto } : null
                    };
                }).ToList(),
                Impuestos = nodoImpuestosGlobal,
                InformacionGlobal = nodoInformacionGlobal
            };

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", token);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var serializeOptions = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
            };
            string jsonPayload = JsonSerializer.Serialize(requestBody, serializeOptions);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/jsontoxml");

            var ambiente = _config["SWServices:Ambiente"];
            bool esDev = string.Equals(ambiente, "Dev", StringComparison.OrdinalIgnoreCase);
            string urlApiBase = (esDev ? _config["SWServices:UrlApiDev"] : _config["SWServices:UrlApi"])!.TrimEnd('/');
            var url = $"{urlApiBase}/v3/cfdi33/issue/json/v4";

            _logger.LogInformation("Timbrando {Serie}-{Folio} (Ambiente: {Ambiente})", logSerie, logFolio, ambiente);

            var response = await _httpClient.PostAsync(url, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(responseBody);
                var data = doc.RootElement.GetProperty("data");
                return new RespuestaTimbradoDto
                {
                    Exito = true,
                    Uuid = data.GetProperty("uuid").GetString(),
                    CadenaOriginalSAT = data.GetProperty("cadenaOriginalSAT").GetString(),
                    XmlTimbrado = data.GetProperty("cfdi").GetString()
                };
            }

            _logger.LogWarning("SW Sapien rechazó el timbrado de {Serie}-{Folio}: {Status} — {Body}", logSerie, logFolio, response.StatusCode, responseBody);
            return new RespuestaTimbradoDto { Exito = false, MensajeError = $"SW Error: {response.StatusCode} - {responseBody}" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excepción al timbrar {Serie}-{Folio}", logSerie, logFolio);
            return new RespuestaTimbradoDto { Exito = false, MensajeError = $"Error interno al timbrar: {ex.Message}" };
        }
    }

    public async Task<byte[]> GenerarPdfAsync(string xmlContent)
    {
        string token = await ObtenerTokenAsync();
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", token);

        var requestBody = new
        {
            xmlContent,
            extras = new { OBSERVACIONES = "Gracias por su preferencia." },
            templateId = "cfdi40"
        };

        // Sin ambiente Dev/Prod: es el único endpoint de PDF que expone SW Sapien (mismo
        // comportamiento que tenían las dos copias originales, no es un descuido).
        var response = await _httpClient.PostAsJsonAsync("https://api.sw.com.mx/pdf/v1/api/GeneratePdf", requestBody);
        var jsonResponse = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"Error al generar el PDF del PAC: {jsonResponse}");

        using var doc = JsonDocument.Parse(jsonResponse);
        var base64Pdf = doc.RootElement.GetProperty("data").GetProperty("contentB64").GetString();
        return Convert.FromBase64String(base64Pdf!);
    }

    private async Task<string> ObtenerTokenAsync()
    {
        var ambiente = _config["SWServices:Ambiente"];
        bool esDev = string.Equals(ambiente, "Dev", StringComparison.OrdinalIgnoreCase);

        string urlBase = (esDev ? _config["SWServices:UrlApiDev"] : _config["SWServices:UrlApi"])!.Trim().TrimEnd('/');
        string? passwordEnv = esDev ? _config["SWServices:PasswordDev"] : _config["SWServices:Password"];
        string? usuarioFinal = esDev
            ? (_config["SWServices:UserNameDev"] ?? _config["SWServices:UserName"])
            : _config["SWServices:UserName"];

        var url = $"{urlBase}/v2/security/authenticate";
        var authRequest = new { user = usuarioFinal?.Trim(), password = passwordEnv?.Trim() };

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _httpClient.PostAsJsonAsync(url, authRequest);
        var resContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"Autenticación fallida con PAC SW (Ambiente: {(esDev ? "Dev" : "Prd")}): {response.StatusCode} - {resContent}");

        using var doc = JsonDocument.Parse(resContent);
        return doc.RootElement.GetProperty("data").GetProperty("token").GetString()!;
    }

    private static ConceptoCalculado CalcularConcepto(ConceptoTimbradoDto d)
    {
        bool esExento = d.TipoFactor == "Exento";
        decimal tIVA = d.ImpuestoClave == "002" ? d.TasaImpuesto / 100m : 0m;
        decimal tIEPS = d.ImpuestoClave == "003" ? d.TasaImpuesto / 100m : 0m;

        decimal divisor = esExento ? 1m : (1 + tIVA + tIEPS == 0 ? 1m : 1 + tIVA + tIEPS);

        decimal importeBrutoConImpuestos = d.PrecioUnitario * d.Cantidad;
        decimal importeBruto = Math.Round(importeBrutoConImpuestos / divisor, 2, MidpointRounding.AwayFromZero);

        decimal descuentoBase = d.Descuento > 0
            ? Math.Round(d.Descuento / divisor, 2, MidpointRounding.AwayFromZero)
            : 0m;

        decimal baseC = importeBruto - descuentoBase;

        decimal impIVA = esExento ? 0 : Math.Round(baseC * tIVA, 2, MidpointRounding.AwayFromZero);
        decimal impIEPS = esExento ? 0 : Math.Round(baseC * tIEPS, 2, MidpointRounding.AwayFromZero);

        string objetoImp = (esExento || d.TipoFactor == "Tasa" || tIEPS > 0) ? "02" : "01";

        return new ConceptoCalculado
        {
            Origen = d,
            Base = baseC,
            ImporteBruto = importeBruto,
            DescuentoBase = descuentoBase,
            TasaIVA = tIVA,
            ImporteIVA = impIVA,
            TasaIEPS = tIEPS,
            ImporteIEPS = impIEPS,
            ImpuestoClave = d.ImpuestoClave,
            ObjetoImp = objetoImp,
            TipoFactor = d.TipoFactor
        };
    }

    private static List<object> GenerarTrasladosConcepto(ConceptoCalculado c)
    {
        var traslados = new List<object>();

        if (c.TipoFactor == "Exento")
        {
            traslados.Add(new { Base = c.Base.ToString("F2"), Impuesto = c.ImpuestoClave, TipoFactor = "Exento" });
            return traslados;
        }

        traslados.Add(new
        {
            Base = c.Base.ToString("F2"),
            Impuesto = "002",
            TipoFactor = "Tasa",
            TasaOCuota = c.TasaIVA.ToString("F6"),
            Importe = c.ImporteIVA.ToString("F2")
        });

        if (c.TasaIEPS > 0)
        {
            traslados.Add(new
            {
                Base = c.Base.ToString("F2"),
                Impuesto = "003",
                TipoFactor = "Tasa",
                TasaOCuota = c.TasaIEPS.ToString("F6"),
                Importe = c.ImporteIEPS.ToString("F2")
            });
        }

        return traslados;
    }

    private static List<object> GenerarTrasladosGlobales(List<ConceptoCalculado> conceptos)
    {
        var conceptosFiltrados = conceptos.Where(c => c.ObjetoImp == "02").ToList();

        var listaTasa = new List<(string Impuesto, decimal Tasa, decimal Base, decimal Importe)>();
        var listaExento = new List<(string Impuesto, decimal Base)>();

        foreach (var c in conceptosFiltrados)
        {
            if (c.TipoFactor == "Exento")
            {
                listaExento.Add((c.ImpuestoClave, c.Base));
                continue;
            }

            listaTasa.Add(("002", c.TasaIVA, c.Base, c.ImporteIVA));
            if (c.TasaIEPS > 0)
                listaTasa.Add(("003", c.TasaIEPS, c.Base, c.ImporteIEPS));
        }

        var resultado = new List<object>();

        resultado.AddRange(
            listaTasa
                .GroupBy(t => (t.Impuesto, t.Tasa))
                .Select(g => (object)new
                {
                    Base = g.Sum(x => x.Base).ToString("F2"),
                    Impuesto = g.Key.Impuesto,
                    TipoFactor = "Tasa",
                    TasaOCuota = g.Key.Tasa.ToString("F6"),
                    Importe = g.Sum(x => x.Importe).ToString("F2")
                }));

        resultado.AddRange(
            listaExento
                .GroupBy(e => e.Impuesto)
                .Select(g => (object)new
                {
                    Base = g.Sum(x => x.Base).ToString("F2"),
                    Impuesto = g.Key,
                    TipoFactor = "Exento"
                }));

        return resultado;
    }

    private static string LimpiarNombreSAT(string? nombre)
    {
        if (string.IsNullOrEmpty(nombre)) return "PUBLICO EN GENERAL";
        string limpio = nombre.ToUpper().Trim();
        string[] sufijos =
        {
            " S.A. DE C.V.", " SA DE CV", " S.A.B. DE C.V.", " SAB DE CV",
            " S. DE R.L. DE C.V.", " S DE RL DE CV", " S.C.", " AC", " A.C."
        };
        foreach (var sufijo in sufijos)
        {
            if (limpio.EndsWith(sufijo))
                limpio = limpio[..^sufijo.Length].Trim();
        }
        return limpio;
    }

    private class ConceptoCalculado
    {
        public required ConceptoTimbradoDto Origen { get; set; }
        public decimal Base { get; set; }
        public decimal ImporteBruto { get; set; }
        public decimal DescuentoBase { get; set; }
        public decimal TasaIVA { get; set; }
        public decimal ImporteIVA { get; set; }
        public decimal TasaIEPS { get; set; }
        public decimal ImporteIEPS { get; set; }
        public required string ImpuestoClave { get; set; }
        public required string ObjetoImp { get; set; }
        public string? TipoFactor { get; set; }
    }
}
