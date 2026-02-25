namespace SIS.Models.Configuration
{
    public class InstitutionConfig
    {
        public string Name { get; set; }
        public string ShortName { get; set; }
        public string LogoPath { get; set; }
        public string Subdomain { get; set; }
        public EmailSettings EmailSettings { get; set; }
        public ContactInfo ContactInfo { get; set; }
        public BrandColors BrandColors { get; set; }
    }

    public class ContactInfo
    {
        public string Address { get; set; }
        public string Phone { get; set; }
        public string Website { get; set; }
    }

    public class BrandColors
    {
        public string Primary { get; set; }
        public string Secondary { get; set; }
        public string Accent { get; set; }
    }

    public class EmailSettings
    {
        public string SmtpServer { get; set; }
        public int Port { get; set; }
        public string SenderEmail { get; set; }
        public string SenderPassword { get; set; }
        public string SenderName { get; set; }
        public bool EnableSsl { get; set; }
        public bool UseDefaultCredentials { get; set; }
    }
}
