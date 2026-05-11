using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Common;

namespace Client
{
    public class CsvReader : IDisposable
    {
        private StreamReader sr;
        private string filePath;
        private string countryCode;

        // Indeksi kolona koje su nam potrebne
        private int idxUtcTimestamp = -1;
        private int idxCetCestTimestamp = -1;
        private int idxActual = -1;
        private int idxForecast = -1;

        public CsvReader(string filePath, string countryCode)
        {
            this.filePath = filePath;
            this.countryCode = countryCode;

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"CSV fajl ne postoji: {filePath}");
            }

            sr = new StreamReader(filePath);
            ParseHeader();
        }

        private void ParseHeader()
        {
            string headerLine = sr.ReadLine();
            if (headerLine == null)
            {
                throw new Exception("CSV fajl je prazan.");
            }

            string[] columns = headerLine.Split(',');

            string actualColumnName = $"{countryCode}_load_actual_entsoe_transparency";
            string forecastColumnName = $"{countryCode}_load_forecast_entsoe_transparency";

            for (int i = 0; i < columns.Length; i++)
            {
                if (columns[i] == "utc_timestamp") idxUtcTimestamp = i;
                else if (columns[i] == "cet_cest_timestamp") idxCetCestTimestamp = i;
                else if (columns[i] == actualColumnName) idxActual = i;
                else if (columns[i] == forecastColumnName) idxForecast = i;
            }

            // Greska konfiguracije ako za zemlju ne postoje obe kolone
            if (idxActual == -1 || idxForecast == -1)
            {
                throw new Exception($"Greska konfiguracije: za zemlju '{countryCode}' ne postoje kolone '{actualColumnName}' i/ili '{forecastColumnName}' u CSV fajlu.");
            }

            if (idxUtcTimestamp == -1 || idxCetCestTimestamp == -1)
            {
                throw new Exception("Greska konfiguracije: u CSV fajlu nedostaju kolone 'utc_timestamp' ili 'cet_cest_timestamp'.");
            }
        }

        // Vraca jedan po jedan red da ne bismo ucitavali ceo CSV file
        public IEnumerable<CsvRow> ReadRows()
        {
            string line;
            int rowIndex = 0;
            while ((line = sr.ReadLine()) != null)
            {
                rowIndex++;
                yield return new CsvRow 
                {
                    RowIndex = rowIndex,
                    RawLine = line,
                    Fields = line.Split(',')
                };
            }
        }

        public int IdxUtcTimestamp => idxUtcTimestamp;
        public int IdxCetCestTimestamp => idxCetCestTimestamp;
        public int IdxActual => idxActual;
        public int IdxForecast => idxForecast;

        public void Dispose()
        {
            if (sr != null)
            {
                sr.Dispose();
                sr = null;
            }
        }
    }

    // Pomocna klasa za jedan procitani red iz CSV fajla
    public class CsvRow
    {
        public int RowIndex { get; set; }
        public string RawLine { get; set; }
        public string[] Fields { get; set; }
    }
}