using System.Threading.Tasks;
using HRMS.Domain.DTOs;

namespace HRMS.UI.Services
{
    public interface IBiometricLogService
    {
        Task<BiometricLogResponseDto> CreateLogAsync(BiometricLogDto createDto);
    }
}
