namespace SIS.Models
{
    public abstract class AuditClass
    {
        public required string CreatedBy { get; set; }
        public required DateTime CreatedAt { get; set; } = DateTime.Now.AddHours(2);
        public string? UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}