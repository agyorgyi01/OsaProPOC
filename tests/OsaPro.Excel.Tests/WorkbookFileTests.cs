using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using OsaPro.Excel;

namespace OsaPro.Excel.Tests;

public class WorkbookFileTests
{
    [Fact]
    public void New_may_workbook_contains_only_parameters_and_the_requested_month()
    {
        var bytes = WorkbookFile.Create(2026, 5, "2025-12-31T00:00:00Z", "7,5,1");

        using var document = SpreadsheetDocument.Open(new MemoryStream(bytes), false);
        var workbook = document.WorkbookPart!;
        var names = workbook.Workbook.Sheets!.Elements<Sheet>().Select(sheet => sheet.Name!.Value!).ToList();
        Assert.Equal(["Paraméterek", "Május"], names);
        Assert.DoesNotContain("Június", names);

        var parameters = Worksheet(workbook, "Paraméterek");
        Assert.Equal("InsDate", CellText(parameters, "A1"));
        Assert.Equal("Előértesítési napok", CellText(parameters, "A2"));
        Assert.Equal("2025-12-31T00:00:00Z", CellText(parameters, "B1"));
        Assert.Equal("7,5,1", CellText(parameters, "B2"));
        Assert.Equal(WorkbookFile.TextFormatIndex, Cell(parameters, "B1").StyleIndex!.Value);
        Assert.Equal(WorkbookFile.TextFormatIndex, Cell(parameters, "B2").StyleIndex!.Value);
        var format = workbook.WorkbookStylesPart!.Stylesheet!.NumberingFormats!
            .Elements<NumberingFormat>()
            .Single(item => item.NumberFormatId!.Value == 164);
        Assert.Equal("@", format.FormatCode!.Value);

        var month = Worksheet(workbook, "Május");
        var headers = month.Worksheet.Descendants<Cell>().Select(CellText).ToList();
        Assert.Equal(WorkbookRules.Columns, headers);
        Assert.DoesNotContain(month.Worksheet.Descendants<Row>(), row => row.RowIndex!.Value > 1);
    }

    [Fact]
    public void Repair_shifts_cells_with_formula_text_and_style_and_leaves_other_sheets()
    {
        var headers = WorkbookRules.Columns.Where(name => name != "Bankszámlaszám")
            .Select((name, index) => (WorkbookFile.ColumnName(index) + "1", name, (string?)null, (uint?)null))
            .ToList();
        headers.Add(("A2", "bal", null, null));
        headers.Add(("C2", null, "=B2", (uint?)4));
        var bytes = Build(
            ("Május", headers.ToArray()),
            ("Egyéb", [("A1", "marad", null, null)]));
        bytes = WithMerge(bytes, "Egyéb", "A1:B1");

        var repaired = WorkbookFile.Repair(bytes, 5, createParameters: false, "", "");

        using var document = SpreadsheetDocument.Open(new MemoryStream(repaired), false);
        var month = Worksheet(document.WorkbookPart!, "Május");
        Assert.Equal("bal", CellText(month, "A2"));
        Assert.Equal("A2", Cell(month, "A2").CellReference!.Value);
        var moved = Cell(month, "D2");
        Assert.Equal("=B2", moved.CellFormula!.Text);
        Assert.Equal((uint)4, moved.StyleIndex!.Value);
        Assert.Equal("Bankszámlaszám", CellText(month, "C1"));
        Assert.DoesNotContain(month.Worksheet.Descendants<Cell>(), cell => cell.CellReference == "C2");
        var other = Worksheet(document.WorkbookPart!, "Egyéb");
        Assert.Equal("marad", CellText(other, "A1"));
        Assert.Equal("A1:B1", other.Worksheet.Elements<MergeCells>().Single().Elements<MergeCell>().Single().Reference!.Value);
        Assert.DoesNotContain(other.Worksheet.Descendants<Cell>(), cell => cell.CellReference == "B1");
    }

    [Fact]
    public void Repair_inserts_missing_columns_around_foreign_cells_and_keeps_parameters()
    {
        var foreign = WorkbookRules.Columns.Where(name => name != "Bankszámlaszám").ToList();
        foreign.Insert(2, "Saját");
        var bytes = Build(
            ("Paraméterek", [("A1", "InsDate", null, null), ("B1", "kept", null, null)]),
            ("Május", foreign.Select((name, index) => (WorkbookFile.ColumnName(index) + "1", name, (string?)null, (uint?)null)).ToArray()));

        var repaired = WorkbookFile.Repair(bytes, 5, createParameters: true, "új", "1");

        using var document = SpreadsheetDocument.Open(new MemoryStream(repaired), false);
        var month = Worksheet(document.WorkbookPart!, "Május");
        var header = month.Worksheet.Descendants<Row>().Single(row => row.RowIndex!.Value == 1)
            .Elements<Cell>()
            .Select(CellText)
            .ToList();
        Assert.Equal("Saját", header[2]);
        Assert.Equal("Bankszámlaszám", header[3]);
        Assert.True(header.IndexOf("Bankszámlaszám") < header.IndexOf("Számlasorszám"));
        var parameters = Worksheet(document.WorkbookPart!, "Paraméterek");
        Assert.Equal("kept", CellText(parameters, "B1"));
        Assert.Equal(2, document.WorkbookPart!.Workbook.Sheets!.Elements<Sheet>().Count());
    }

    [Fact]
    public void Repair_shifts_an_empty_header_right_and_adds_only_the_requested_sheet()
    {
        var bytes = Build(
            ("Paraméterek", [("A1", "InsDate", null, null), ("B1", "kept", null, null)]),
            ("Május", [("A1", "Idegen", null, null), ("A2", "adat", null, null)]));

        var repaired = WorkbookFile.Repair(bytes, 5, createParameters: false, "", "");

        using var document = SpreadsheetDocument.Open(new MemoryStream(repaired), false);
        var names = document.WorkbookPart!.Workbook.Sheets!.Elements<Sheet>().Select(sheet => sheet.Name!.Value).ToList();
        Assert.Equal(["Paraméterek", "Május"], names);
        var month = Worksheet(document.WorkbookPart, "Május");
        var header = month.Worksheet.Descendants<Row>().Single(row => row.RowIndex!.Value == 1).Elements<Cell>().Select(CellText).ToList();
        Assert.Equal(WorkbookRules.Columns, header.Take(16));
        Assert.Equal("Idegen", header[16]);
        Assert.Equal("adat", CellText(month, WorkbookFile.ColumnName(16) + "2"));
    }

    private static WorksheetPart Worksheet(WorkbookPart workbook, string name)
    {
        var sheet = workbook.Workbook.Sheets!.Elements<Sheet>().Single(item => item.Name == name);
        return (WorksheetPart)workbook.GetPartById(sheet.Id!);
    }

    private static Cell Cell(WorksheetPart worksheet, string reference)
    {
        return worksheet.Worksheet.Descendants<Cell>().Single(cell => cell.CellReference == reference);
    }

    private static string CellText(WorksheetPart worksheet, string reference)
    {
        return CellText(Cell(worksheet, reference));
    }

    private static string CellText(Cell cell)
    {
        return cell.InlineString?.Text?.Text ?? "";
    }

    private static byte[] Build(params (string Name, (string Reference, string? Text, string? Formula, uint? Style)[] Cells)[] sheets)
    {
        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();
            var sheetList = workbookPart.Workbook.AppendChild(new Sheets());
            uint id = 1;
            foreach (var (name, cells) in sheets)
            {
                var part = workbookPart.AddNewPart<WorksheetPart>();
                var data = new SheetData();
                var rows = new Dictionary<uint, Row>();
                foreach (var (reference, text, formula, style) in cells)
                {
                    var rowIndex = uint.Parse(new string(reference.SkipWhile(char.IsLetter).ToArray()));
                    if (!rows.TryGetValue(rowIndex, out var row))
                    {
                        row = new Row { RowIndex = rowIndex };
                        rows[rowIndex] = row;
                        data.Append(row);
                    }

                    var cell = new Cell { CellReference = reference };
                    if (formula is not null)
                    {
                        cell.CellFormula = new CellFormula(formula);
                    }

                    if (text is not null)
                    {
                        cell.DataType = CellValues.InlineString;
                        cell.InlineString = new InlineString(new Text(text));
                    }

                    if (style is not null)
                    {
                        cell.StyleIndex = style.Value;
                    }

                    row.Append(cell);
                }

                part.Worksheet = new Worksheet(data);
                part.Worksheet.Save();
                sheetList.Append(new Sheet
                {
                    Name = name,
                    SheetId = id++,
                    Id = workbookPart.GetIdOfPart(part)
                });
            }

            workbookPart.Workbook.Save();
        }

        return stream.ToArray();
    }

    private static byte[] WithMerge(byte[] source, string sheetName, string reference)
    {
        using var stream = new MemoryStream();
        stream.Write(source);
        stream.Position = 0;
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var part = Worksheet(document.WorkbookPart!, sheetName);
            var merge = new MergeCells(new MergeCell { Reference = reference });
            part.Worksheet.InsertAfter(merge, part.Worksheet.GetFirstChild<SheetData>());
            part.Worksheet.Save();
            document.WorkbookPart!.Workbook.Save();
        }

        return stream.ToArray();
    }
}
