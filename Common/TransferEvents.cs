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
        public string WarningType { get; set; }
        public string Message { get; set; }
        public HourlyConsumptionSample Sample { get; set; }

        // Eksplicitna polja koja zadatak traži u eventu k2 z4
        public int Hour { get; set; }
        public double ActualMW { get; set; }
        public double ForecastMW { get; set; }
        public string MeterID { get; set; }
    }
}