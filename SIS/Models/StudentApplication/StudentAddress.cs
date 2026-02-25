using System.ComponentModel.DataAnnotations;

namespace SIS.Models.StudentApplication
{
    public class StudentAddress
    {
        [Key]
        public int Id { get; set; }
        // Address properties
        public required string AddressLine1 { get; set; }
        public string? AddressLine2 { get; set; }
        public required string City { get; set; }
        public required string State { get; set; }
        public required string PostalCode { get; set; }
        public required string Country { get; set; }
    }
}
