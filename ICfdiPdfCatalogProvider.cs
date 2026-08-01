namespace ApiARAConsultoria.Services.Pdf;

/// <summary>
/// Abstrae las fuentes de datos que CfdiPdfGeneratorService necesita para
/// enriquecer el PDF (nombres de catálogos SAT y logo default) pero que
/// varían según el proyecto host -- ApiARAConsultoria las resuelve contra
/// PapeleriaContext (SQL Server) + wwwroot/images, PapeleriaCentral no tiene
/// esos catálogos y regresa null (el PDF ya degrada bien a mostrar solo la
/// clave cuando el nombre no está disponible).
/// </summary>
public interface ICfdiPdfCatalogProvider
{
    Task<string?> GetRegimenFiscalNombreAsync(string clave);
    Task<string?> GetUsoCfdiNombreAsync(string clave);
    Task<string?> GetFormaPagoNombreAsync(string clave);
    Task<string?> GetMetodoPagoNombreAsync(string clave);
    byte[]? GetLogoDefault();
}
