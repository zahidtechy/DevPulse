namespace DevPulse.Domain.DTOs;

public class DeveloperDTO
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string? Email { get; set; }
}
