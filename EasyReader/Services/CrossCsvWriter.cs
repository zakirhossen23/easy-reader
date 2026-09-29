using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace EasyReader.Services
{
    public class CsvRow : List<string>
    {
        public string LineText { get; set; }
    }
    public class CrossCsvWriter : StreamWriter
    {
        public CrossCsvWriter(string filePath)
            : base(filePath)
        {
        }

        public async Task WriteRow(CsvRow row)
        {
            StringBuilder builder = new StringBuilder();
            bool firstColumn = true;
            foreach (string value in row)
            {
                if (!firstColumn)
                    builder.Append(',');

                builder.Append(value);
                firstColumn = false;
            }
            row.LineText = builder.ToString();
            await WriteLineAsync(row.LineText);
        }
    }
}
