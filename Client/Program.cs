using System;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.ServiceModel;
using Common;

namespace Client
{
    class Program
    {
        static void Main()
        {
            // Citanje konfiguracije
            string csvPath = ConfigurationManager.AppSettings["csvPath"];
            string countryCode = ConfigurationManager.AppSettings["countryCode"];
            string selectedDateStr = ConfigurationManager.AppSettings["selectedDate"];

            DateTime selectedDate;
            if (!DateTime.TryParseExact(selectedDateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out selectedDate))
            {
                Console.WriteLine($"[GRESKA] Datum '{selectedDateStr}' nije u formatu yyyy-MM-dd.");
                Console.ReadKey();
                return;
            }

            Console.WriteLine($"Konfiguracija: zemlja={countryCode}, datum={selectedDate:yyyy-MM-dd}, fajl={csvPath}");

            ChannelFactory<IConsumptionService> factory = null;
            IConsumptionService proxy = null;
            CsvReader csvReader = null;
            StreamWriter rejectedWriter = null;

            try
            {
                // Otvaranje CSV-a (citanje header-a, validacija zemlje)
                csvReader = new CsvReader(csvPath, countryCode);

                // Otvaranje rejected_client.csv za upis odbacenih redova
                rejectedWriter = new StreamWriter("rejected_client.csv", false);
                rejectedWriter.WriteLine("RowIndex,Razlog,OriginalniRed");

                // Prvo: prolazak kroz CSV da prebrojimo koliko redova pripada odabranom danu
                // (treba nam za TotalSamples u meta-i)
                int totalForDay = CountRowsForDay(csvPath, countryCode, selectedDate);
                Console.WriteLine($"Pronadjeno {totalForDay} redova za odabrani dan.");

                if (totalForDay == 0)
                {
                    Console.WriteLine("Nema redova za odabrani datum. Proveri datum u konfiguraciji.");
                    return;
                }

                // Otvaranje WCF kanala
                factory = new ChannelFactory<IConsumptionService>("ConsumptionServiceEndpoint");
                proxy = factory.CreateChannel();

                // StartSession
                SessionMeta meta = new SessionMeta
                {
                    CountryCode = countryCode,
                    Date = selectedDate,
                    SourceFileName = Path.GetFileName(csvPath),
                    TotalSamples = totalForDay
                };
                proxy.StartSession(meta);
                Console.WriteLine("StartSession uspesno.");

                // Prolazak kroz redove i slanje
                int sentCount = 0;
                int rejectedCount = 0;

                foreach (CsvRow row in csvReader.ReadRows())
                {
                    // Izvuci timestamp iz reda
                    string utcStr = row.Fields[csvReader.IdxUtcTimestamp];

                    if (string.IsNullOrWhiteSpace(utcStr))
                    {
                        WriteRejected(rejectedWriter, row, "Prazan utc_timestamp");
                        rejectedCount++;
                        continue;
                    }

                    DateTime utcTime;
                    if (!DateTime.TryParse(utcStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out utcTime))
                    {
                        WriteRejected(rejectedWriter, row, $"Neispravan utc_timestamp: '{utcStr}'");
                        rejectedCount++;
                        continue;
                    }

                    // Filtriraj: da li pripada odabranom danu
                    if (utcTime.Date != selectedDate.Date)
                    {
                        continue; // Preskoci, ne ide u rejected (nije greska, samo nije nas dan)
                    }

                    // Parsiranje cet_cest_timestamp (lokalno vreme)
                    string localStr = row.Fields[csvReader.IdxCetCestTimestamp];
                    DateTime localTime;
                    if (!DateTime.TryParse(localStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out localTime))
                    {
                        WriteRejected(rejectedWriter, row, $"Neispravan cet_cest_timestamp: '{localStr}'");
                        rejectedCount++;
                        continue;
                    }

                    // Parsiranje ActualMW
                    string actualStr = row.Fields[csvReader.IdxActual];
                    if (string.IsNullOrWhiteSpace(actualStr))
                    {
                        WriteRejected(rejectedWriter, row, "ActualMW je prazan (NaN/missing)");
                        rejectedCount++;
                        continue;
                    }

                    double actualMW;
                    if (!double.TryParse(actualStr, NumberStyles.Any, CultureInfo.InvariantCulture, out actualMW))
                    {
                        WriteRejected(rejectedWriter, row, $"ActualMW se ne moze parsirati: '{actualStr}'");
                        rejectedCount++;
                        continue;
                    }

                    if (double.IsNaN(actualMW))
                    {
                        WriteRejected(rejectedWriter, row, "ActualMW je NaN");
                        rejectedCount++;
                        continue;
                    }

                    // Parsiranje ForecastMW
                    string forecastStr = row.Fields[csvReader.IdxForecast];
                    if (string.IsNullOrWhiteSpace(forecastStr))
                    {
                        WriteRejected(rejectedWriter, row, "ForecastMW je prazan (NaN/missing)");
                        rejectedCount++;
                        continue;
                    }

                    double forecastMW;
                    if (!double.TryParse(forecastStr, NumberStyles.Any, CultureInfo.InvariantCulture, out forecastMW))
                    {
                        WriteRejected(rejectedWriter, row, $"ForecastMW se ne moze parsirati: '{forecastStr}'");
                        rejectedCount++;
                        continue;
                    }

                    if (double.IsNaN(forecastMW))
                    {
                        WriteRejected(rejectedWriter, row, "ForecastMW je NaN");
                        rejectedCount++;
                        continue;
                    }

                    // Sve je u redu - pravimo sample i saljemo
                    HourlyConsumptionSample sample = new HourlyConsumptionSample
                    {
                        TimestampUtc = utcTime,
                        TimestampLocal = localTime,
                        Hour = utcTime.Hour,
                        ActualMW = actualMW,
                        ForecastMW = forecastMW,
                        CountryCode = countryCode,
                        RowIndex = row.RowIndex
                    };

                    try
                    {
                        proxy.PushSample(sample);
                        sentCount++;
                        Console.WriteLine($"Poslat sat {sample.Hour}: Actual={sample.ActualMW} MW, Forecast={sample.ForecastMW} MW");
                    }
                    catch (FaultException<ValidationFault> vf)
                    {
                        WriteRejected(rejectedWriter, row, $"Server odbio (validacija): {vf.Detail.Message}");
                        rejectedCount++;
                    }
                    catch (FaultException<DataFormatFault> df)
                    {
                        WriteRejected(rejectedWriter, row, $"Server odbio (format): {df.Detail.Message}");
                        rejectedCount++;
                    }
                }

                proxy.EndSession();
                Console.WriteLine($"\nZavrseno. Poslato: {sentCount}, Odbaceno: {rejectedCount}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GRESKA] {ex.Message}");
            }
            finally
            {
                if (csvReader != null) csvReader.Dispose();
                if (rejectedWriter != null) rejectedWriter.Dispose();

                if (proxy != null)
                {
                    try { ((IClientChannel)proxy).Close(); }
                    catch { ((IClientChannel)proxy).Abort(); }
                }
                if (factory != null)
                {
                    try { factory.Close(); }
                    catch { factory.Abort(); }
                }
            }

            Console.WriteLine("Pritisni bilo koji taster za izlaz...");
            Console.ReadKey();
        }

        private static void WriteRejected(StreamWriter writer, CsvRow row, string reason)
        {
            // Escape-uj zarez u razlogu (zato sto je CSV)
            string safeReason = reason.Replace(",", ";");
            writer.WriteLine($"{row.RowIndex},\"{safeReason}\",\"{row.RawLine}\"");
        }

        // Pomocna metoda: prebroji redove za odabrani dan (da bismo znali TotalSamples)
        private static int CountRowsForDay(string csvPath, string countryCode, DateTime selectedDate)
        {
            int count = 0;
            using (CsvReader tempReader = new CsvReader(csvPath, countryCode))
            {
                foreach (CsvRow row in tempReader.ReadRows())
                {
                    string utcStr = row.Fields[tempReader.IdxUtcTimestamp];
                    if (string.IsNullOrWhiteSpace(utcStr)) continue;

                    DateTime utcTime;
                    if (!DateTime.TryParse(utcStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out utcTime))
                        continue;

                    if (utcTime.Date == selectedDate.Date)
                        count++;
                }
            }
            return count;
        }
    }
}