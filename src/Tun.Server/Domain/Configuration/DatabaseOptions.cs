using System.ComponentModel.DataAnnotations;

namespace Tun.Server.Domain.Configuration;

public class DatabaseOptions
{
    public bool Enabled { get; set; } = false;

    [RequiredIf(nameof(Enabled), true, ErrorMessage = "启用数据库时,ConnectionString 不能为空")]
    public string ConnectionString { get; set; } = "";
}

[AttributeUsage(AttributeTargets.Property)]
public class RequiredIfAttribute : ValidationAttribute
{
    private readonly string _dependentProperty;
    private readonly object _targetValue;

    public RequiredIfAttribute(string dependentProperty, object targetValue)
    {
        _dependentProperty = dependentProperty;
        _targetValue = targetValue;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var property = validationContext.ObjectType.GetProperty(_dependentProperty);
        if (property == null)
            return new ValidationResult($"未知属性: {_dependentProperty}");

        var dependentValue = property.GetValue(validationContext.ObjectInstance);
        if (Equals(dependentValue, _targetValue) && string.IsNullOrWhiteSpace(value?.ToString()))
            return new ValidationResult(ErrorMessage);

        return ValidationResult.Success;
    }
}
