using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;

namespace Common
{
    [ServiceContract]
    public interface IConsumptionService
    {
        [OperationContract]
        void StartSession(SessionMeta meta);

        [OperationContract]
        void PushSample(HourlyConsumptionSample sample);

        [OperationContract]
        void EndSession();
    }
}