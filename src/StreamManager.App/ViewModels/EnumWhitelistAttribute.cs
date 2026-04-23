using System.ComponentModel.DataAnnotations;

namespace StreamManager.App.ViewModels;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class EnumWhitelistAttribute : ValidationAttribute
{
    public string[] AllowedValues { get; }

    public EnumWhitelistAttribute(params string[] values)
    {
        AllowedValues = values;
    }

    public override bool IsValid(object? value)
    {
        if (value is null) return true;
        if (value is not string s) return false;
        return Array.IndexOf(AllowedValues, s) >= 0;
    }

    public override string FormatErrorMessage(string name) =>
        $"{name} must be one of: {string.Join(", ", AllowedValues)}.";
}
