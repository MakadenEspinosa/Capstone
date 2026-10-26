namespace ESEvents.Application.Users;

public interface IUserService
{
    Task<UserDto> RegisterAsync(RegisterUserRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken ct = default);
    Task<UserDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<UserDto?> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task SuspendAsync(Guid id, CancellationToken ct = default);
    Task ReinstateAsync(Guid id, CancellationToken ct = default);
}
