namespace R2WAI.Application.Features.Members.Queries;

public record GetEventsQuery : IRequest<List<MemberEventDto>>
{
    public bool IncludeInactive { get; init; }
}

public class GetEventsQueryHandler(
    IRepository<MemberEvent> eventRepo,
    IRepository<EventAttendance> attendanceRepo,
    ICurrentUserService currentUser) : IRequestHandler<GetEventsQuery, List<MemberEventDto>>
{
    public async Task<List<MemberEventDto>> Handle(GetEventsQuery query, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var events = await eventRepo.FindAsync(
            e => e.TenantId == tenantId && (query.IncludeInactive || e.IsActive),
            cancellationToken);
        var ordered = events.OrderByDescending(e => e.EventDate).ToList();

        var eventIds = ordered.Select(e => e.Id).ToList();
        var attendances = await attendanceRepo.FindAsync(a => eventIds.Contains(a.EventId), cancellationToken);
        var attendanceCounts = attendances
            .GroupBy(a => a.EventId)
            .ToDictionary(g => g.Key, g => g.Count());

        return ordered.Select(e => new MemberEventDto
        {
            Id = e.Id,
            Name = e.Name,
            Description = e.Description,
            PointsValue = e.PointsValue,
            EventDate = e.EventDate,
            IsActive = e.IsActive,
            AttendanceCount = attendanceCounts.GetValueOrDefault(e.Id, 0),
            CreatedAt = e.CreatedAt,
        }).ToList();
    }
}
