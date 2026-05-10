using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;
using Common;

namespace Service
{
    public class ConsumptionService : IConsumptionService
    {
        public void StartSession(SessionMeta meta)
        {
            Console.WriteLine($"[StartSession] Zemlja={meta.CountryCode}, Datum={meta.Date:yyyy-MM-dd}, Fajl={meta.SourceFileName}, UkupnoUzoraka={meta.TotalSamples}");
        }

        public void PushSample(HourlyConsumptionSample sample)
        {
            // sample ne postoji
            if (sample == null)
            {
                DataFormatFault fault = new DataFormatFault
                {
                    Message = "Primljen prazan (null) podatak."
                };
                throw new FaultException<DataFormatFault>(fault, new FaultReason("Format nije ispravan"));
            }

            // CountryCode mora biti popunjen
            if (string.IsNullOrWhiteSpace(sample.CountryCode))
            {
                DataFormatFault fault = new DataFormatFault
                {
                    Message = "CountryCode je prazan."
                };
                throw new FaultException<DataFormatFault>(fault, new FaultReason("Format nije ispravan"));
            }

            // Timestamp mora biti smislen (ne sme biti default DateTime - 1.1.0001)
            if (sample.TimestampUtc == default(DateTime))
            {
                DataFormatFault fault = new DataFormatFault
                {
                    Message = "TimestampUtc nije postavljen."
                };
                throw new FaultException<DataFormatFault>(fault, new FaultReason("Format nije ispravan"));
            }
            // Validacija: Hour u opsegu 0-23
            if (sample.Hour < 0 || sample.Hour > 23)
            {
                ValidationFault fault = new ValidationFault
                {
                    Message = $"Hour van opsega [0,23]. Primljeno: {sample.Hour}"
                };
                throw new FaultException<ValidationFault>(fault, new FaultReason("Validacija nije prosla"));
            }

            // Validacija: ActualMW >= 0
            if (sample.ActualMW < 0)
            {
                ValidationFault fault = new ValidationFault
                {
                    Message = $"ActualMW mora biti >= 0. Primljeno: {sample.ActualMW}"
                };
                throw new FaultException<ValidationFault>(fault, new FaultReason("Validacija nije prosla"));
            }

            // Validacija: ForecastMW >= 0
            if (sample.ForecastMW < 0)
            {
                ValidationFault fault = new ValidationFault
                {
                    Message = $"ForecastMW mora biti >= 0. Primljeno: {sample.ForecastMW}"
                };
                throw new FaultException<ValidationFault>(fault, new FaultReason("Validacija nije prosla"));
            }

            Console.WriteLine($"[PushSample] Sat={sample.Hour}, Actual={sample.ActualMW} MW, Forecast={sample.ForecastMW} MW");
        }

        public void EndSession()
        {
            Console.WriteLine("[EndSession] Sesija zavrsena.");
        }
    }
}
