namespace SIS.Models.StudentAccommodation
{
    public class HostelManagerDashboardViewModel
    {
        public List<Hostel> ManagedHostels { get; set; }
        public int TotalBeds { get; set; }
        public int OccupiedBeds { get; set; }
        public int AvailableBeds { get; set; }
        public int MaintenanceBeds { get; set; }
        public int ReservedBeds { get; set; }
        public List<MaintenanceRequest> MaintenanceRequests { get; set; }
        public int PendingApplications { get; set; }
        public List<CheckInOut> RecentCheckIns { get; set; }
        public int ResourcesNeedingRepair { get; set; }
        public object RoomStatusData { get; set; }
    }
}
