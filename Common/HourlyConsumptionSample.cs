using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;

namespace Common
{
    [DataContract]
    public class HourlyConsumptionSample
    {
        [DataMember]
        public DateTime TimestampUtc { get; set; }

        [DataMember]
        public DateTime TimestampLocal { get; set; }

        [DataMember]
        public int Hour { get; set; }

        [DataMember]
        public double ActualMW { get; set; }

        [DataMember]
        public double ForecastMW { get; set; }

        [DataMember]
        public string CountryCode { get; set; }

        [DataMember]
        public int RowIndex { get; set; }
    }
}
