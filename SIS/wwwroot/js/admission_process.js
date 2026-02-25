$(document).ready(function () {
    // Embed the serialized JSON into JavaScript using Html.Raw and ensure proper escaping
    const applicants = window.applicantsData;


    // Event handlers for buttons
    $(document).on("click", ".view-details", function () {
        let applicantId = $(this).data('applicant');

        // Ensure applicants references are resolved
        function resolveReferences(obj) {
            const refs = {};
            const resolve = o => {
                if (Array.isArray(o)) {
                    return o.map(resolve);
                } else if (o && typeof o === "object") {
                    if (o["$ref"]) {
                        return refs[o["$ref"]]; // Resolve reference
                    }
                    if (o["$id"]) {
                        refs[o["$id"]] = o;
                    }
                    return Object.fromEntries(Object.entries(o).map(([k, v]) => [k, resolve(v)]));
                }
                return o;
            };
            return resolve(obj);
        }

        // Resolve the references in the applicants data
        const resolvedApplicants = resolveReferences(applicants);

        // Now, we can safely find the applicant based on applicantId
        const applicant = resolvedApplicants.$values.find((appl) => appl.ApplicantId == applicantId);

        if (!applicant) {
            console.error("Applicant not found");
            return; // Exit if applicant is not found
        }

        // Populate modal fields with applicant data
        $("#nrcOrPassport").text(applicant.NrcOrPassport);
        $("#fullName").text(applicant.FullName);
        $("#dob").text(applicant.DateOfBirth ? new Date(applicant.DateOfBirth).toLocaleDateString() : "N/A");
        $("#gender").text(applicant.Gender || "N/A");
        $("#nationality").text(applicant.Nationality || "N/A");
        $("#maritalStatus").text(applicant.MaritalStatus || "N/A");
        $("#religion").text(applicant.Religion || "N/A");
        $("#foreigner").text(applicant.IsForeigner ? "Yes" : "No");
        $("#email").text(applicant.Email || "N/A");
        $("#phone").text(applicant.Phone || "N/A");
        $("#address").text(`${applicant.AddressLine1 || ""}, ${applicant.AddressLine2 || ""}, ${applicant.City || ""}, ${applicant.State || ""}, ${applicant.PostalCode || ""}, ${applicant.Country || ""}`);
        $("#kinName").text(applicant.NextOfKinName || "N/A");
        $("#kinRelation").text(applicant.NextOfKinRelation || "N/A");
        $("#kinPhone").text(applicant.NextOfKinPhone || "N/A");
        $("#kinEmail").text(applicant.NextOfKinEmail || "N/A");
        $("#kinAddress").text(applicant.NextOfKinAddress || "N/A");
        $("#highSchoolName").text(applicant.FormerSchoolName || "N/A");
        $("#highSchoolAddress").text(applicant.FormerSchoolAddress || "N/A");
        $("#educationLevel").text(applicant.FormerSchoolLevel || "N/A");
        $("#yearOfCompletion").text(applicant.YearOfCompletion || "N/A");
        $("#school").text(applicant.School?.Name || "N/A");
        $("#programme").text(applicant.Programme?.Name || "N/A");
        $("#modeOfStudy").text(applicant.ModeOfStudy?.ModeName || "N/A");
        $("#academicYear").text(`${applicant.AcademicYear?.YearValue || "N/A"}/${applicant.AcademicYear?.SemesterId || "N/A"}`);
        $("#level").text(applicant.ProgrammeLevel?.Name || "N/A");

        // Show or hide Subject Grades section based on education level
        if (applicant.ProgrammeLevel?.Name === "Diploma" || applicant.ProgrammeLevel?.Name === "Bachelor's") {
            $("#subjectGradesSection").show();

            // Process the applicant.SubjectGrades
            const subjectGrades = resolveReferences(applicant.SubjectGrades)?.$values || [];

            // Generate the list of grades
            const gradesList = subjectGrades.map(g => {
                const subjectName = g.Subject?.SubjectName || "N/A";
                const gradeValue = g.Grade?.GradeValue || "N/A";
                return `
                    <div style="display: flex; justify-content: space-between; border-bottom: 1px solid #ddd; padding: 5px 0;">
                        <span>${subjectName}</span>
                        <span>${gradeValue}</span>
                    </div>`;
            });

            // Display grades
            $("#subjectGradesList").html(gradesList.join(""));
        } else {
            // Hide the Subject Grades section if Education Level is not Diploma or Bachelor's
            $("#subjectGradesSection").hide();
        }

        const viewFileInModal = (filePath, fileType) => {
            if (!filePath) {
                Swal.fire({
                    icon: 'error',
                    title: 'Oops...',
                    text: 'File not available for viewing!',
                });
                return;
            }

            // Set the file path in the iframe's `src` attribute
            const filePreview = document.getElementById('filePreview');
            filePreview.src = `/AdmissionProcess/PreviewFile?filePath=${encodeURIComponent(filePath)}&type=${fileType}`;

            // Show the modal
            const fileModal = new bootstrap.Modal(document.getElementById('fileModal'));
            fileModal.show();
        };

        // Attachments event handlers
        $("#nrcCopy")
            .off("click")
            .on("click", function (e) {
                e.preventDefault();
                viewFileInModal(applicant.NrcOrPassportCopy, 'nrc');
            })
            .text(applicant.NrcOrPassportCopy ? "View" : "No file uploaded");

        $("#resultsAttachment")
            .off("click")
            .on("click", function (e) {
                e.preventDefault();
                viewFileInModal(applicant.ResultsAttachmentCopy, 'results');
            })
            .text(applicant.ResultsAttachmentCopy ? "View" : "No file uploaded");

        $("#studyPermit")
            .off("click")
            .on("click", function (e) {
                e.preventDefault();
                viewFileInModal(applicant.StudyPermitCopy, 'studyPermit');
            })
            .text(applicant.StudyPermitCopy ? "View" : "No file uploaded");

        // Show modal
        $('#detailsModal').modal('show');
    });

    $(document).on("click", ".check-requirements", function () {
        const applicantId = $(this).data("id");
        $('input[name=applicantId]').val(applicantId);

        $.ajax({
            url: '/AdmissionProcess/CheckRequirements',
            method: 'POST',
            data: { applicantId },
            beforeSend: function () {
                $(this).prop("disabled", true).text("Checking...");
            },
            success: function (response) {
                $(".check-requirements[data-id='" + applicantId + "']").prop("disabled", false).text("Check Requirements");

                Swal.fire({
                    icon: 'success',
                    title: 'Requirements Checked!',
                    text: response.message,
                }).then((result) => {
                    if (result.isConfirmed) {
                        $('#message').html(response.message);

                        // Show the program assignment section if the requirements are not met
                        if (!response.meetsRequirements) {
                            $('#programAssignment').removeClass('d-none');
                        } else {
                            $('#programAssignment').addClass('d-none');
                            $(".admit").removeClass('d-none');
                            $(".waitlist").removeClass('d-none');
                            $(".reject").addClass('d-none');
                        }

                        $("#requirementsModal").modal("show");

                        // Fetch schools for the dropdown dynamically
                        $.ajax({
                            url: '/AdmissionProcess/GetSchools',
                            method: 'POST',
                            success: function (schoolResponse) {
                                if (schoolResponse.success) {
                                    const schoolDropdown = $('#newSchool');
                                    schoolDropdown.empty(); // Clear the existing options
                                    schoolDropdown.append($('<option>', {
                                        text: 'Select School',
                                        value: '',
                                        disabled: true,
                                        selected: true
                                    }));
                                    // Append the schools dynamically
                                    schoolResponse.schools.forEach(function (school) {
                                        schoolDropdown.append($('<option>', {
                                            text: school.name,
                                            value: school.id
                                        }));
                                    });

                                    // Trigger school change to populate programs
                                    schoolDropdown.on('change', function () {
                                        const schoolId = $(this).val();
                                        if (schoolId) {
                                            populatePrograms(schoolId);
                                            $(".admit").removeClass('d-none');
                                            $(".waitlist").addClass('d-none');
                                        } else {
                                            $(".admit").addClass('d-none');
                                            $(".waitlist").removeClass('d-none');
                                        }
                                    });

                                    $(".admit").removeClass('d-none');
                                    $(".waitlist").removeClass('d-none');
                                }
                            },
                            error: function () {
                                Swal.fire('Error', 'Failed to load schools.', 'error');
                            }
                        });
                    }
                });

                $("#requirementsModal").on("hidden.bs.modal", function () {
                    $(this).remove();
                });
            },
            error: function (xhr, status, error) {
                $(".check-requirements[data-id='" + applicantId + "']").prop("disabled", false).text("Check Requirements");

                Swal.fire({
                    icon: 'error',
                    title: 'Server Error',
                    text: xhr.responseText || `An error occurred: ${error}`,
                });
            }
        });
    });

    // Function to populate programs based on selected school
    function populatePrograms(schoolId) {
        $.ajax({
            url: '/AdmissionProcess/GetProgramsForSchool',
            method: 'GET',
            data: { schoolId },
            success: function (programResponse) {
                if (programResponse.success) {
                    const programDropdown = $('#newProgram');
                    programDropdown.empty();
                    programDropdown.append($('<option>', {
                        text: 'Select Programme',
                        value: '',
                        disabled: true,
                        selected: true
                    }));

                    if (programResponse.programs.length > 0) {
                        programResponse.programs.forEach(function (program) {
                            programDropdown.append(new Option(program.name, program.id));
                        });
                    } else {
                        programDropdown.append(new Option("No programs available", ""));
                    }
                } else {
                    Swal.fire('Error', 'Failed to load programs.', 'error');
                }
            },
            error: function () {
                Swal.fire('Error', 'Failed to load programs for the selected school.', 'error');
            }
        });
    }

    $(document).on("click", ".admit, .waitlist, .reject", function () {
        const action = $(this).hasClass('admit') ? 'admit' : ($(this).hasClass('waitlist') ? 'waitlist' : 'reject');
        const applicantId = $('input[name=applicantId]').val();
        const schoolId = document.getElementById('newSchool').value;
        const programId = document.getElementById('newProgram').value;

        // Ensure all data is selected
        if (!schoolId || !programId) {
            Swal.fire('Error', 'Please select a school and a program.', 'error');
            return;
        }

        $.ajax({
            url: `/AdmissionProcess/${action}`,
            method: 'POST',
            data: {
                applicantId,
                schoolId,
                programId
            },
            success: function (response) {
                Swal.fire(response.success ? 'Success' : 'Error', response.message, response.success ? 'success' : 'error');
                $("#requirementsModal").modal('hide');
            },
            error: function (xhr, status, error) {
                Swal.fire('Error', `Error: ${error}`, 'error');
            }
        });
    });
});