using System.Security.Claims;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using ESEvents.Application.Departments;
using ESEvents.Application.Events;
using ESEvents.Application.Users;
using ESEvents.Domain.Events;
using ESEvents.Domain.Users;
using ESEvents.Web.Controllers;
using ESEvents.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using S = DocumentFormat.OpenXml.Spreadsheet;

namespace ESEvents.Web.Tests;

public sealed class EventExportTests
{
    [Theory]
    [InlineData("docx", true)]
    [InlineData("xlsx", true)]
    [InlineData("docx", false)]
    [InlineData("xlsx", false)]
    public async Task Export_CreatesValidFilesWithRequestedData(string format, bool includeParticipants)
    {
        var userId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();
        var eventService = new Mock<IEventService>();
        var departmentService = new Mock<IDepartmentService>();
        var userService = new Mock<IUserService>();
        var calendarEvent = new CalendarEventDto(Guid.NewGuid(), "=Team meeting", "Agenda\nNext steps",
            DateTimeOffset.Parse("2026-03-08T16:00:00Z"), DateTimeOffset.Parse("2026-03-08T17:00:00Z"),
            false, departmentId, EventRole.Organizer, true, [new EventParticipantDto(userId, EventRole.Organizer)]);
        eventService.Setup(s => s.GetCalendarEventsAsync(userId, false,
                DateTimeOffset.Parse("2026-03-08T07:00:00Z"),
                DateTimeOffset.Parse("2026-03-09T06:00:00Z"), It.IsAny<CancellationToken>()))
            .ReturnsAsync([calendarEvent]);
        departmentService.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new DepartmentDto(departmentId, "Operations", null, 0)]);
        userService.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new UserDto(userId, "user@example.test", "Alex", "Example", null, null, SystemRole.User, UserStatus.Active)]);
        var service = new EventExportService(eventService.Object, departmentService.Object, userService.Object);

        var bytes = await service.ExportAsync(userId, false, new DateOnly(2026, 3, 8), new DateOnly(2026, 3, 8),
            "America/Denver", includeParticipants, format);

        using var stream = new MemoryStream(bytes);
        if (format == "docx")
        {
            using var document = WordprocessingDocument.Open(stream, false);
            Assert.Empty(new OpenXmlValidator().Validate(document));
            var text = document.MainDocumentPart!.Document.InnerText;
            Assert.Contains("=Team meeting", text);
            Assert.Contains("2026-03-08 10:00", text);
            Assert.Contains("Operations", text);
            Assert.NotEmpty(document.MainDocumentPart.Document.Descendants<DocumentFormat.OpenXml.Wordprocessing.Break>());
            Assert.Equal(includeParticipants, text.Contains("Alex Example (Organizer)"));
        }
        else
        {
            using var document = SpreadsheetDocument.Open(stream, false);
            Assert.Empty(new OpenXmlValidator().Validate(document));
            var sheet = document.WorkbookPart!.WorksheetParts.Single().Worksheet;
            var rows = sheet.Descendants<S.Row>().ToArray();
            var cells = rows[2].Elements<S.Cell>().ToArray();
            Assert.Equal("=Team meeting", cells[0].InnerText);
            Assert.Equal(S.CellValues.InlineString, cells[0].DataType!.Value);
            Assert.Null(cells[0].CellFormula);
            Assert.Equal("Agenda\nNext steps", cells[1].InnerText);
            Assert.Equal(2U, cells[1].StyleIndex!.Value);
            Assert.Equal(new DateTime(2026, 3, 8, 10, 0, 0).ToOADate(),
                double.Parse(cells[2].CellValue!.Text, System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal("Operations", cells[5].InnerText);
            Assert.Equal(includeParticipants ? 7 : 6, cells.Length);
            if (includeParticipants) Assert.Contains("Alex Example (Organizer)", cells[6].InnerText);
        }
        eventService.VerifyAll();
        userService.Verify(s => s.GetAllAsync(It.IsAny<CancellationToken>()), includeParticipants ? Times.Once : Times.Never);
    }

    [Fact]
    public async Task Export_RejectsInvalidRangeBeforeLoadingEvents()
    {
        var events = new Mock<IEventService>();
        var service = new EventExportService(events.Object, Mock.Of<IDepartmentService>(), Mock.Of<IUserService>());
        await Assert.ThrowsAsync<ArgumentException>(() => service.ExportAsync(Guid.NewGuid(), false,
            new DateOnly(2026, 3, 9), new DateOnly(2026, 3, 8), "UTC", false, "xlsx"));
        events.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("invalid-zone", "docx")]
    [InlineData("UTC", "pdf")]
    public async Task Export_RejectsInvalidOptions(string zone, string format)
    {
        var events = new Mock<IEventService>();
        var service = new EventExportService(events.Object, Mock.Of<IDepartmentService>(), Mock.Of<IUserService>());
        await Assert.ThrowsAsync<ArgumentException>(() => service.ExportAsync(Guid.NewGuid(), false,
            new DateOnly(2026, 3, 8), new DateOnly(2026, 3, 8), zone, false, format));
        events.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("docx")]
    [InlineData("xlsx")]
    public async Task Export_ProducesValidEmptyReport(string format)
    {
        var events = new Mock<IEventService>();
        events.Setup(s => s.GetCalendarEventsAsync(It.IsAny<Guid>(), false, It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var service = new EventExportService(events.Object, Mock.Of<IDepartmentService>(), Mock.Of<IUserService>());
        var bytes = await service.ExportAsync(Guid.NewGuid(), false, new DateOnly(2026, 3, 8),
            new DateOnly(2026, 3, 8), "UTC", false, format);
        using var stream = new MemoryStream(bytes);
        if (format == "docx")
        {
            using var document = WordprocessingDocument.Open(stream, false);
            Assert.Empty(new OpenXmlValidator().Validate(document));
            Assert.Contains("No events in this date range.", document.MainDocumentPart!.Document.InnerText);
        }
        else
        {
            using var document = SpreadsheetDocument.Open(stream, false);
            Assert.Empty(new OpenXmlValidator().Validate(document));
            Assert.Equal(2, document.WorkbookPart!.WorksheetParts.Single().Worksheet.Descendants<S.Row>().Count());
        }
    }

    [Fact]
    public async Task Export_DoesNotDiscloseParticipantsForEventsTheUserCannotManage()
    {
        var events = new Mock<IEventService>();
        var users = new Mock<IUserService>();
        var userId = Guid.NewGuid();
        events.Setup(s => s.GetCalendarEventsAsync(userId, false, It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CalendarEventDto(Guid.NewGuid(), "Visible event", null,
                DateTimeOffset.Parse("2026-03-08T16:00:00Z"), DateTimeOffset.Parse("2026-03-08T17:00:00Z"),
                false, null, EventRole.Member, false, [new EventParticipantDto(Guid.NewGuid(), EventRole.Organizer)])]);
        var service = new EventExportService(events.Object, Mock.Of<IDepartmentService>(), users.Object);

        var bytes = await service.ExportAsync(userId, false, new DateOnly(2026, 3, 8),
            new DateOnly(2026, 3, 8), "UTC", true, "docx");

        using var document = WordprocessingDocument.Open(new MemoryStream(bytes), false);
        Assert.Contains("Available only to event organizers and administrators", document.MainDocumentPart!.Document.InnerText);
        users.Verify(s => s.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Download_UsesAuthenticatedUserAndReturnsDownload()
    {
        var userId = Guid.NewGuid();
        var service = new Mock<IEventExportService>();
        var start = new DateOnly(2026, 3, 1);
        var end = new DateOnly(2026, 3, 31);
        service.Setup(s => s.ExportAsync(userId, true, start, end, "UTC", true, "docx", It.IsAny<CancellationToken>()))
            .ReturnsAsync([1, 2, 3]);
        var controller = new EventExportsController(service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim("system_role", "Administrator")], "test"))
                }
            }
        };

        var result = Assert.IsType<FileContentResult>(await controller.Download("docx", start, end, "UTC", true, CancellationToken.None));
        Assert.Equal("events-2026-03-01-to-2026-03-31.docx", result.FileDownloadName);
        Assert.Equal("no-store", controller.Response.Headers.CacheControl.ToString());
        service.VerifyAll();
    }

    [Fact]
    public async Task Download_RejectsMissingUserId()
    {
        var service = new Mock<IEventExportService>();
        var controller = new EventExportsController(service.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        var result = await controller.Download("xlsx", new DateOnly(2026, 3, 1),
            new DateOnly(2026, 3, 31), "UTC", false, CancellationToken.None);
        Assert.IsType<UnauthorizedResult>(result);
        service.VerifyNoOtherCalls();
    }
}
