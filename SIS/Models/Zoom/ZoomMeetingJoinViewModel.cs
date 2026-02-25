namespace SIS.Models.Zoom
{
    public class ZoomMeetingJoinViewModel
    {
        public int MeetingId { get; set; }
        public string ZoomMeetingNumber { get; set; } = "";         
        public string MeetingPassword { get; set; } = "";
        public string MeetingTopic { get; set; } = "";
        public string UserName { get; set; } = "";
        public int Role { get; set; }  // 0 for attendee, 1 for host
       // public string ZoomWebConfig { get; set; }
        public string Signature { get;  set; } = "";
        public string SdkKey { get; set; } = "";
    }
}
