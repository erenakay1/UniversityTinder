using UniversityTinder.Models.Dto;

public class PaginatedProfilesDto
{
    public List<ProfileCardDto> Profiles { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalProfiles { get; set; }
    public int TotalPages { get; set; }
    public bool HasNextPage { get; set; }
}