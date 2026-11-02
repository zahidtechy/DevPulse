namespace DevPulse.Client.Models;

public sealed record SearchableSelectOption(
    string Value,
    string Label,
    string? SupportingText = null);
