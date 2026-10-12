using ErrorOr;

namespace Core.Domain.Models;

public sealed record Currency
{
    internal static readonly Currency Usd = new(840, "USD", "Доллар США");
    internal static readonly Currency Eur = new(978, "EUR", "Евро");

    public ushort NumCode { get; }
    public string CharCode { get; }
    public string Name { get; }

    private Currency(ushort numCode, string charCode, string name)
    {
        NumCode = numCode;
        CharCode = charCode;
        Name = name;
    }

    internal static ErrorOr<Currency> Create(ushort numCode, string? charCode, string? name)
    {
        if (string.IsNullOrWhiteSpace(charCode))
            return Error.Validation(
                description: $"Отсутствует символьный код (CharCode) у валюты с NumCode '{numCode}'.");

        var normalizedCharCode = charCode.Trim().ToUpperInvariant();

        if (normalizedCharCode.Length != 3)
            return Error.Validation(
                description: $"Символьный код валюты должен состоять из 3 символов (получено: '{normalizedCharCode}').");

        if (numCode == 0)
            return Error.Validation(
                description: $"Некорректный цифровой код (NumCode: 0) для валюты {normalizedCharCode}.");

        if (string.IsNullOrWhiteSpace(name))
            return Error.Validation(
                description: $"Отсутствует название (Name) для валюты {normalizedCharCode}.");

        return new Currency(numCode, normalizedCharCode, name.Trim());
    }

    public bool Equals(Currency? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is null) return false;
        return NumCode == other.NumCode && CharCode == other.CharCode;
    }

    public override int GetHashCode() =>
        HashCode.Combine(NumCode, CharCode);
}
