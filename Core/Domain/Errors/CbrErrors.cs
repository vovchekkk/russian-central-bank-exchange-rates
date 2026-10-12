using ErrorOr;

namespace Core.Domain.Errors;

internal static class CbrErrors
{
    private const int TransientNetworkErrorType = 100;

    public static Error ServerHttpError(int statusCode) => Error.Custom(
        type: TransientNetworkErrorType,
        code: "Cbr.ServerHttpError",
        description: $"Сервер ЦБ РФ вернул ошибку (HTTP {statusCode}). Попробуйте позже."
    );

    public static Error Timeout => Error.Custom(
        type: TransientNetworkErrorType,
        code: "Cbr.Timeout",
        description: "Превышено время ожидания ответа от сервера ЦБ РФ."
    );

    public static Error NetworkFailure => Error.Custom(
        type: TransientNetworkErrorType,
        code: "Cbr.NetworkFailure",
        description: "Не удалось получить данные от сервера ЦБ РФ. Проверьте интернет."
    );

    public static bool IsTransientNetworkError(this Error error)
        => error.NumericType == TransientNetworkErrorType;
}
