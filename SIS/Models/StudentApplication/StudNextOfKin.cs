using System.ComponentModel.DataAnnotations;

namespace SIS.Models.StudentApplication
{
    public class StudNextOfKin
    {
        [Key]
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Relationship { get; set; }
        public string Email { get; set; }
        public required string PhoneNumber { get; set; }
        public required string Address { get; set; }
    }
}
