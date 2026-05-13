using System;
using System.Configuration;
using System.IO;
using System.ServiceModel;
using System.Text;
using Common;

namespace Service
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class ConsumptionService : IConsumptionService
    {
        // Eventi
        public event EventHandler<TransferStartedEventArgs> OnTransferStarted;
        public event EventHandler<SampleReceivedEventArgs> OnSampleReceived;
        public event EventHandler<TransferCompletedEventArgs> OnTransferCompleted;
        public event EventHandler<WarningRaisedEventArgs> OnWarningRaised;

        private StreamWriter _sessionWriter;
        private StreamWriter _rejectsWriter;
        private SessionMeta _meta;
        private int _receivedCount = 0;
        private double _dailyTotalMW = 0;

        // Pragovi iz app.config
        private readonly double _underConsumptionAlpha;
        private readonly double _overConsumptionBeta;
        private readonly double _spikeDeltaMW;
        private readonly double _dailyLimitMW;

        private double _previousActualMW = double.NaN;

        public ConsumptionService()
        {
            _underConsumptionAlpha = double.Parse(ConfigurationManager.AppSettings["UnderConsumptionAlpha"] ?? "0.5");
            _overConsumptionBeta = double.Parse(ConfigurationManager.AppSettings["OverConsumptionBeta"] ?? "1.5");
            _spikeDeltaMW = double.Parse(ConfigurationManager.AppSettings["SpikeDeltaMW"] ?? "5000");
            _dailyLimitMW = double.Parse(ConfigurationManager.AppSettings["DailyLimitMW"] ?? "1000000");

            Console.WriteLine($"[Config] UnderConsumptionAlpha={_underConsumptionAlpha}, OverConsumptionBeta={_overConsumptionBeta}, SpikeDeltaMW={_spikeDeltaMW}, DailyLimitMW={_dailyLimitMW}");
        }

        public void StartSession(SessionMeta meta)
        {
            _meta = meta;
            _receivedCount = 0;
            _dailyTotalMW = 0;
            _previousActualMW = double.NaN;

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

            // Okidamo event
            OnTransferStarted?.Invoke(this, new TransferStartedEventArgs
            {
                CountryCode = meta.CountryCode,
                Date = meta.Date,
                TotalSamples = meta.TotalSamples
            });
        }

        public void PushSample(HourlyConsumptionSample sample)
        {
            if (_sessionWriter == null)
            {
                var fault = new DataFormatFault { Message = "Sesija nije pokrenuta. Prvo pozovi StartSession." };
                throw new FaultException<DataFormatFault>(fault, new FaultReason("Format nije ispravan"));
            }

            if (sample == null)
            {
                WriteReject(-1, "Primljen prazan (null) podatak.", "(null)");
                var fault = new DataFormatFault { Message = "Primljen prazan (null) podatak." };
                throw new FaultException<DataFormatFault>(fault, new FaultReason("Format nije ispravan"));
            }

            string originalLine = FormatSampleLine(sample);

            if (string.IsNullOrWhiteSpace(sample.CountryCode))
            {
                WriteReject(sample.RowIndex, "CountryCode je prazan.", originalLine);
                var fault = new DataFormatFault { Message = "CountryCode je prazan." };
                throw new FaultException<DataFormatFault>(fault, new FaultReason("Format nije ispravan"));
            }

            if (sample.TimestampUtc == default(DateTime))
            {
                WriteReject(sample.RowIndex, "TimestampUtc nije postavljen.", originalLine);
                var fault = new DataFormatFault { Message = "TimestampUtc nije postavljen." };
                throw new FaultException<DataFormatFault>(fault, new FaultReason("Format nije ispravan"));
            }

            if (sample.Hour < 0 || sample.Hour > 23)
            {
                string reason = $"Hour van opsega [0,23]. Primljeno: {sample.Hour}";
                WriteReject(sample.RowIndex, reason, originalLine);
                var fault = new ValidationFault { Message = reason };
                throw new FaultException<ValidationFault>(fault, new FaultReason("Validacija nije prosla"));
            }

            if (sample.ActualMW < 0)
            {
                string reason = $"ActualMW mora biti >= 0. Primljeno: {sample.ActualMW}";
                WriteReject(sample.RowIndex, reason, originalLine);
                var fault = new ValidationFault { Message = reason };
                throw new FaultException<ValidationFault>(fault, new FaultReason("Validacija nije prosla"));
            }

            if (sample.ForecastMW < 0)
            {
                string reason = $"ForecastMW mora biti >= 0. Primljeno: {sample.ForecastMW}";
                WriteReject(sample.RowIndex, reason, originalLine);
                var fault = new ValidationFault { Message = reason };
                throw new FaultException<ValidationFault>(fault, new FaultReason("Validacija nije prosla"));
            }

            _sessionWriter.WriteLine(originalLine);
            _receivedCount++;
            _dailyTotalMW += sample.ActualMW;

            double procenat = (_meta != null && _meta.TotalSamples > 0)
                ? (double)_receivedCount / _meta.TotalSamples * 100.0
                : 0.0;

            Console.WriteLine($"[PRENOS U TOKU] Primljeno: {_receivedCount}/{_meta?.TotalSamples ?? 0} ({procenat:F1}%) | Sat={sample.Hour}, Actual={sample.ActualMW} MW");

            // Okidamo OnSampleReceived
            OnSampleReceived?.Invoke(this, new SampleReceivedEventArgs
            {
                Sample = sample,
                ReceivedCount = _receivedCount,
                TotalSamples = _meta?.TotalSamples ?? 0,
                PercentDone = procenat
            });

            // Provjera upozorenja
            CheckWarnings(sample);

            _previousActualMW = sample.ActualMW;
        }

        public void EndSession()
        {
            if (_sessionWriter != null) { _sessionWriter.Flush(); _sessionWriter.Dispose(); _sessionWriter = null; }
            if (_rejectsWriter != null) { _rejectsWriter.Flush(); _rejectsWriter.Dispose(); _rejectsWriter = null; }

            int total = _meta?.TotalSamples ?? 0;
            double finProcenat = (total > 0) ? (double)_receivedCount / total * 100.0 : 0.0;
            Console.WriteLine($"[PRENOS ZAVRSEN] Ukupno primljeno: {_receivedCount}/{total} ({finProcenat:F1}%)");

            // Okidamo OnTransferCompleted
            OnTransferCompleted?.Invoke(this, new TransferCompletedEventArgs
            {
                TotalReceived = _receivedCount,
                TotalSamples = total
            });

            Console.WriteLine("[EndSession] Sesija zavrsena. Fajlovi sacuvani.");
        }

        private void CheckWarnings(HourlyConsumptionSample sample)
        {
            // 1. UnderConsumption: Actual < Alpha * Forecast
            if (sample.ForecastMW > 0 && sample.ActualMW < _underConsumptionAlpha * sample.ForecastMW)
            {
                string msg = $"[UPOZORENJE] UnderConsumption: Sat={sample.Hour}, Actual={sample.ActualMW} MW < {_underConsumptionAlpha} * Forecast={sample.ForecastMW} MW";
                Console.WriteLine(msg);
                OnWarningRaised?.Invoke(this, new WarningRaisedEventArgs
                {
                    WarningType = "UnderConsumption",
                    Message = msg,
                    Sample = sample
                });
            }

            // 2. OverConsumption: Actual > Beta * Forecast
            if (sample.ForecastMW > 0 && sample.ActualMW > _overConsumptionBeta * sample.ForecastMW)
            {
                string msg = $"[UPOZORENJE] OverConsumption: Sat={sample.Hour}, Actual={sample.ActualMW} MW > {_overConsumptionBeta} * Forecast={sample.ForecastMW} MW";
                Console.WriteLine(msg);
                OnWarningRaised?.Invoke(this, new WarningRaisedEventArgs
                {
                    WarningType = "OverConsumption",
                    Message = msg,
                    Sample = sample
                });
            }

            // 3. Spike: razlika od prethodnog sata > SpikeDeltaMW
            if (!double.IsNaN(_previousActualMW))
            {
                double delta = Math.Abs(sample.ActualMW - _previousActualMW);
                if (delta > _spikeDeltaMW)
                {
                    string msg = $"[UPOZORENJE] Spike: Sat={sample.Hour}, delta={delta:F1} MW > {_spikeDeltaMW} MW (prethodni={_previousActualMW}, trenutni={sample.ActualMW})";
                    Console.WriteLine(msg);
                    OnWarningRaised?.Invoke(this, new WarningRaisedEventArgs
                    {
                        WarningType = "Spike",
                        Message = msg,
                        Sample = sample
                    });
                }
            }

            // 4. DailyLimit: kumulativna suma za dan > DailyLimitMW
            if (_dailyTotalMW > _dailyLimitMW)
            {
                string msg = $"[UPOZORENJE] DailyLimit: Kumulativno={_dailyTotalMW:F1} MW > limit={_dailyLimitMW} MW";
                Console.WriteLine(msg);
                OnWarningRaised?.Invoke(this, new WarningRaisedEventArgs
                {
                    WarningType = "DailyLimit",
                    Message = msg,
                    Sample = sample
                });
            }
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