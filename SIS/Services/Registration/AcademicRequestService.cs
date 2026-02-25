using SIS.Enums;
using SIS.Models.Registration;
using SIS.Repository;

namespace SIS.Services.Registration
{
    public class AcademicRequestService : IAcademicRequestService
    {
        private readonly IStudentRepository _studentRepository;
        private readonly IAcademicRequestRepository _academicRequestRepository;

        public AcademicRequestService(IStudentRepository studentRepository, IAcademicRequestRepository academicRequestRepository)
        {
            _studentRepository = studentRepository;
            _academicRequestRepository = academicRequestRepository;
        }
        
        public async Task<string> SubmitRequestAsync(int studentId, string requestType, string description, int programmeId, int schoolId)
        {
            var student = await _studentRepository.GetByIdAsync(studentId);

            if (student == null)
            {
                return "Student not found.";
            }

            AcademicRequest academicRequest = null;

            if(requestType.Equals("Programme Change"))
            {
                if(schoolId <= 0 && programmeId <= 0)
                { 
                    return "Please select a new school and/or programme.";
                }
                else
                {
                    academicRequest = new AcademicRequest
                    {
                        StudentId = studentId,
                        RequestType = requestType,
                        Description = description,
                        Status = Status.Pending,
                        RequestDate = DateTime.Now,
                        ProgrammeId = programmeId,
                        SchoolId = schoolId,
                        CreatedAt = DateTime.Now,
                        CreatedBy = student.FullName
                    };
                }
               
            }
            else
            {
                academicRequest = new AcademicRequest
                {
                    StudentId = studentId,
                    RequestType = requestType,
                    Description = description,
                    Status = Status.Pending,
                    RequestDate = DateTime.Now,
                    CreatedAt = DateTime.Now,
                    CreatedBy = student.FullName,

                    // insert the already existing fields
                    ProgrammeId = student.ProgrammeId,
                    SchoolId = student.SchoolId
                };
            }


            if (academicRequest == null)
            {
                return "Invalid request type.";
            }

            // Add the academic request (No assignment to a variable needed)
            await _academicRequestRepository.AddAsync(academicRequest);

            return $"Request for {requestType} has been successfully submitted.";
        }

        public async Task<string> GetRequestStatusAsync(int requestId)
        {
            var request = await _academicRequestRepository.GetByIdAsync(requestId);

            if (request == null)
            {
                return "Request not found.";
            }

            return $"Request for {request.RequestType} is {request.Status}.";
        }
    }


}
