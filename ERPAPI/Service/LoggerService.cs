

using ERPAPI.Data;
using ERPAPI.Model;
using ERPAPI.Service;

using ERPGenericFunctions.Model;




namespace ERPAPI.Services
{
    public class LoggerService : ILoggerService
    {
        private readonly AppDbContext _appDbContext;

        public LoggerService(AppDbContext appDbContext)
        {
            _appDbContext = appDbContext;
        }



        public void LogEvent(string message, string category, int triggeredBy)
        {
            var log = new EventLog
            {
                Event = message,
                EventTriggeredBy = triggeredBy,
                Category = category,
            };
            _appDbContext.EventLogs.Add(log);
            _appDbContext.SaveChanges();
        }
        public void LogError(string error, string errormessage, string Controller)
        {
            var log = new ErrorLog
            {
                Error = error,
                Message = errormessage,
                OccuranceSpace = Controller,
            };

            _appDbContext.ErrorLogs.Add(log);
            _appDbContext.SaveChanges();
        }
    }
}

