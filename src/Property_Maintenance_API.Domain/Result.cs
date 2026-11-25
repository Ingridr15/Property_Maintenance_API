namespace Property_Maintenance_API.Domain;

public enum ErrorKind
{
    Validation,  // 400: los datos enviados estan mal
    NotFound,    // 404: lo que se busca no existe
    Conflict,    // 409: los datos están bien, pero chocan con el estado actual
}

public sealed record Error(
    ErrorKind Kind,
    string Code,
    string Message,
    IReadOnlyDictionary<string, string[]>? Fields = null)
{
    public static Error Validation(string code, string message, string? field = null) =>
        new(ErrorKind.Validation, code, message,
            field is null ? null : new Dictionary<string, string[]> { [field] = [message] });

    public static Error NotFound(string code, string message) => new(ErrorKind.NotFound, code, message);

    public static Error Conflict(string code, string message) => new(ErrorKind.Conflict, code, message);
}

// Resultado de una operacion, o trae un valor, o trae un Error.
public sealed class Result<T>
{
    private readonly T? _value;

    private Result(T value) { _value = value; IsSuccess = true; }
    private Result(Error error) { Error = error; IsSuccess = false; }

    public bool IsSuccess { get; }
    public Error? Error { get; }
    public T Value => IsSuccess ? _value! : throw new InvalidOperationException("El resultado es un error.");

    public static implicit operator Result<T>(T value) => new(value);
    public static implicit operator Result<T>(Error error) => new(error);
}