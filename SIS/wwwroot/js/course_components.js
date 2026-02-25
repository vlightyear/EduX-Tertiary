export function select_lecturer_row(lecturers) {
    let options = '<select name="LecturerIds" class="w-full px-3 py-1.5 text-sm border border-gray-300 rounded-lg focus:ring-blue-500 focus:border-blue-500">';
    options += '<option value="">Select Lecturer</option>';
    lecturers.forEach(lecturer => {
        options += `<option value="${lecturer.value}">${lecturer.text}</option>`;
    });
    options += '</select>';

    return `
        <div class="bg-gray-50 px-4 py-2 grid grid-cols-12 items-center border-b">
            <div class="col-span-2 flex justify-center">
                <input type="checkbox" class="data-check rounded">
            </div>
            <div class="col-span-6">
                ${options}
            </div>
            <div class="col-span-4">
                <input type="text" class="w-full px-3 py-1.5 text-sm border border-gray-300 rounded-lg bg-gray-100" 
                       value="Lecturer" disabled>
            </div>
        </div>
    `;
}

export function select_prerequisite_row(courses) {
    let options = '<select name="PrerequisiteCourseIds" class="w-full px-3 py-1.5 text-sm border border-gray-300 rounded-lg focus:ring-blue-500 focus:border-blue-500">';
    options += '<option value="">Select Course</option>';
    courses.forEach(course => {
        options += `<option value="${course.value}">${course.text}</option>`;
    });
    options += '</select>';

    return `
        <div class="bg-gray-50 px-4 py-2 flex items-center border-b">
            <div class="w-12 flex justify-center">
                <input type="checkbox" class="prerequisite-data-check rounded">
            </div>
            <div class="flex-1">
                ${options}
            </div>
        </div>
    `;
}

export function select_assessment_row(assessments) {
    let options = '<select name="AssessmentIds" class="w-full px-3 py-1.5 text-sm border border-gray-300 rounded-lg focus:ring-blue-500 focus:border-blue-500">';
    options += '<option value="">Select Assessment</option>';
    assessments.forEach(assessment => {
        options += `<option value="${assessment.value}">${assessment.text}</option>`;
    });
    options += '</select>';

    return `
        <div class="bg-gray-50 px-4 py-2 flex items-center border-b">
            <div class="w-12 flex justify-center">
                <input type="checkbox" class="assessment-data-check rounded">
            </div>
            <div class="flex-1">
                ${options}
            </div>
        </div>
    `;
}