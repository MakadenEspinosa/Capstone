namespace ESEvents.Domain.Common;

public sealed class DomainConflictException(string message) : DomainException(message);
