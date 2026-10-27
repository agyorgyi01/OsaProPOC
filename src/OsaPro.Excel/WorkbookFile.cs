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

    internal static RegistrySnapshot ReadSnapshot(IReadOnlyDictionary<int, byte[]> files, DateTimeOffset clock)
    {
        var current = WorkbookRules.CurrentYear(clock);
        int? cursorYear = files.ContainsKey(current)
            ? current
            : files.ContainsKey(current - 1) ? current - 1 : null;
        if (cursorYear is null)
        {
            return new RegistrySnapshot
            {
                HasFile = false,
                NoticeDays = WorkbookRules.DefaultNoticeDays
            };
        }

        var parameters = ReadParameterRows(files[cursorYear.Value]);
        string? insDate = null;
        string? noticeDays = null;
        var insDateSeen = false;
        if (parameters is not null)
        {
            foreach (var (key, value) in parameters)
            {
                if (!insDateSeen && WorkbookRules.SameName(key, WorkbookRules.InsDateKey))
                {
                    insDateSeen = true;
                    insDate = WorkbookRules.CopyValue(value);
                }

                if (noticeDays is null && WorkbookRules.SameName(key, WorkbookRules.NoticeDaysKey))
                {
                    noticeDays = WorkbookRules.CopyValue(value);
                }
            }
        }

        if (!insDateSeen || string.IsNullOrWhiteSpace(insDate))
        {
            throw new ExcelWorkbookException("Az InsDate hiányzik.");
        }

        var rows = new List<StoredInvoiceRow>();
        foreach (var (year, bytes) in files)
        {
            rows.AddRange(ReadInvoices(bytes, year));
        }

        return new RegistrySnapshot
        {
            HasFile = true,
            InsDateText = insDate,
            NoticeDays = noticeDays ?? WorkbookRules.DefaultNoticeDays,
            CursorYear = cursorYear.Value,
            Rows = rows,
            Files = files
        };
    }

    internal static byte[] Apply(byte[]? source, RegistryEdit edit)
    {
        var bytes = source ?? Create(edit.Year, edit.SheetMonth, edit.CarriedInsDate, edit.CarriedNoticeDays);
        var months = edit.NewRows.Select(row => row.Month).ToList();
        if (edit.WriteInsDate)
        {
            months.Add(edit.SheetMonth);
        }

        foreach (var month in months.Distinct())
        {
            bytes = Repair(bytes, month, createParameters: false, insDate: "", noticeDays: "");
        }

        using var stream = new MemoryStream();
        stream.Write(bytes);
        stream.Position = 0;
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var workbookPart = document.WorkbookPart ?? throw new ExcelWorkbookException("A munkafüzet hiányzik.");
            var (dateStyle, numberStyle) = EnsureValueStyles(workbookPart);
            var table = workbookPart.SharedStringTablePart?.SharedStringTable;
            foreach (var row in edit.NewRows)
            {
                var part = Worksheet(workbookPart, WorkbookRules.MonthName(row.Month));
                var index = NextDataRow(part.Worksheet, table);
                WriteInvoiceRow(part.Worksheet, table, index, row.Cells, dateStyle, numberStyle);
                part.Worksheet.Save();
            }

            foreach (var link in edit.Links)
            {
                WriteLink(workbookPart, table, link);
            }

            if (edit.WriteInsDate && edit.Year == edit.CurrentYear && !string.IsNullOrWhiteSpace(edit.InsDate))
            {
                WriteInsDate(workbookPart, table, edit.InsDate);
            }

            workbookPart.Workbook.Save();
        }

        return stream.ToArray();
    }

    private static IReadOnlyList<StoredInvoiceRow> ReadInvoices(byte[] source, int year)
    {
        using var stream = new MemoryStream(source);
        using var document = SpreadsheetDocument.Open(stream, false);
        var workbookPart = document.WorkbookPart;
        var sheets = workbookPart?.Workbook.Sheets;
        if (workbookPart is null || sheets is null)
        {
            return [];
        }

        var table = workbookPart.SharedStringTablePart?.SharedStringTable;
        var rows = new List<StoredInvoiceRow>();
        foreach (var sheet in sheets.Elements<Sheet>())
        {
            var month = Array.FindIndex(WorkbookRules.MonthNames, name => WorkbookRules.SameName(name, sheet.Name?.Value));
            if (month < 0 || sheet.Id is null)
            {
                continue;
            }

            var part = (WorksheetPart)workbookPart.GetPartById(sheet.Id);
            var map = HeaderMap(part.Worksheet, table);
            foreach (var row in part.Worksheet.Descendants<Row>())
            {
                if (row.RowIndex?.Value is not uint index || index < 2)
                {
                    continue;
                }

                var cells = Values(row, map, table);
                if (!cells.Any(value => !string.IsNullOrWhiteSpace(value)))
                {
                    continue;
                }

                rows.Add(new StoredInvoiceRow
                {
                    Year = year,
                    Month = month + 1,
                    RowIndex = index,
                    Cells = cells
                });
            }
        }

        return rows;
    }

    private static void WriteLink(WorkbookPart workbookPart, SharedStringTable? table, RegistryLinkWrite link)
    {
        var sheets = workbookPart.Workbook.Sheets;
        if (sheets is null)
        {
            return;
        }

        foreach (var sheet in sheets.Elements<Sheet>())
        {
            var month = Array.FindIndex(WorkbookRules.MonthNames, name => WorkbookRules.SameName(name, sheet.Name?.Value));
            if (month < 0 || sheet.Id is null)
            {
                continue;
            }

            var part = (WorksheetPart)workbookPart.GetPartById(sheet.Id);
            var map = HeaderMap(part.Worksheet, table);
            if (!map.TryGetValue("Adószám", out var taxColumn) || !map.TryGetValue("Számlasorszám", out var invoiceColumn))
            {
                continue;
            }

            foreach (var row in part.Worksheet.Descendants<Row>())
            {
                var tax = TextAt(row, taxColumn, table).Trim();
                var invoice = TextAt(row, invoiceColumn, table).Trim();
                if (!string.Equals(tax, link.TaxNumber.Trim(), StringComparison.Ordinal)
                    || !string.Equals(invoice, link.InvoiceNumber.Trim(), StringComparison.Ordinal))
                {
                    continue;
                }

                if (!map.TryGetValue("Kapcsolt bizonylatok", out var relatedColumn))
                {
                    return;
                }

                Place(row, TextCell(ColumnName(relatedColumn) + row.RowIndex!.Value, link.Related, TextFormatIndex));
                part.Worksheet.Save();
                return;
            }
        }
    }

    private static void WriteInsDate(WorkbookPart workbookPart, SharedStringTable? table, string insDate)
    {
        var part = Worksheet(workbookPart, WorkbookRules.ParametersSheet);
        foreach (var row in part.Worksheet.Descendants<Row>())
        {
            if (!WorkbookRules.SameName(TextAt(row, 0, table), WorkbookRules.InsDateKey))
            {
                continue;
            }

            Place(row, TextCell(ColumnName(1) + row.RowIndex!.Value, insDate, TextFormatIndex));
            part.Worksheet.Save();
            return;
        }

        throw new ExcelWorkbookException("Az InsDate hiányzik.");
    }

    private static WorksheetPart Worksheet(WorkbookPart workbookPart, string name)
    {
        var sheet = FindSheet(workbookPart.Workbook.Sheets ?? throw new ExcelWorkbookException("A munkafüzet hiányzik."), name)
            ?? throw new ExcelWorkbookException("A lap nem található.");
        return (WorksheetPart)workbookPart.GetPartById(sheet.Id!);
    }

    private static void WriteInvoiceRow(
        Worksheet worksheet,
        SharedStringTable? table,
        uint rowIndex,
        string[] cells,
        uint dateStyle,
        uint numberStyle)
    {
        var map = HeaderMap(worksheet, table);
        var data = worksheet.GetFirstChild<SheetData>() ?? worksheet.InsertAt(new SheetData(), 0);
        var row = data.Elements<Row>().FirstOrDefault(item => item.RowIndex?.Value == rowIndex);
        if (row is null)
        {
            row = new Row { RowIndex = rowIndex };
            var next = data.Elements<Row>().FirstOrDefault(item => item.RowIndex?.Value > rowIndex);
            if (next is null)
            {
                data.Append(row);
            }
            else
            {
                data.InsertBefore(row, next);
            }
        }

        for (var index = 0; index < WorkbookRules.Columns.Length && index < cells.Length; index++)
        {
            var value = cells[index] ?? "";
            if (string.IsNullOrWhiteSpace(value) || !map.TryGetValue(WorkbookRules.Columns[index], out var column))
            {
                continue;
            }

            var reference = ColumnName(column) + rowIndex;
            if (index is 6 or 7 && DateOnly.TryParseExact(value, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var date))
            {
                Place(row, NumberCell(reference, ExcelDate(date).ToString(System.Globalization.CultureInfo.InvariantCulture), dateStyle));
            }
            else if (index is 8 or 10 && decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var amount))
            {
                Place(row, NumberCell(reference, amount.ToString(System.Globalization.CultureInfo.InvariantCulture), numberStyle));
            }
            else
            {
                Place(row, TextCell(reference, value, TextFormatIndex));
            }
        }
    }

    private static uint NextDataRow(Worksheet worksheet, SharedStringTable? table)
    {
        var map = HeaderMap(worksheet, table);
        uint last = 1;
        foreach (var row in worksheet.Descendants<Row>())
        {
            if (row.RowIndex?.Value is not uint index || index < 2)
            {
                continue;
            }

            if (Values(row, map, table).Any(value => !string.IsNullOrWhiteSpace(value)))
            {
                last = Math.Max(last, index);
            }
        }

        return last + 1;
    }

    private static string[] Values(Row row, IReadOnlyDictionary<string, int> map, SharedStringTable? table)
    {
        var cells = new string[WorkbookRules.Columns.Length];
        for (var index = 0; index < WorkbookRules.Columns.Length; index++)
        {
            cells[index] = map.TryGetValue(WorkbookRules.Columns[index], out var column)
                ? TextAt(row, column, table)
                : "";
        }

        return cells;
    }

    private static Dictionary<string, int> HeaderMap(Worksheet worksheet, SharedStringTable? table)
    {
        var headers = HeaderRow(worksheet, table);
        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < headers.Count; index++)
        {
            foreach (var name in WorkbookRules.Columns)
            {
                if (map.ContainsKey(name))
                {
                    continue;
                }

                if (WorkbookRules.SameName(headers[index], name))
                {
                    map[name] = index;
                }
            }
        }

        return map;
    }

    private static void Place(Row row, Cell cell)
    {
        var column = ColumnIndex(cell.CellReference!.Value!);
        var existing = row.Elements<Cell>().FirstOrDefault(item =>
            item.CellReference?.Value is string reference && ColumnIndex(reference) == column);
        existing?.Remove();
        var next = row.Elements<Cell>().FirstOrDefault(item =>
            item.CellReference?.Value is string reference && ColumnIndex(reference) > column);
        if (next is null)
        {
            row.Append(cell);
        }
        else
        {
            row.InsertBefore(cell, next);
        }
    }

    private static Cell TextCell(string reference, string value, uint style)
    {
        return new Cell
        {
            CellReference = reference,
            DataType = CellValues.InlineString,
            StyleIndex = style,
            InlineString = new InlineString(Preserve(value))
        };
    }

    private static Cell NumberCell(string reference, string value, uint style)
    {
        return new Cell
        {
            CellReference = reference,
            StyleIndex = style,
            CellValue = new CellValue(value)
        };
    }

    private static int ExcelDate(DateOnly date)
    {
        return date.DayNumber - new DateOnly(1899, 12, 30).DayNumber;
    }

    private static (uint DateStyle, uint NumberStyle) EnsureValueStyles(WorkbookPart workbookPart)
    {
        var stylesPart = workbookPart.WorkbookStylesPart;
        if (stylesPart?.Stylesheet is null)
        {
            stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
            stylesPart.Stylesheet = TextStylesheet();
        }

        var stylesheet = stylesPart.Stylesheet;
        var numbering = stylesheet.NumberingFormats ?? stylesheet.InsertAt(new NumberingFormats(), 0);
        if (!numbering.Elements<NumberingFormat>().Any(item => item.NumberFormatId?.Value == 165))
        {
            numbering.Append(new NumberingFormat { NumberFormatId = 165, FormatCode = "0.00" });
            numbering.Count = (uint)numbering.Elements<NumberingFormat>().Count();
        }

        var formats = stylesheet.CellFormats ?? stylesheet.AppendChild(new CellFormats());
        var dateStyle = FindOrAddFormat(formats, 14);
        var numberStyle = FindOrAddFormat(formats, 165);
        stylesheet.Save();
        return (dateStyle, numberStyle);
    }

    private static uint FindOrAddFormat(CellFormats formats, uint numberFormatId)
    {
        var existing = formats.Elements<CellFormat>().ToList();
        for (var index = 0; index < existing.Count; index++)
        {
            if (existing[index].NumberFormatId?.Value == numberFormatId && existing[index].ApplyNumberFormat?.Value == true)
            {
                return (uint)index;
            }
        }

        formats.Append(new CellFormat { NumberFormatId = numberFormatId, ApplyNumberFormat = true });
        formats.Count = (uint)formats.Elements<CellFormat>().Count();
        return formats.Count - 1;
    }
}
