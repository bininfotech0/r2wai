using AutoMapper;
using R2WAI.Application.Features.TestCases.DTOs;

namespace R2WAI.Application.Features.TestCases.Mappings;

public class TestCaseProfile : Profile
{
    public TestCaseProfile()
    {
        CreateMap<TestCase, TestCaseDto>();
        CreateMap<TestCaseResult, TestCaseResultDto>()
            .ForMember(d => d.Status, opt => opt.MapFrom(s => s.Status.ToString()));
        CreateMap<TestRun, TestRunDto>()
            .ForMember(d => d.Status, opt => opt.MapFrom(s => s.Status.ToString()));
    }
}
