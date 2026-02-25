

export function enter_year_row() {

    // Return the complete row with the populated select element
    return `
        <div class="data-row form-group row d-flex justify-content-center align-items-center col-md-12" style="background-color: #ecf0f1; border-bottom: 1px solid #bdc3c7; height: 35px;">
            <div class="col-md-2 d-flex justify-content-center align-items-center">
                <input type="checkbox" class="data-check">
            </div>
            <div class="col-md-10 d-flex justify-content-start align-items-center border-left">
                <input type="number" name="YearOfStudy" class="form-control form-control-sm" placeholder="Enter Year of Study">
            </div>
        </div>
    `;
}

export function select_programme_row(programmes) {
    console.log(programmes);
    // Start the select element
    let options = '<select name="ProgrammeIds" class="form-control form-control-sm">';
    options += '<option value="">Select Programme</option>'; // Default option
    // Loop through the programmes array and create option elements
    programmes.forEach(programme => {
        options += `<option value="${programme.value}">${programme.text}</option>`;
    });
    options += '</select>'; // Close the select element

    // Return the complete row with the populated select element
    return `
        <div class="data-row form-group row d-flex justify-content-center align-items-center col-md-12" style="background-color: #ecf0f1; border-bottom: 1px solid #bdc3c7; height: 35px;">
            <div class="col-md-2 d-flex justify-content-center align-items-center">
                <input type="checkbox" class="data-check">
            </div>
            <div class="col-md-10 d-flex justify-content-start align-items-center border-left">
                ${options} <!-- Insert the select element here -->
            </div>
        </div>
    `;
}
export function select_school_row(schools) {
    // Start the select element
    let options = '<select name="SchoolIds" class="form-control form-control-sm">';
    options += '<option value="">Select School</option>'; // Default option
    // Loop through the schools array and create option elements
    schools.forEach(school => {
        options += `<option value="${school.value}">${school.text}</option>`;
    });
    options += '</select>'; // Close the select element

    // Return the complete row with the populated select element
    return `
        <div class="data-row form-group row d-flex justify-content-center align-items-center col-md-12" style="background-color: #ecf0f1; border-bottom: 1px solid #bdc3c7; height: 35px;">
            <div class="col-md-2 d-flex justify-content-center align-items-center">
                <input type="checkbox" class="data-check">
            </div>
            <div class="col-md-10 d-flex justify-content-start align-items-center border-left">
                ${options} <!-- Insert the select element here -->
            </div>
        </div>
    `;
}

export function select_mos_row(modesOfStudy) {
    // Start the select element
    let options = '<select name="ModeOfStudyIds" class="form-control form-control-sm">';
    options += '<option value="">Select Mode of Study</option>'; // Default option
    // Loop through the modesOfStudy array and create option elements
    modesOfStudy.forEach(mos => {
        options += `<option value="${mos.value}">${mos.text}</option>`;
    });
    options += '</select>'; // Close the select element

    // Return the complete row with the populated select element
    return `
        <div class="data-row form-group row d-flex justify-content-center align-items-center col-md-12" style="background-color: #ecf0f1; border-bottom: 1px solid #bdc3c7; height: 35px;">
            <div class="col-md-2 d-flex justify-content-center align-items-center">
                <input type="checkbox" class="data-check">
            </div>
            <div class="col-md-10 d-flex justify-content-start align-items-center border-left">
                ${options} <!-- Insert the select element here -->
            </div>
        </div>
    `;
}