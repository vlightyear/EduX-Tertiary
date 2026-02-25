using SIS.Models.Zoom;
using SIS.Enums;

namespace SIS.Services.Zoom
{
    public interface IZoomService
    {
        Task<ZoomMeeting> CreateMeetingAsync(string topic, DateTime startTime, int durationMinutes, string agenda, int courseId, string hostId);
        Task<ZoomMeeting> GetMeetingAsync(long meetingId);
        Task<ZoomMeeting> GetMeetingByZoomIdAsync(string zoomMeetingId);
        Task<List<ZoomMeeting>> GetUpcomingMeetingsAsync(int courseId);
        Task<List<ZoomMeeting>> GetActiveMeetingsAsync(int courseId);
        Task<List<ZoomMeeting>> GetMeetingsForLecturerAsync(string lecturerId);
        Task<List<ZoomMeeting>> GetMeetingsForStudentAsync(string studentId);
        Task<bool> UpdateMeetingStatusAsync(int meetingId, Status status);
        Task<bool> DeleteMeetingAsync(int meetingId);
    }
}
