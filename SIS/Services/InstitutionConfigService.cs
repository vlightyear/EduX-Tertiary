using SIS.Models.Configuration;

namespace SIS.Services
{
    public interface IInstitutionConfigService
    {
        InstitutionConfig GetCurrentInstitution();
        EmailSettings GetEmailSettings();
        string GetInstitutionName();
        string GetLogoPath();
    }

    public class InstitutionConfigService : IInstitutionConfigService
    {
        private readonly IConfiguration _configuration;
        private readonly InstitutionConfig _currentInstitution;

        public InstitutionConfigService(IConfiguration configuration)
        {
            _configuration = configuration;

            var activeInstitution = _configuration["ActiveInstitution"];
            _currentInstitution = _configuration
                .GetSection($"Institutions:{activeInstitution}")
                .Get<InstitutionConfig>();
        }

        public InstitutionConfig GetCurrentInstitution() => _currentInstitution;
        public EmailSettings GetEmailSettings() => _currentInstitution.EmailSettings;
        public string GetInstitutionName() => _currentInstitution.Name;
        public string GetLogoPath() => _currentInstitution.LogoPath;
    }
}
