using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Interfaces
{
    //Interfaces/IAlarmRepository.cs
    public interface IAlarmRepository
    {
        Task AddAsync(PersistentAlarm alarm);
        Task BulkInsertAsync(IEnumerable<PersistentAlarm> alarms);
        Task ClearAllAsync();
        Task<IEnumerable<PersistentAlarm>> GetHistoryAsync(DateTime? start, DateTime? end);
    }

}
