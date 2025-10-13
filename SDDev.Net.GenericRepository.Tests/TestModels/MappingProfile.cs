using AutoMapper;

namespace SDDev.Net.GenericRepository.Tests.TestModels
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<BaseTestObject, BaseTestIndexModel>()
                .ForMember(x => x.Id, x => x.MapFrom(y => y.Id.Value));
        }
    }
}
