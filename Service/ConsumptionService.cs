using System;
using System.IO;
using System.ServiceModel;
using System.Text;
using Common;

namespace Service
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class ConsumptionService : IConsumptionService
    {
        private StreamWriter _sessionWriter;
        private StreamWriter _rejectsWriter;
        private SessionMeta _meta;

        public void StartSession(SessionMeta meta)
        {
            _meta = meta;

            string dir = Path.Combine("Data", meta.CountryCode, meta.Date.ToString("yyyy-MM-dd"));
            Directory.CreateDirectory(dir);

            string sessionPath = Path.Combine(dir, "session.csv");
            bool sessionNew = !File.Exists(sessionPath) || new FileInfo(sessionPath).Length == 0;
            FileStream sessionFs = new FileStream(sessionPath, FileMode.Append, FileAccess.Write, FileShare.Read);
            _sessionWriter = new StreamWriter(sessionFs, Encoding.UTF8) { AutoFlush = true };
            if (sessionNew)
                _sessionWriter.WriteLine("TimestampUtc,TimestampLocal,Hour,ActualMW,ForecastMW,CountryCode,RowIndex");

            string rejectsPath = Path.Combine(dir, "rejects.csv");
            bool rejectsNew = !File.Exists(rejectsPath) || new FileInfo(rejectsPath).Length == 0;
            FileStream rejectsFs = new FileStream(rejectsPath, FileMode.Append, FileAccess.Write, FileShare.Read);
            _rejectsWriter = new StreamWriter(rejectsFs, Encoding.UTF8) { AutoFlush = true };
            if (rejectsNew)
                _rejectsWriter.WriteLine("RowIndex,Reason,OriginalLine");

            Console.WriteLine($"[StartSession] Zemlja={meta.CountryCode}, Datum={meta.Date:yyyy-MM-dd}, Fajl={meta.SourceFileName}, UkupnoUzoraka={meta.TotalSamples}");
            Console.WriteLine($"  -> {sessionPath}");
            Console.WriteLine($"  -> {rejectsPath}");
        }

        public void PushSample(HourlyConsumptionSample sample)
        {
            if (_sessionWriter == null)
            {
                DataFormatFault fault = new DataFormatFault { Message = "Sesija nije pokrenuta. Prvo pozovi StartSession." };
                throw new FaultException<DataFormatFault>(fault, new FaultReason("Format nije ispravan"));
            }

            // sample ne postoji
            if (sample == null)
            {
                WriteReject(-1, "Primljen prazan (null) podatak.", "(null)");
                DataFormatFault fault = new DataFormatFault { Message = "Primljen prazan (null) podatak." };
                throw new FaultException<DataFormatFault>(fault, new FaultReason("Format nije ispravan"));
            }

            string originalLine = FormatSampleLine(sample);

            // CountryCode mora biti popunjen
            if (string.IsNullOrWhiteSpace(sample.CountryCode))
            {
                WriteReject(sample.RowIndex, "CountryCode je prazan.", originalLine);
                DataFormatFault fault = new DataFormatFault { Message = "CountryCode je prazan." };
                throw new FaultException<DataFormatFault>(fault, new FaultReason("Format nije ispravan"));
            }

            // Timestamp mora biti smislen (ne sme biti default DateTime - 1.1.0001)
            if (sample.TimestampUtc == default(DateTime))
            {
                WriteReject(sample.RowIndex, "TimestampUtc nije postavljen.", originalLine);
                DataFormatFault fault = new DataFormatFault { Message = "TimestampUtc nije postavljen." };
                throw new FaultException<DataFormatFault>(fault, new FaultReason("Format nije ispravan"));
            }

            // Validacija: Hour u opsegu 0-23
            if (sample.Hour < 0 || sample.Hour > 23)
            {
                string reason = $"Hour van opsega [0,23]. Primljeno: {sample.Hour}";
                WriteReject(sample.RowIndex, reason, originalLine);
                ValidationFault fault = new ValidationFault { Message = reason };
                throw new FaultException<ValidationFault>(fault, new FaultReason("Validacija nije prosla"));
            }

            // Validacija: ActualMW >= 0
            if (sample.ActualMW < 0)
            {
                string reason = $"ActualMW mora biti >= 0. Primljeno: {sample.ActualMW}";
                WriteReject(sample.RowIndex, reason, originalLine);
                ValidationFault fault = new ValidationFault { Message = reason };
                throw new FaultException<ValidationFault>(fault, new FaultReason("Validacija nije prosla"));
            }

            // Validacija: ForecastMW >= 0
            if (sample.ForecastMW < 0)
            {
                string reason = $"ForecastMW mora biti >= 0. Primljeno: {sample.ForecastMW}";
                WriteReject(sample.RowIndex, reason, originalLine);
                ValidationFault fault = new ValidationFault { Message = reason };
                throw new FaultException<ValidationFault>(fault, new FaultReason("Validacija nije prosla"));
            }

            // Validan red - upisujemo u session.csv
            _sessionWriter.WriteLine(originalLine);
            Console.WriteLine($"[PushSample] Sat={sample.Hour}, Actual={sample.ActualMW} MW, Forecast={sample.ForecastMW} MW");
        }

        public void EndSession()
        {
            if (_sessionWriter != null) { _sessionWriter.Flush(); _sessionWriter.Dispose(); _sessionWriter = null; }
            if (_rejectsWriter != null) { _rejectsWriter.Flush(); _rejectsWriter.Dispose(); _rejectsWriter = null; }
            Console.WriteLine("[EndSession] Sesija zavrsena. Fajlovi sacuvani.");
        }

        private void WriteReject(int rowIndex, string reason, string originalLine)
        {
            if (_rejectsWriter == null) return;
            string safeReason = reason.Replace("\"", "\"\"");
            string safeLine = originalLine.Replace("\"", "\"\"");
            _rejectsWriter.WriteLine($"{rowIndex},\"{safeReason}\",\"{safeLine}\"");
        }

        private static string FormatSampleLine(HourlyConsumptionSample s)
        {
            return $"{s.TimestampUtc:yyyy-MM-ddTHH:mm:ssZ},{s.TimestampLocal:yyyy-MM-ddTHH:mm:ss},{s.Hour},{s.ActualMW},{s.ForecastMW},{s.CountryCode},{s.RowIndex}";
        }
    }
}
