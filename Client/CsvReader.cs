using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Common;

namespace Client
{
    public class CsvReader : IDisposable
    {
        private StreamReader _reader;
        private string _filePath;
        private string _countryCode;

        // Indeksi kolona koje su nam potrebne (saznaju se iz header-a)
        private int _idxUtcTimestamp = -1;
        private int _idxCetCestTimestamp = -1;
        private int _idxActual = -1;
        private int _idxForecast = -1;

        public CsvReader(string filePath, string countryCode)
        {
            _filePath = filePath;
            _countryCode = countryCode;

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"CSV fajl ne postoji: {filePath}");
            }

            _reader = new StreamReader(filePath);
            ParseHeader();
        }

        private void ParseHeader()
        {
            string headerLine = _reader.ReadLine();
            if (headerLine == null)
            {
                throw new Exception("CSV fajl je prazan.");
            }

            string[] columns = headerLine.Split(',');

            string actualColumnName = $"{_countryCode}_load_actual_entsoe_transparency";
            string forecastColumnName = $"{_countryCode}_load_forecast_entsoe_transparency";

            for (int i = 0; i < columns.Length; i++)
            {
                if (columns[i] == "utc_timestamp") _idxUtcTimestamp = i;
                else if (columns[i] == "cet_cest_timestamp") _idxCetCestTimestamp = i;
                else if (columns[i] == actualColumnName) _idxActual = i;
                else if (columns[i] == forecastColumnName) _idxForecast = i;
            }

            // Greska konfiguracije: ako za zemlju ne postoje obe kolone
            if (_idxActual == -1 || _idxForecast == -1)
            {
                throw new Exception($"Greska konfiguracije: za zemlju '{_countryCode}' ne postoje kolone '{actualColumnName}' i/ili '{forecastColumnName}' u CSV fajlu.");
            }

            if (_idxUtcTimestamp == -1 || _idxCetCestTimestamp == -1)
            {
                throw new Exception("Greska konfiguracije: u CSV fajlu nedostaju kolone 'utc_timestamp' ili 'cet_cest_timestamp'.");
            }
        }

        // Vraca jedan po jedan red iz CSV-a (uz pomoc yield)
        public IEnumerable<CsvRow> ReadRows()
        {
            string line;
            int rowIndex = 0;
            while ((line = _reader.ReadLine()) != null)
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

        public int IdxUtcTimestamp => _idxUtcTimestamp;
        public int IdxCetCestTimestamp => _idxCetCestTimestamp;
        public int IdxActual => _idxActual;
        public int IdxForecast => _idxForecast;

        public void Dispose()
        {
            if (_reader != null)
            {
                _reader.Dispose();
                _reader = null;
            }
        }
    }

    // Pomocna klasa - jedan procitani red iz CSV-a (jos ne parsiran u sample)
    public class CsvRow
    {
        public int RowIndex { get; set; }
        public string RawLine { get; set; }
        public string[] Fields { get; set; }
    }
}