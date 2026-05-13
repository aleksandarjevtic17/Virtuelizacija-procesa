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

        // NOVO: pratimo broj primljenih uzoraka
        private int _receivedCount = 0;

        public void StartSession(SessionMeta meta)
        {
            _meta = meta;
            _receivedCount = 0; // resetujemo brojac pri novoj sesiji

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

            if (sample == null)
            {
                WriteReject(-1, "Primljen prazan (null) podatak.", "(null)");
                DataFormatFault fault = new DataFormatFault { Message = "Primljen prazan (null) podatak." };
                throw new FaultException<DataFormatFault>(fault, new FaultReason("Format nije ispravan"));
            }

            string originalLine = FormatSampleLine(sample);

            if (string.IsNullOrWhiteSpace(sample.CountryCode))
            {
                WriteReject(sample.RowIndex, "CountryCode je prazan.", originalLine);
                DataFormatFault fault = new DataFormatFault { Message = "CountryCode je prazan." };
                throw new FaultException<DataFormatFault>(fault, new FaultReason("Format nije ispravan"));
            }

            if (sample.TimestampUtc == default(DateTime))
            {
                WriteReject(sample.RowIndex, "TimestampUtc nije postavljen.", originalLine);
                DataFormatFault fault = new DataFormatFault { Message = "TimestampUtc nije postavljen." };
                throw new FaultException<DataFormatFault>(fault, new FaultReason("Format nije ispravan"));
            }

            if (sample.Hour < 0 || sample.Hour > 23)
            {
                string reason = $"Hour van opsega [0,23]. Primljeno: {sample.Hour}";
                WriteReject(sample.RowIndex, reason, originalLine);
                ValidationFault fault = new ValidationFault { Message = reason };
                throw new FaultException<ValidationFault>(fault, new FaultReason("Validacija nije prosla"));
            }

            if (sample.ActualMW < 0)
            {
                string reason = $"ActualMW mora biti >= 0. Primljeno: {sample.ActualMW}";
                WriteReject(sample.RowIndex, reason, originalLine);
                ValidationFault fault = new ValidationFault { Message = reason };
                throw new FaultException<ValidationFault>(fault, new FaultReason("Validacija nije prosla"));
            }

            if (sample.ForecastMW < 0)
            {
                string reason = $"ForecastMW mora biti >= 0. Primljeno: {sample.ForecastMW}";
                WriteReject(sample.RowIndex, reason, originalLine);
                ValidationFault fault = new ValidationFault { Message = reason };
                throw new FaultException<ValidationFault>(fault, new FaultReason("Validacija nije prosla"));
            }

            // validan red - upisujem
            _sessionWriter.WriteLine(originalLine);
            // uvecavam brojac i ispisujemo status "prenos u toku"
            _receivedCount++;
            double procenat = (_meta != null && _meta.TotalSamples > 0) ? (double)_receivedCount / _meta.TotalSamples * 100.0 : 0.0;
            Console.WriteLine($"[PRENOS U TOKU] Primljeno: {_receivedCount}/{_meta?.TotalSamples ?? 0} uzoraka ({procenat:F1}%) | Sat={sample.Hour}, Actual={sample.ActualMW} MW");
        }

        public void EndSession()
        {
            if (_sessionWriter != null) { _sessionWriter.Flush(); _sessionWriter.Dispose(); _sessionWriter = null; }
            if (_rejectsWriter != null) { _rejectsWriter.Flush(); _rejectsWriter.Dispose(); _rejectsWriter = null; }

            //ispis "prenos završen" sa finalnim brojevima
            int total = _meta?.TotalSamples ?? 0;
            double finProcenat = (total > 0) ? (double)_receivedCount / total * 100.0 : 0.0;
            Console.WriteLine($"[PRENOS ZAVRSEN] Ukupno primljeno: {_receivedCount}/{total} uzoraka ({finProcenat:F1}%)");
            Console.WriteLine("[EndSession] Sesija zavrsena.");
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