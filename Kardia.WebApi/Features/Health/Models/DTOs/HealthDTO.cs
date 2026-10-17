namespace Kardia.WebApi.Features.Health.Models.DTOs;

/// <summary>Whether the WebApi and its database answer (Docker's health check).</summary>
public sealed class HealthDTO
{
    /// <summary>Gets or sets "ok", or what failed.</summary>
    public string Status { get; set; } = "ok";

    /// <summary>Gets or sets a value indicating whether the database answered.</summary>
    public bool Database { get; set; }

    /// <summary>Gets or sets a value indicating whether Kardia runs on sample data.</summary>
    public bool DemoMode { get; set; }
}
