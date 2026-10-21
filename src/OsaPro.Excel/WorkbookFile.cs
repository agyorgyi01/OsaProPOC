using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace OsaPro.Excel;

internal static class WorkbookFile
{
    public const uint TextFormatIndex = 1;

    public static byte[] Create(int year, int month, string insDate, string noticeDays)
    {
        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, true))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();
            var styles = workbookPart.AddNewPart<WorkbookStylesPart>();
            styles.Stylesheet = TextStylesheet();
            styles.Stylesheet.Save();

            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            AddSheet(
                workbookPart,
                sheets,
                1,
                WorkbookRules.ParametersSheet,
                [
                    TextRow(1, ("A1", WorkbookRules.InsDateKey, null), ("B1", insDate, TextFormatIndex)),
                    TextRow(2, ("A2", WorkbookRules.NoticeDaysKey, null), ("B2", noticeDays, TextFormatIndex))
                ]);
            var headers = WorkbookRules.Columns
                .Select((name, index) => ($"{ColumnName(index)}1", name, (uint?)null))
                .ToArray();
            AddSheet(
                workbookPart,
                sheets,
                2,
                WorkbookRules.MonthName(month),
                [TextRow(1, headers)]);
            workbookPart.Workbook.Save();
        }

        return stream.ToArray();
    }

    public static string ColumnName(int index)
    {
        var number = index + 1;
        var name = "";
        while (number > 0)
        {
            number--;
            name = (char)('A' + (number % 26)) + name;
            number /= 26;
        }

        return name;
    }

    public static byte[] Repair(byte[] source, int month, bool createParameters, string insDate, string noticeDays)
    {
        using var stream = new MemoryStream();
        stream.Write(source);
        stream.Position = 0;
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var workbookPart = document.WorkbookPart ?? throw new InvalidOperationException("A munkafüzet hiányzik.");
            var sheets = workbookPart.Workbook.Sheets ?? workbookPart.Workbook.AppendChild(new Sheets());
            if (createParameters && FindSheet(sheets, WorkbookRules.ParametersSheet) is null)
            {
                AddSheet(
                    workbookPart,
                    sheets,
                    NextSheetId(sheets),
                    WorkbookRules.ParametersSheet,
                    [
                        TextRow(1, ("A1", WorkbookRules.InsDateKey, null), ("B1", insDate, null)),
                        TextRow(2, ("A2", WorkbookRules.NoticeDaysKey, null), ("B2", noticeDays, null))
                    ]);
            }

            var monthName = WorkbookRules.MonthName(month);
            var monthSheet = FindSheet(sheets, monthName);
            if (monthSheet is null)
            {
                var headers = WorkbookRules.Columns
                    .Select((name, index) => ($"{ColumnName(index)}1", name, (uint?)null))
                    .ToArray();
                AddSheet(workbookPart, sheets, NextSheetId(sheets), monthName, [TextRow(1, headers)]);
            }
            else
            {
                var part = (WorksheetPart)workbookPart.GetPartById(monthSheet.Id!);
                var table = workbookPart.SharedStringTablePart?.SharedStringTable;
                foreach (var insert in WorkbookRules.PlanColumnInserts(HeaderRow(part.Worksheet, table)))
                {
                    InsertColumn(part.Worksheet, insert.Index, insert.Name);
                }

                part.Worksheet.Save();
            }

            workbookPart.Workbook.Save();
        }

        return stream.ToArray();
    }

    public static IReadOnlyList<(string Key, string Value)>? ReadParameterRows(byte[] source)
    {
        using var stream = new MemoryStream(source);
        using var document = SpreadsheetDocument.Open(stream, false);
        var workbookPart = document.WorkbookPart;
        var sheets = workbookPart?.Workbook.Sheets;
        if (workbookPart is null || sheets is null)
        {
            return null;
        }

        var sheet = FindSheet(sheets, WorkbookRules.ParametersSheet);
        if (sheet?.Id is null)
        {
            return null;
        }

        var part = (WorksheetPart)workbookPart.GetPartById(sheet.Id);
        var table = workbookPart.SharedStringTablePart?.SharedStringTable;
        var rows = new List<(string Key, string Value)>();
        foreach (var row in part.Worksheet.Descendants<Row>())
        {
            var key = TextAt(row, 0, table);
            var value = TextAt(row, 1, table);
            if (key.Length == 0 && value.Length == 0)
            {
                continue;
            }

            rows.Add((key, value));
        }

        return rows;
    }

    private static void InsertColumn(Worksheet worksheet, int index, string header)
    {
        var data = worksheet.GetFirstChild<SheetData>() ?? worksheet.InsertAt(new SheetData(), 0);
        foreach (var row in data.Elements<Row>())
        {
            foreach (var cell in row.Elements<Cell>())
            {
                if (cell.CellReference?.Value is not string reference || ColumnIndex(reference) < index)
                {
                    continue;
                }

                cell.CellReference = Shift(reference);
            }
        }

        var headerRow = data.Elements<Row>().FirstOrDefault(row => row.RowIndex?.Value == 1);
        if (headerRow is null)
        {
            headerRow = new Row { RowIndex = 1 };
            data.InsertAt(headerRow, 0);
        }

        var headerReference = ColumnName(index) + "1";
        var headerCell = new Cell
        {
            CellReference = headerReference,
            DataType = CellValues.InlineString,
            InlineString = new InlineString(Preserve(header))
        };
        var next = headerRow.Elements<Cell>()
            .FirstOrDefault(cell => cell.CellReference?.Value is string cellReference && ColumnIndex(cellReference) > index);
        if (next is null)
        {
            headerRow.Append(headerCell);
        }
        else
        {
            headerRow.InsertBefore(headerCell, next);
        }
    }

    private static List<string> HeaderRow(Worksheet worksheet, SharedStringTable? table)
    {
        var row = worksheet.Descendants<Row>().FirstOrDefault(item => item.RowIndex?.Value == 1);
        if (row is null)
        {
            return [];
        }

        var cells = row.Elements<Cell>().Where(cell => cell.CellReference?.Value is not null).ToList();
        if (cells.Count == 0)
        {
            return [];
        }

        var headers = Enumerable.Repeat("", cells.Max(cell => ColumnIndex(cell.CellReference!.Value!)) + 1).ToList();
        foreach (var cell in cells)
        {
            headers[ColumnIndex(cell.CellReference!.Value!)] = CellText(cell, table);
        }

        return headers;
    }

    private static string TextAt(Row row, int column, SharedStringTable? table)
    {
        var cell = row.Elements<Cell>().FirstOrDefault(item =>
            item.CellReference?.Value is string reference && ColumnIndex(reference) == column);
        return cell is null ? "" : CellText(cell, table);
    }

    private static string CellText(Cell cell, SharedStringTable? table)
    {
        if (cell.InlineString is not null)
        {
            return cell.InlineString.InnerText;
        }

        if (cell.DataType?.Value == CellValues.SharedString
            && table is not null
            && int.TryParse(cell.CellValue?.Text, out var index)
            && index >= 0
            && index < table.Count())
        {
            return table.ElementAt(index).InnerText;
        }

        return cell.CellValue?.Text ?? "";
    }

    private static Sheet? FindSheet(Sheets sheets, string name)
    {
        return sheets.Elements<Sheet>().FirstOrDefault(sheet => WorkbookRules.SameName(sheet.Name?.Value, name));
    }

    private static uint NextSheetId(Sheets sheets)
    {
        return sheets.Elements<Sheet>().Select(sheet => sheet.SheetId?.Value ?? 0).DefaultIfEmpty<uint>(0).Max() + 1;
    }

    private static int ColumnIndex(string reference)
    {
        var index = 0;
        foreach (var letter in reference.TakeWhile(char.IsLetter))
        {
            index = (index * 26) + (char.ToUpperInvariant(letter) - 'A' + 1);
        }

        return index - 1;
    }

    private static string Shift(string reference)
    {
        var letters = new string(reference.TakeWhile(char.IsLetter).ToArray());
        return ColumnName(ColumnIndex(reference) + 1) + reference[letters.Length..];
    }

    private static Stylesheet TextStylesheet()
    {
        var numbering = new NumberingFormats(
            new NumberingFormat { NumberFormatId = 164, FormatCode = "@" })
        {
            Count = 1
        };
        var fonts = new Fonts(new Font()) { Count = 1 };
        var fills = new Fills(
            new Fill(new PatternFill { PatternType = PatternValues.None }),
            new Fill(new PatternFill { PatternType = PatternValues.Gray125 }))
        { Count = 2 };
        var borders = new Borders(new Border()) { Count = 1 };
        var cellStyleFormats = new CellStyleFormats(new CellFormat()) { Count = 1 };
        var cellFormats = new CellFormats(
            new CellFormat(),
            new CellFormat { NumberFormatId = 164, ApplyNumberFormat = true })
        { Count = 2 };
        return new Stylesheet(numbering, fonts, fills, borders, cellStyleFormats, cellFormats);
    }

    private static void AddSheet(
        WorkbookPart workbookPart,
        Sheets sheets,
        uint sheetId,
        string name,
        IReadOnlyList<Row> rows)
    {
        var part = workbookPart.AddNewPart<WorksheetPart>();
        var data = new SheetData();
        foreach (var row in rows)
        {
            data.Append(row);
        }

        part.Worksheet = new Worksheet(data);
        part.Worksheet.Save();
        sheets.Append(new Sheet
        {
            Name = name,
            SheetId = sheetId,
            Id = workbookPart.GetIdOfPart(part)
        });
    }

    private static Row TextRow(uint index, params (string Reference, string Text, uint? Style)[] cells)
    {
        var row = new Row { RowIndex = index };
        foreach (var (reference, text, style) in cells)
        {
            var cell = new Cell
            {
                CellReference = reference,
                DataType = CellValues.InlineString,
                InlineString = new InlineString(Preserve(text))
            };
            if (style is not null)
            {
                cell.StyleIndex = style.Value;
            }

            row.Append(cell);
        }

        return row;
    }

    private static Text Preserve(string value)
    {
        var text = new Text(value);
        if (value.Any(char.IsWhiteSpace))
        {
            text.Space = SpaceProcessingModeValues.Preserve;
        }

        return text;
    }
}
