using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using ESEvents.Application.Departments;
using ESEvents.Application.Events;
using ESEvents.Application.Users;
using S = DocumentFormat.OpenXml.Spreadsheet;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace ESEvents.Web.Services;

public sealed class EventExportService(
    IEventService events,
    IDepartmentService departments,
    IUserService users) : IEventExportService
{
    public async Task<byte[]> ExportAsync(
        Guid userId, bool isAdministrator, DateOnly start, DateOnly end,
        string timeZoneId, bool includeParticipants, string format, CancellationToken ct = default)
    {
        if (end < start || end == DateOnly.MaxValue)
            throw new ArgumentException("Choose a valid date range with an end date on or after the start date.");
        if (format is not ("docx" or "xlsx"))
            throw new ArgumentException("Choose Word or Excel as the export format.");
        if (string.IsNullOrWhiteSpace(timeZoneId))
            throw new ArgumentException("A time zone is required.");

        TimeZoneInfo zone;
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            throw new ArgumentException("The selected time zone is not available.");
        }
        catch (InvalidTimeZoneException)
        {
            throw new ArgumentException("The selected time zone is invalid.");
        }

        var startLocal = start.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var endLocal = end.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(startLocal) || zone.IsInvalidTime(endLocal) ||
            zone.IsAmbiguousTime(startLocal) || zone.IsAmbiguousTime(endLocal))
            throw new ArgumentException("The date range has an ambiguous or invalid midnight in the selected time zone.");

        var rangeStart = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(startLocal, zone));
        var rangeEnd = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(endLocal, zone));
        var visibleEvents = await events.GetCalendarEventsAsync(userId, isAdministrator, rangeStart, rangeEnd, ct);

        var departmentNames = visibleEvents.Any(e => e.DepartmentId.HasValue)
            ? (await departments.GetAllAsync(ct)).ToDictionary(d => d.Id, d => d.Name)
            : new Dictionary<Guid, string>();

        var userNames = includeParticipants && visibleEvents.Any(e => e.CanManage && e.Participants.Count > 0)
            ? (await users.GetAllAsync(ct)).ToDictionary(u => u.Id, u => $"{u.FirstName} {u.LastName}")
            : new Dictionary<Guid, string>();

        var rows = visibleEvents.OrderBy(e => e.Start).Select(e => new ExportRow(
            e.Title, e.Description ?? string.Empty,
            TimeZoneInfo.ConvertTime(e.Start, zone).DateTime,
            TimeZoneInfo.ConvertTime(e.End, zone).DateTime,
            e.AllDay, e.DepartmentId is { } id
                ? (departmentNames.TryGetValue(id, out var departmentName) ? departmentName : "Unavailable department")
                : string.Empty,
            includeParticipants
                ? e.CanManage
                    ? string.Join(", ", e.Participants.Select(p =>
                        $"{(userNames.TryGetValue(p.UserId, out var name) ? name : "Unavailable account")} ({p.Role})"))
                    : "Available only to event organizers and administrators"
                : string.Empty)).ToArray();

        return format == "docx"
            ? CreateWord(rows, start, end, zone.Id, includeParticipants)
            : CreateExcel(rows, start, end, zone.Id, includeParticipants);
    }

    private static byte[] CreateWord(
        IReadOnlyList<ExportRow> rows, DateOnly start, DateOnly end, string zone, bool includeParticipants)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = document.AddMainDocumentPart();
            var body = new W.Body();
            main.Document = new W.Document(body);
            body.Append(Paragraph("Event report", true));
            body.Append(Paragraph($"{start:yyyy-MM-dd} to {end:yyyy-MM-dd} | Time zone: {zone}"));
            if (rows.Count == 0) body.Append(Paragraph("No events in this date range."));
            foreach (var row in rows)
            {
                body.Append(Paragraph(row.Title, true));
                body.Append(Paragraph($"Start: {row.Start:yyyy-MM-dd HH:mm} | End: {row.End:yyyy-MM-dd HH:mm} | All-day: {(row.AllDay ? "Yes" : "No")}"));
                if (!string.IsNullOrWhiteSpace(row.Department)) body.Append(Paragraph($"Department: {row.Department}"));
                if (!string.IsNullOrWhiteSpace(row.Description)) body.Append(Paragraph($"Description: {row.Description}"));
                if (includeParticipants) body.Append(Paragraph($"Participants: {(string.IsNullOrEmpty(row.Participants) ? "None" : row.Participants)}"));
            }
            main.Document.Save();
        }
        return stream.ToArray();
    }

    private static W.Paragraph Paragraph(string text, bool bold = false)
    {
        var run = new W.Run();
        if (bold) run.Append(new W.RunProperties(new W.Bold()));
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0) run.Append(new W.Break());
            run.Append(new W.Text(lines[i]) { Space = SpaceProcessingModeValues.Preserve });
        }
        return new W.Paragraph(run);
    }

    private static byte[] CreateExcel(
        IReadOnlyList<ExportRow> rows, DateOnly start, DateOnly end, string zone, bool includeParticipants)
    {
        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var workbook = document.AddWorkbookPart();
            workbook.Workbook = new S.Workbook();
            var styles = workbook.AddNewPart<WorkbookStylesPart>();
            styles.Stylesheet = new S.Stylesheet(
                new S.NumberingFormats(new S.NumberingFormat { NumberFormatId = 164, FormatCode = "yyyy-mm-dd hh:mm" }),
                new S.Fonts(new S.Font()),
                new S.Fills(new S.Fill(new S.PatternFill { PatternType = S.PatternValues.None }), new S.Fill(new S.PatternFill { PatternType = S.PatternValues.Gray125 })),
                new S.Borders(new S.Border()),
                new S.CellStyleFormats(new S.CellFormat()),
                new S.CellFormats(
                    new S.CellFormat(),
                    new S.CellFormat { NumberFormatId = 164, ApplyNumberFormat = true },
                    new S.CellFormat(new S.Alignment { WrapText = true }) { ApplyAlignment = true }));
            styles.Stylesheet.Save();
            var sheet = workbook.AddNewPart<WorksheetPart>();
            var data = new S.SheetData();
            sheet.Worksheet = new S.Worksheet(
                new S.Columns(
                    new S.Column { Min = 1, Max = 1, Width = 30, CustomWidth = true },
                    new S.Column { Min = 2, Max = 2, Width = 45, CustomWidth = true },
                    new S.Column { Min = 3, Max = 4, Width = 22, CustomWidth = true },
                    new S.Column { Min = 5, Max = 5, Width = 12, CustomWidth = true },
                    new S.Column { Min = 6, Max = 6, Width = 25, CustomWidth = true },
                    new S.Column { Min = 7, Max = 7, Width = 40, CustomWidth = true }),
                data);
            workbook.Workbook.Append(new S.Sheets(new S.Sheet
            {
                Id = workbook.GetIdOfPart(sheet),
                SheetId = 1,
                Name = "Events"
            }));

            data.Append(new S.Row(TextCell($"Event report: {start:yyyy-MM-dd} to {end:yyyy-MM-dd} | Time zone: {zone}")));
            var headers = new S.Row(TextCell("Title"), TextCell("Description"), TextCell("Start"), TextCell("End"),
                TextCell("All-day"), TextCell("Department"));
            if (includeParticipants) headers.Append(TextCell("Participants"));
            data.Append(headers);
            foreach (var row in rows)
            {
                var excelRow = new S.Row(TextCell(row.Title), TextCell(row.Description, wrap: true), DateCell(row.Start),
                    DateCell(row.End), TextCell(row.AllDay ? "Yes" : "No"), TextCell(row.Department));
                if (includeParticipants) excelRow.Append(TextCell(row.Participants, wrap: true));
                data.Append(excelRow);
            }
            sheet.Worksheet.Save();
            workbook.Workbook.Save();
        }
        return stream.ToArray();
    }

    private static S.Cell TextCell(string value, bool wrap = false) => new()
    {
        DataType = S.CellValues.InlineString,
        StyleIndex = wrap ? 2U : 0U,
        InlineString = new S.InlineString(new S.Text(value) { Space = SpaceProcessingModeValues.Preserve })
    };

    private static S.Cell DateCell(DateTime value) => new()
    {
        CellValue = new S.CellValue(value.ToOADate().ToString(CultureInfo.InvariantCulture)),
        DataType = S.CellValues.Number,
        StyleIndex = 1
    };

    private sealed record ExportRow(
        string Title, string Description, DateTime Start, DateTime End,
        bool AllDay, string Department, string Participants);
}
