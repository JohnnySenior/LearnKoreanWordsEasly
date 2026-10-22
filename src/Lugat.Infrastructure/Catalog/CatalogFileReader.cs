using System.Text;
using ExcelDataReader;
using Lugat.Application;
using Lugat.Application.Catalog;

namespace Lugat.Infrastructure.Catalog;

/// <summary>
/// Reads the first sheet of the official list (spec §2.1): Excel (.xls from 2003 or .xlsx) through ExcelDataReader,
/// or the text version (tab- or comma-separated) in UTF-8 or the Korean code page 949 it was published in.
/// </summary>
public sealed class CatalogFileReader : ICatalogFileReader
{
    public const long MaxBytes = 20 * 1024 * 1024;

    static CatalogFileReader() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); // .xls and CP949 text

    public IReadOnlyList<IReadOnlyList<string?>> ReadRows(Stream content, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        try
        {
            return extension switch
            {
                ".xls" or ".xlsx" => ReadExcel(content),
                ".txt" or ".tsv" or ".csv" => ReadText(content),
                _ => throw new ValidationException("Fayl turi qo‘llab-quvvatlanmaydi: .xls, .xlsx yoki .txt yuklang."),
            };
        }
        catch (Exception ex) when (ex is ExcelDataReader.Exceptions.ExcelReaderException or InvalidDataException or IOException)
        {
            throw new ValidationException("Faylni o‘qib bo‘lmadi. U buzilgan yoki boshqa formatda bo‘lishi mumkin.");
        }
    }

    private static List<IReadOnlyList<string?>> ReadExcel(Stream content)
    {
        using var reader = ExcelReaderFactory.CreateReader(content);
        var rows = new List<IReadOnlyList<string?>>();
        while (reader.Read())
        {
            var row = new string?[reader.FieldCount];
            for (var i = 0; i < row.Length; i++)
            {
                row[i] = reader.GetValue(i) switch
                {
                    null => null,
                    double d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    var v => Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture),
                };
            }

            rows.Add(row);
        }

        return rows;
    }

    private static List<IReadOnlyList<string?>> ReadText(Stream content)
    {
        using var buffer = new MemoryStream();
        content.CopyTo(buffer);
        var lines = Decode(buffer.ToArray()).Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        // Tab- or comma-separated; otherwise columns separated by spaces (the parser joins a gloss with spaces again).
        var sample = lines.Take(20).ToList();
        return sample.Any(l => l.Contains('\t')) ? lines.Select(l => (IReadOnlyList<string?>)l.Split('\t')).ToList()
            : sample.Count(l => l.Contains(',')) > sample.Count / 2 ? lines.Select(l => (IReadOnlyList<string?>)l.Split(',')).ToList()
            : lines.Select(l => (IReadOnlyList<string?>)l.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToList();
    }

    /// <summary>By the byte order mark (UTF-8, or UTF-16 as Excel saves "Unicode text"), else UTF-8, else Korean CP949.</summary>
    public static string Decode(byte[] bytes)
    {
        if (bytes is [0xFF, 0xFE, ..])
        {
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        }

        if (bytes is [0xFE, 0xFF, ..])
        {
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        }

        try
        {
            return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes).TrimStart('\uFEFF');
        }
        catch (DecoderFallbackException)
        {
            return Encoding.GetEncoding(949).GetString(bytes);
        }
    }
}
