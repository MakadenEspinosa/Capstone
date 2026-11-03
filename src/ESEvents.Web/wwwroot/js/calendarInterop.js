window.eseventsCalendar = (() => {
    let calendar;

    async function persistMovedEvent(info, dotNetReference) {
        const allDayEnd = new Date(info.event.start);
        allDayEnd.setDate(allDayEnd.getDate() + 1);
        const start = info.event.start?.toISOString();
        const end = info.event.end?.toISOString() ||
            (info.event.allDay
                ? allDayEnd.toISOString()
                : new Date(info.event.start.getTime() + 3600000).toISOString());

        try {
            const saved = await dotNetReference.invokeMethodAsync(
                "UpdateEventTime",
                info.event.id,
                start,
                end,
                info.event.allDay);
            if (!saved) info.revert();
        } catch {
            info.revert();
        }
    }

    return {
        init(element, dotNetReference) {
            calendar = new FullCalendar.Calendar(element, {
                initialView: "dayGridMonth",
                timeZone: "local",
                height: "auto",
                nowIndicator: true,
                selectable: true,
                editable: true,
                eventDurationEditable: true,
                headerToolbar: {
                    left: "prev,next today",
                    center: "title",
                    right: "dayGridMonth,timeGridWeek,timeGridDay"
                },
                events: async (fetchInfo, successCallback, failureCallback) => {
                    try {
                        const events = await dotNetReference.invokeMethodAsync(
                            "LoadEvents",
                            fetchInfo.startStr,
                            fetchInfo.endStr);
                        successCallback(events.map(event => ({
                            id: event.id,
                            title: event.title,
                            start: event.start,
                            end: event.end,
                            allDay: event.allDay,
                            editable: event.canManage,
                            extendedProps: {
                                canManage: event.canManage,
                                departmentId: event.departmentId,
                                currentUserRole: event.currentUserRole
                            }
                        })));
                    } catch (error) {
                        failureCallback(error);
                    }
                },
                dateClick: async info => {
                    const end = new Date(info.date);
                    if (info.allDay) end.setDate(end.getDate() + 1);
                    else end.setHours(end.getHours() + 1);
                    await dotNetReference.invokeMethodAsync(
                        "CreateEventAt",
                        info.date.toISOString(),
                        end.toISOString(),
                        info.allDay);
                },
                eventClick: async info => {
                    await dotNetReference.invokeMethodAsync("SelectEvent", info.event.id);
                },
                eventDrop: info => persistMovedEvent(info, dotNetReference),
                eventResize: info => persistMovedEvent(info, dotNetReference)
            });
            calendar.render();
        },
        refresh() {
            calendar?.refetchEvents();
        },
        destroy() {
            calendar?.destroy();
            calendar = undefined;
        },
        toLocalDateTime(isoValue) {
            const date = new Date(isoValue);
            const pad = value => String(value).padStart(2, "0");
            return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
        },
        toUtcIso(localValue) {
            if (!localValue) throw new Error("Enter a valid event date and time.");
            const date = new Date(localValue);
            if (Number.isNaN(date.getTime())) throw new Error("Enter a valid event date and time.");
            return date.toISOString();
        }
    };
})();
