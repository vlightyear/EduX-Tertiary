using SIS.Models.StudentApplication;

namespace SIS.Services.PhotoValidation
{
    public interface IPhotoValidationService
    {
        Task<PhotoValidationResult> ValidatePassportPhotoAsync(IFormFile photo);
    }
}