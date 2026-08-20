using AutoMapper;
using R2WAI.Application.Features.Members.DTOs;

namespace R2WAI.Application.Features.Members.Mappings;

public class MembersProfile : Profile
{
    public MembersProfile()
    {
        CreateMap<MemberWallet, MemberWalletDto>();

        CreateMap<PointsTransaction, PointsTransactionDto>();

        CreateMap<MemberEvent, MemberEventDto>()
            .ForMember(d => d.AttendanceCount, o => o.MapFrom(s => s.Attendances.Count));

        CreateMap<WithdrawalRequest, WithdrawalRequestDto>()
            .ForMember(d => d.MemberName, o => o.MapFrom(s => s.User != null ? $"{s.User.FirstName} {s.User.LastName}".Trim() : string.Empty));

        CreateMap<PlanUpgradeRequest, PlanUpgradeRequestDto>()
            .ForMember(d => d.MemberName, o => o.MapFrom(s => s.User != null ? $"{s.User.FirstName} {s.User.LastName}".Trim() : string.Empty));
    }
}
