namespace Olbrasoft.PushToTalk.App.Configuration;

/// <summary>
/// Configuration options for the web server (Kestrel).
/// </summary>
public class WebServerOptions
{
    /// <summary>
    /// HTTP port for the web server.
    /// </summary>
    public int Port { get; set; } = 5050;

    /// <summary>
    /// HTTPS configuration (optional).
    /// </summary>
    public HttpsOptions? Https { get; set; }
}

/// <summary>
/// HTTPS configuration for the web server.
/// </summary>
public class HttpsOptions
{
    /// <summary>
    /// HTTPS port for the web server.
    /// </summary>
    public int Port { get; set; } = 5051;

    /// <summary>
    /// Path to the SSL certificate (relative to app directory or absolute).
    /// </summary>
    public string CertificatePath { get; set; } = "certs/localhost.p12";

    /// <summary>
    /// Password for the SSL certificate.
    /// </summary>
    public string CertificatePassword { get; set; } = "changeit";
}
