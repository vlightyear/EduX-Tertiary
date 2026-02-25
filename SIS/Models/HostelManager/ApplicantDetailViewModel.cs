using SIS.Enums;

namespace SIS.Models.HostelManager
{
    public class ApplicantDetailViewModel
    {
        public int ApplicationId { get; set; }
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public string MatricNumber { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Gender { get; set; }
        public DateTime DateOfBirth { get; set; }
        public string Nationality { get; set; }
        public bool IsInternational { get; set; }

        // Academic information
        public string Programme { get; set; }
        public string School { get; set; }
        public string ModeOfStudy { get; set; }
        public string ProgrammeLevel { get; set; }
        public int? YearOfStudy { get; set; }

        // Address information
        public string Address { get; set; }
        public string City { get; set; }
        public string Country { get; set; }

        // Next of kin information
        public string NextOfKinName { get; set; }
        public string NextOfKinPhone { get; set; }
        public string NextOfKinRelationship { get; set; }

        // Application information
        public DateTime ApplicationDate { get; set; }
        public Status ApplicationStatus { get; set; }
        public string Notes { get; set; }

        // Allocation information
        public bool HasAllocation { get; set; }
        public AllocationDetailsViewModel AllocationDetails { get; set; }
    }

    public class AllocateRoomViewModel
    {
        public int ApplicationId { get; set; }
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public string MatricNumber { get; set; }
        public string Gender { get; set; }

        // Hostels available for allocation
        public List<HostelViewModel> Hostels { get; set; } = new List<HostelViewModel>();

        // Allocation details
        public List<string> AllocationTypes { get; set; } = new List<string>();
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IsGraduationBased { get; set; }
    }

    public class HostelViewModel
    {
        public int HostelId { get; set; }
        public string HostelName { get; set; }
        public int TotalRooms { get; set; }
        public int TotalCapacity { get; set; }
        public int AvailableRooms { get; set; }
    }

    public class AllocationDetailsViewModel
    {
        public int AllocationId { get; set; }
        public int ApplicationId { get; set; }
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public string MatricNumber { get; set; }

        // Allocation information
        public string AllocationType { get; set; }
        public DateTime AllocationDate { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IsGraduationBased { get; set; }
        public Status Status { get; set; }

        // Room information
        public string HostelName { get; set; }
        public string RoomNumber { get; set; }
        public int Floor { get; set; }
        public string RoomType { get; set; }
        public string BedIdentifier { get; set; }

        // Check-in/out information
        public bool IsCheckedIn { get; set; }
        public DateTime? CheckInDate { get; set; }
        public bool IsCheckedOut { get; set; }
        public DateTime? CheckOutDate { get; set; }
    }

    public class BulkAllocationViewModel
    {
        public int PeriodId { get; set; }
        public string AllocationType { get; set; } // "FCFS" or "Random"
        public string StatusFilter { get; set; }
        public string GenderFilter { get; set; }
        public string StudentTypeFilter { get; set; } // "International" or "Local"
        public int? YearOfStudyFilter { get; set; }
        public int? MaxAllocations { get; set; }
    }
}
