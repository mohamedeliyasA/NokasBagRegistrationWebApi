using AutoMapper;
using Nokas.CashMgmt.BagRegistration.API.Models;

namespace Nokas.CashMgmt.BagRegistration.API.Profiles
{
    public class BagProfile : Profile
    {
        public BagProfile()
        {
            // DTO (ForCreation) → Model mappings
            CreateMap<BagRegistrationRequestForCreation, BagRegistrationRequest>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.BagRegistration != null ? src.BagRegistration.BagNumber : null))
                .ForMember(dest => dest.BagRegistration, opt => opt.MapFrom(src => src.BagRegistration))
                .ForMember(dest => dest.RegistrationStatus, opt => opt.MapFrom(_ => "In-Progress"))
                .ForMember(dest => dest.ClientId, opt => opt.Ignore())
                .ForMember(dest => dest.CorrelationId, opt => opt.Ignore());

            // Model → DTO (For returning to client)
            CreateMap<BagRegistrationRequest, BagRegistrationRequestForCreation>()
                .ForMember(dest => dest.BagRegistration, opt => opt.MapFrom(src => src.BagRegistration));

            // Internal model → Summary DTO
            CreateMap<BagRegistrationRequest, BagRegResponse>()
                .ForMember(dest => dest.customerRefNumber, opt => opt.MapFrom(src => src.BagRegistration.CustomerRefNumber))
                .ForMember(dest => dest.BagNumber, opt => opt.MapFrom(src => src.BagRegistration.BagNumber))
                .ForMember(dest => dest.RegistrationStatus, opt => opt.MapFrom(src => src.RegistrationStatus))
                //.ForMember(dest => dest.DownstreamSystemId, opt => opt.MapFrom(src => src.DownstreamSystemId))
                .ForMember(dest => dest.BagLifecycleId, opt => opt.MapFrom(src => src.BagLifecycleId))
                .ForMember(dest => dest.RequestCorrelationId, opt => opt.Ignore()) // set manually in controller
                .ForMember(dest => dest.Description, opt => opt.Ignore());         // set manually in controller
        }
    }
}
