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
        var bytes = buffer.ToArray();
        string text;
        try
        {
            text = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            text = Encoding.GetEncoding(949).GetString(bytes);
        }

        var lines = text.TrimStart('﻿').Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        var separator = lines.Take(20).Any(l => l.Contains('\t')) ? '\t' : ',';
        return lines.Select(l => (IReadOnlyList<string?>)l.Split(separator)).ToList();
    }
}
