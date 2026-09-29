namespace Eggrack.Operations.Common.Models;

public sealed class BusinessRuleException(string message, string? code = null, Exception? innerException = null)
    : InvalidOperationException(message, innerException)
{
    public string? Code { get; } = code;
}