using Infrastructure.Dto;

namespace Core.Services;

public interface ICbrClient
{
    public Task<ValCurs> GetValCursAsync(DateOnly date, CancellationToken cancellationToken = default);
}