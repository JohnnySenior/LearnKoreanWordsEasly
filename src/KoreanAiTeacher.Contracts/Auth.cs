namespace KoreanAiTeacher.Contracts;

public sealed record MeResponse(long TelegramUserId, string? FirstName, string? Username);
