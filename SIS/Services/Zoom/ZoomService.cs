using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SIS.Data;
using SIS.Models.Zoom;
using SIS.Services.Zoom;
using SIS.Data;
using SIS.Models;

namespace SIS.Services.Zoom

{
    public class ZoomService : IZoomService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ZoomService> _logger;
        private readonly ZoomOptions _options;
        private readonly HttpClient _httpClient;
        private string _accessToken;
        private DateTime _tokenExpiry;
        public ZoomService(
            ApplicationDbContext context,
            ILogger<ZoomService> logger,
            IOptions<ZoomOptions> options,
            IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _logger = logger;
            _options = options.Value;
            _httpClient = httpClientFactory.CreateClient();
            _httpClient.BaseAddress = new Uri("https://api.zoom.us/v2/");
        }

        private async Task EnsureTokenAsync()
        {
            if (string.IsNullOrEmpty(_accessToken) || DateTime.Now >= _tokenExpiry)
            {
                await GetNewAccessTokenAsync();
            }
        }

        private async Task GetNewAccessTokenAsync()
        {
            try
            {
                var tokenClient = new HttpClient();
                var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "https://zoom.us/oauth/token")
                {
                    Content = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        ["grant_type"] = "account_credentials",
                        ["account_id"] = _options.AccountId
                    })
                };

                var credentials = Convert.ToBase64String(
                    Encoding.ASCII.GetBytes($"{_options.ClientId}:{_options.ClientSecret}"));
                tokenRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

                var response = await tokenClient.SendAsync(tokenRequest);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var tokenResponse = JsonSerializer.Deserialize<JsonElement>(content);

                _accessToken = tokenResponse.GetProperty("access_token").GetString();
                int expiresIn = tokenResponse.GetProperty("expires_in").GetInt32();

                // Set expiry time with a buffer (5 minutes before actual expiry)
                _tokenExpiry = DateTime.Now.AddSeconds(expiresIn - 300);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Zoom access token");
                throw;
            }
        }

        public async Task<ZoomMeeting> CreateMeetingAsync(string topic, DateTime startTime, int durationMinutes, string agenda, int courseId, string hostId)
        {
            await EnsureTokenAsync();

            try
            {
                // Get the current user's Zoom user ID (or use a specific ID)
                var userId = await GetCurrentUserIdAsync();

                var meetingRequest = new
                {
                    topic = topic,
                    type = 2, // Scheduled meeting
                    start_time = startTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                    duration = durationMinutes,
                    timezone = "UTC",
                    agenda = agenda,
                    settings = new
                    {
                        host_video = true,
                        participant_video = true,
                        join_before_host = false,
                        mute_upon_entry = true,
                        waiting_room = true
                    }
                };

                var requestContent = new StringContent(
                    JsonSerializer.Serialize(meetingRequest),
                    Encoding.UTF8,
                    "application/json");

                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _accessToken);

                var response = await _httpClient.PostAsync($"users/{userId}/meetings", requestContent);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var meetingResponse = JsonSerializer.Deserialize<JsonElement>(content);

                // Create a new ZoomMeeting entity
                var meeting = new ZoomMeeting
                {
                    Topic = topic,
                    StartTime = startTime,
                    Duration = durationMinutes,
                    ZoomMeetingId = meetingResponse.GetProperty("id").ToString(), // Changed from GetString() to ToString()
                    JoinUrl = meetingResponse.GetProperty("join_url").GetString(),
                    StartUrl = meetingResponse.GetProperty("start_url").GetString(),
                    Password = meetingResponse.GetProperty("password").GetString(),
                    Agenda = agenda,
                    CourseId = courseId,
                    CreatedById = hostId,
                    Status = SIS.Enums.Status.Scheduled,
                    CreatedAt = DateTime.Now
                };

                _context.ZoomMeetings.Add(meeting);
                await _context.SaveChangesAsync();

                return meeting;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Zoom meeting");
                throw;
            }
        }

        private async Task<string> GetCurrentUserIdAsync()
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _accessToken);

            var response = await _httpClient.GetAsync("users/me");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var userInfo = JsonSerializer.Deserialize<JsonElement>(content);

            return userInfo.GetProperty("id").GetString();
        }

        public async Task<ZoomMeeting> GetMeetingAsync(long meetingId)
        {
            return await _context.ZoomMeetings
                .Include(m => m.Course)
                .Include(m => m.CreatedBy)
                .FirstOrDefaultAsync(m => m.Id == meetingId);
        }

        public async Task<ZoomMeeting> GetMeetingByZoomIdAsync(string zoomMeetingId)
        {
            return await _context.ZoomMeetings
                .Include(m => m.Course)
                .Include(m => m.CreatedBy)
                .FirstOrDefaultAsync(m => m.ZoomMeetingId == zoomMeetingId);
        }

        public async Task<List<ZoomMeeting>> GetUpcomingMeetingsAsync(int courseId)
        {
            return await _context.ZoomMeetings
                .Where(m => m.CourseId == courseId &&
                       m.StartTime > DateTime.Now &&
                       m.Status == SIS.Enums.Status.Scheduled)
                .OrderBy(m => m.StartTime)
                .ToListAsync();
        }

        public async Task<List<ZoomMeeting>> GetActiveMeetingsAsync(int courseId)
        {
            return await _context.ZoomMeetings
                .Where(m => m.CourseId == courseId &&
                       m.Status == SIS.Enums.Status.Active)
                .OrderBy(m => m.StartTime)
                .ToListAsync();
        }

        public async Task<List<ZoomMeeting>> GetMeetingsForLecturerAsync(string lecturerId)
        {
            return await _context.ZoomMeetings
                .Include(m => m.Course)  // Include the Course navigation property
                .Include(m => m.CreatedBy)  // Include the CreatedBy user if you need it
                .Where(m => m.CreatedById == lecturerId)
                .OrderByDescending(m => m.StartTime)
                .ToListAsync();
        }

        public async Task<List<ZoomMeeting>> GetMeetingsForStudentAsync(string username)
        {
            // First, find the student based on the username
            var student = await _context.Students
                .FirstOrDefaultAsync(s => s.Username == username);

            if (student == null)
            {
                _logger.LogWarning($"No student found with username: {username}");
                return new List<ZoomMeeting>();
            }

            // Use student.Id directly, which is 1048 in this case
            var studentCourses = await _context.StudentCourseRegistrations
                .Where(scr => scr.StudentId == student.Id)  // This is correct now
                .Select(scr => scr.CourseId)
                .ToListAsync();

            // Get meetings for those courses
            return await _context.ZoomMeetings
                .Include(m => m.Course)
                .Include(m => m.CreatedBy)
                .Where(m => studentCourses.Contains(m.CourseId))
                .OrderByDescending(m => m.StartTime)
                .ToListAsync();
        }

        public async Task<bool> UpdateMeetingStatusAsync(int meetingId, Enums.Status status)
        {
            var meeting = await _context.ZoomMeetings.FindAsync(meetingId);
            if (meeting == null)
                return false;

            meeting.Status = status;

            if (status == Enums.Status.Completed)
                meeting.EndedAt = DateTime.Now;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteMeetingAsync(int meetingId)
        {
            var meeting = await _context.ZoomMeetings.FindAsync(meetingId);
            if (meeting == null)
                return false;

            // Optionally delete from Zoom API as well
            await EnsureTokenAsync();

            try
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _accessToken);

                var response = await _httpClient.DeleteAsync($"meetings/{meeting.ZoomMeetingId}");
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete meeting from Zoom API, but will continue to delete from database");
            }

            _context.ZoomMeetings.Remove(meeting);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}