using AutoMapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SDDev.Net.GenericRepository.Tests.TestModels
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<BaseTestObject, BaseTestIndexModel>();
        }
    }
}
