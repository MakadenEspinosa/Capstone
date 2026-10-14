namespace ESEvents.Application.Users;

public interface IUserService
{
    Task<UserDto> RegisterAsync(RegisterUserRequest request, CancellationToken ct = default);
    Task<UserDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task SuspendAsync(Guid id, CancellationToken ct = default);
    Task ReinstateAsync(Guid id, CancellationToken ct = default);
    Task RemoveAsync(Guid id, CancellationToken ct = default);
}
