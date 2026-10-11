using System.ComponentModel.DataAnnotations;
using ErrorOr;
using MimeKit;

namespace Core.Domain.Models;

public sealed record EmailAddress
{
    private static readonly EmailAddressAttribute Validator = new();

    internal MailboxAddress Mailbox { get; }
    public string Value => Mailbox.Address;

    private EmailAddress(MailboxAddress mailbox)
    {
        Mailbox = mailbox;
    }

    public static ErrorOr<EmailAddress> Create(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return Error.Validation(
                description: "Не указан email-адрес получателя.");

        var trimmed = email.Trim();

        if (!Validator.IsValid(trimmed) ||
            !MailboxAddress.TryParse(trimmed, out var mailbox) ||
            !string.Equals(mailbox.Address, trimmed, StringComparison.OrdinalIgnoreCase))
            return Error.Validation(
                description: "Указан некорректный формат email-адреса получателя.");

        return new EmailAddress(mailbox);
    }

    public bool Equals(EmailAddress? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is null) return false;
        return string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);
    }

    public override int GetHashCode() =>
        StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

    public override string ToString() => Value;
}