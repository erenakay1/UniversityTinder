using Amazon.SQS.Model;
using AutoMapper;
using Google.Cloud.Vision.V1;
using Stripe;
using UniversityTinder.Models;
using UniversityTinder.Models.Dto;

namespace UniversityTinder
{
    public class MappingConfig
    {
        public static MapperConfiguration RegisterMaps()
        {
            var mappingConfig = new MapperConfiguration(config =>
            {
                // ========== APPLICATION USER MAPPINGS ==========

                // ApplicationUser <-> UsersDto
                config.CreateMap<ApplicationUser, UsersDto>()
                    .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
                    .ForMember(dest => dest.UserId, opt => opt.MapFrom(src => src.Id))
                    .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.FirstName))
                    .ForMember(dest => dest.Surname, opt => opt.MapFrom(src => src.LastName))
                    .ForMember(dest => dest.DisplayName, opt => opt.MapFrom(src => $"{src.FirstName} {src.LastName[0]}."))
                    .ForMember(dest => dest.Gender, opt => opt.MapFrom(src => src.Gender))
                    .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email))
                    .ForMember(dest => dest.PhoneNumber, opt => opt.MapFrom(src => src.PhoneNumber))
                    .ForMember(dest => dest.UniversityName, opt => opt.MapFrom(src => src.UniversityName))
                    .ForMember(dest => dest.IsVerified, opt => opt.MapFrom(src => src.IsUniversityVerified))
                    .ForMember(dest => dest.Age, opt => opt.MapFrom(src => CalculateAge(src.DateOfBirth)))
                    .ForMember(dest => dest.ProfileImageUrl, opt => opt.Ignore()) // Profile'dan gelecek
                    .ForMember(dest => dest.Roles, opt => opt.Ignore())
                    .ForMember(dest => dest.Lock_Unlock, opt => opt.Ignore())
                    .ForMember(dest => dest.HasUnreadMessages, opt => opt.Ignore())
                    .ForMember(dest => dest.IsActive, opt => opt.Ignore())
                    .ForMember(dest => dest.IsPassed, opt => opt.Ignore())
                    .ForMember(dest => dest.IsSuperLike, opt => opt.Ignore());

                config.CreateMap<UsersDto, ApplicationUser>()
                    .ForMember(dest => dest.FirstName, opt => opt.MapFrom(src => src.Name))
                    .ForMember(dest => dest.LastName, opt => opt.MapFrom(src => src.Surname))
                    .ForMember(dest => dest.Gender, opt => opt.MapFrom(src => src.Gender))
                    .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email))
                    .ForMember(dest => dest.PhoneNumber, opt => opt.MapFrom(src => src.PhoneNumber))
                    .ForMember(dest => dest.UniversityName, opt => opt.MapFrom(src => src.UniversityName))
                    .ForMember(dest => dest.IsUniversityVerified, opt => opt.MapFrom(src => src.IsVerified));

                // ApplicationUser <-> UserDto
                config.CreateMap<ApplicationUser, UserDto>()
                    .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
                    .ForMember(dest => dest.UserId, opt => opt.MapFrom(src => src.Id))
                    .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.FirstName))
                    .ForMember(dest => dest.Surname, opt => opt.MapFrom(src => src.LastName))
                    .ForMember(dest => dest.DisplayName, opt => opt.MapFrom(src => $"{src.FirstName} {src.LastName[0]}."))
                    .ForMember(dest => dest.Gender, opt => opt.MapFrom(src => src.Gender))
                    .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email))
                    .ForMember(dest => dest.PhoneNumber, opt => opt.MapFrom(src => src.PhoneNumber))
                    .ForMember(dest => dest.UniversityName, opt => opt.MapFrom(src => src.UniversityName))
                    .ForMember(dest => dest.IsVerified, opt => opt.MapFrom(src => src.IsUniversityVerified))
                    .ForMember(dest => dest.Age, opt => opt.MapFrom(src => CalculateAge(src.DateOfBirth)));

                // ========== USER PROFILE MAPPINGS ==========

                // UserProfile <-> ProfileDto (Detailed profile)
                config.CreateMap<UserProfile, ProfileDto>()
                    .ForMember(dest => dest.User, opt => opt.MapFrom(src => src.User))
                    .ForMember(dest => dest.PhotosList, opt => opt.MapFrom(src => src.PhotosList)) // ⭐ Photos mapping
                    .ReverseMap()
                    .ForMember(dest => dest.User, opt => opt.Ignore())
                    .ForMember(dest => dest.PhotosList, opt => opt.Ignore()); // ⭐ Reverse'te ignore

                // UserProfile <-> ProfileCreateDto
                config.CreateMap<ProfileCreateDto, UserProfile>()
                    .ForMember(dest => dest.ProfileId, opt => opt.Ignore())
                    .ForMember(dest => dest.UserId, opt => opt.Ignore())
                    .ForMember(dest => dest.User, opt => opt.Ignore())
                    .ForMember(dest => dest.MatchesList, opt => opt.Ignore())
                    .ForMember(dest => dest.LikedUsersList, opt => opt.Ignore())
                    .ForMember(dest => dest.ReceivedLikesList, opt => opt.Ignore())
                    .ForMember(dest => dest.PassedUsersList, opt => opt.Ignore())
                    .ForMember(dest => dest.BlockedUsersList, opt => opt.Ignore())
                    .ForMember(dest => dest.ReportedUsersList, opt => opt.Ignore())
                    .ForMember(dest => dest.PhotosList, opt => opt.Ignore())
                    .ForMember(dest => dest.ProfileCompletionScore, opt => opt.MapFrom(src => 0))
                    .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                    .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => DateTime.UtcNow));

                // UserProfile <-> ProfileUpdateDto
                config.CreateMap<ProfileUpdateDto, UserProfile>()
                    .ForMember(dest => dest.ProfileId, opt => opt.Ignore())
                    .ForMember(dest => dest.UserId, opt => opt.Ignore())
                    .ForMember(dest => dest.User, opt => opt.Ignore())
                    .ForMember(dest => dest.MatchesList, opt => opt.Ignore())
                    .ForMember(dest => dest.LikedUsersList, opt => opt.Ignore())
                    .ForMember(dest => dest.ReceivedLikesList, opt => opt.Ignore())
                    .ForMember(dest => dest.PassedUsersList, opt => opt.Ignore())
                    .ForMember(dest => dest.BlockedUsersList, opt => opt.Ignore())
                    .ForMember(dest => dest.ReportedUsersList, opt => opt.Ignore())
                    .ForMember(dest => dest.PhotosList, opt => opt.Ignore())
                    .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                    .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => DateTime.UtcNow));

                // ========== REGISTRATION & AUTH MAPPINGS ==========

                config.CreateMap<RegistrationRequestDTO, ApplicationUser>()
                    .ForMember(dest => dest.FirstName, opt => opt.MapFrom(src => src.FirstName))
                    .ForMember(dest => dest.LastName, opt => opt.MapFrom(src => src.LastName))
                    .ForMember(dest => dest.Gender, opt => opt.MapFrom(src => src.Gender))
                    .ForMember(dest => dest.DateOfBirth, opt => opt.MapFrom(src => src.DateOfBirth))
                    .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email))
                    .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.Email))
                    .ForMember(dest => dest.UniversityDomain, opt => opt.MapFrom(src => ExtractDomain(src.Email)))
                    .ForMember(dest => dest.IsUniversityVerified, opt => opt.MapFrom(src => false))
                    .ForMember(dest => dest.Id, opt => opt.Ignore())
                    .ForMember(dest => dest.EmailVerifiedAt, opt => opt.Ignore())
                    .ForMember(dest => dest.LastVerificationCheck, opt => opt.Ignore())
                    .ForMember(dest => dest.UniversityName, opt => opt.Ignore());

                // ========== PHOTO MAPPINGS ==========

                config.CreateMap<Photo, PhotoDto>()
                    .ForMember(dest => dest.PostImage, opt => opt.Ignore()) // IFormFile DTO'da var ama entity'de yok
                    .ReverseMap()
                    .ForMember(dest => dest.Profile, opt => opt.Ignore()); // ⭐ Navigation property ignore

                // ========== SWIPE & MATCH MAPPINGS ==========

                // SwipeRequestDto -> internal processing (if needed)


                // ========== MESSAGE MAPPINGS ==========





                // ========== REPORT MAPPINGS ==========



                // ========== BLOCK MAPPINGS ==========



                // ========== PREMIUM & SUBSCRIPTION MAPPINGS ==========


                // ========== BOOST MAPPINGS ==========



            });

            return mappingConfig;
        }

        // Helper methods
        private static int CalculateAge(DateTime dateOfBirth)
        {
            var today = DateTime.Today;
            var age = today.Year - dateOfBirth.Year;
            if (dateOfBirth.Date > today.AddYears(-age)) age--;
            return age;
        }

        private static string ExtractDomain(string email)
        {
            return email.Split('@').LastOrDefault() ?? string.Empty;
        }

        private static int CalculateRemainingMinutes(DateTime activatedAt, int durationMinutes)
        {
            var endTime = activatedAt.AddMinutes(durationMinutes);
            var remaining = (int)(endTime - DateTime.UtcNow).TotalMinutes;
            return remaining > 0 ? remaining : 0;
        }

        private static bool IsBoostActive(DateTime activatedAt, int durationMinutes)
        {
            var endTime = activatedAt.AddMinutes(durationMinutes);
            return DateTime.UtcNow < endTime;
        }
    }
}