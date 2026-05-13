using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    // Event argumenti
    public class TransferStartedEventArgs : EventArgs
    {
        public string CountryCode { get; set; }
        public DateTime Date { get; set; }
        public int TotalSamples { get; set; }
    }

    public class SampleReceivedEventArgs : EventArgs
    {
        public HourlyConsumptionSample Sample { get; set; }
        public int ReceivedCount { get; set; }
        public int TotalSamples { get; set; }
        public double PercentDone { get; set; }
    }

    public class TransferCompletedEventArgs : EventArgs
    {
        public int TotalReceived { get; set; }
        public int TotalSamples { get; set; }
    }

    public class WarningRaisedEventArgs : EventArgs
    {
        public string WarningType { get; set; }   // "UnderConsumption", "OverConsumption", "Spike", "DailyLimit"
        public string Message { get; set; }
        public HourlyConsumptionSample Sample { get; set; }
    }
}