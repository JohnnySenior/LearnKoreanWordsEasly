namespace Lugat.Application;

/// <summary>Expected failures with an Uzbek message that is safe to show (spec API03). The API maps them to ProblemDetails.</summary>
public abstract class AppException(string userMessage) : Exception(userMessage)
{
    public string UserMessage { get; } = userMessage;
}

/// <summary>Missing, or owned by someone else. Both look the same to the caller (spec SEC02, AC01).</summary>
public sealed class NotFoundException(string userMessage = "Topilmadi.") : AppException(userMessage);

public sealed class ConflictException(string userMessage) : AppException(userMessage);

public sealed class ValidationException(string userMessage) : AppException(userMessage);

public sealed class PayloadTooLargeException(string userMessage) : AppException(userMessage);

/// <summary>An outside service (AI, speech) is not available right now. The learner's input is kept (spec REL01).</summary>
public sealed class ServiceUnavailableException(string userMessage) : AppException(userMessage);

/// <summary>A usage limit was reached (spec BUD05).</summary>
public sealed class TooManyRequestsException(string userMessage) : AppException(userMessage);
