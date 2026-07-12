using System.Threading.Tasks;
using HRMS.Domain.Entities.Attendance;

namespace HRMS.UI.Services
{
    public interface IAttendanceService
    {
        Task ProcessAttendanceAsync(BiometricLog biometricLog);
    }
}

