import { select_lecturer_row } from './course_components.js';
import { select_prerequisite_row } from './course_components.js';
import { select_assessment_row } from './course_components.js';

$(document).ready(function () {
    // Lecturer Section
    var lecturerRowTemplate = select_lecturer_row(lecturers);

    // Add this JavaScript to both views
    document.querySelector('form').addEventListener('submit', function (e) {
        const venueSelect = document.getElementById('PreferredVenues');
        const selectedVenues = Array.from(venueSelect.selectedOptions).map(opt => opt.value);
        document.querySelector('[name="PreferredVenueIds"]').value = JSON.stringify(selectedVenues);
    });


    // Add event listener to the "Add Lecturer" button
    $('#add-leturer-btn').click(function () {
        var lecturerContainer = $(this).closest('.border.rounded-lg');
        var lastLecturerDataRow = lecturerContainer.find('.grid.grid-cols-12.items-center.border-b:last');

        if (lastLecturerDataRow.length) {
            $(lecturerRowTemplate).insertAfter(lastLecturerDataRow);
        } else {
            lecturerContainer.find('.bg-gray-800.text-white').after(lecturerRowTemplate);
        }
    });

    // When the lecturer header checkbox is clicked
    $('#header-check').on('change', function () {
        var isChecked = $(this).is(':checked');
        $('.data-check').prop('checked', isChecked);
    });

    // Update lecturer header checkbox based on individual data-check checkboxes
    $(document).on('change', '.data-check', function () {
        var totalCheckboxes = $('.data-check').length;
        var checkedCheckboxes = $('.data-check:checked').length;
        $('#header-check').prop('checked', totalCheckboxes === checkedCheckboxes);
    });

    // Remove lecturer rows
    $('#rmv-leturer-btn').click(function () {
        var checkedLecturerRows = $(this).closest('.border.rounded-lg')
            .find('.data-check:checked');

        if (checkedLecturerRows.length) {
            checkedLecturerRows.each(function () {
                $(this).closest('.grid.grid-cols-12.items-center.border-b').remove();
            });
        } else {
            var lastLecturerDataRow = $(this).closest('.border.rounded-lg')
                .find('.grid.grid-cols-12.items-center.border-b:last');
            if (lastLecturerDataRow.length) {
                lastLecturerDataRow.remove();
            }
        }
    });

    // Prerequisite Section
    var prerequisiteRowTemplate = select_prerequisite_row(courses);

    // Add event listener to the "Add Prerequisite" button
    $('#add-prerequisite-btn').click(function () {
        var prerequisiteContainer = $(this).closest('.border.rounded-lg');
        var lastPrerequisiteDataRow = prerequisiteContainer.find('.flex.items-center.border-b:last');

        if (lastPrerequisiteDataRow.length) {
            $(prerequisiteRowTemplate).insertAfter(lastPrerequisiteDataRow);
        } else {
            prerequisiteContainer.find('.bg-gray-800.text-white').after(prerequisiteRowTemplate);
        }
    });

    // When the prerequisite header checkbox is clicked
    $('#prerequisite-check').on('change', function () {
        var isChecked = $(this).is(':checked');
        $('.prerequisite-data-check').prop('checked', isChecked);
    });

    // Update prerequisite header checkbox based on individual data-check checkboxes
    $(document).on('change', '.prerequisite-data-check', function () {
        var totalCheckboxes = $('.prerequisite-data-check').length;
        var checkedCheckboxes = $('.prerequisite-data-check:checked').length;
        $('#prerequisite-check').prop('checked', totalCheckboxes === checkedCheckboxes);
    });

    // Remove prerequisite rows
    $('#rmv-prerequisite-btn').click(function () {
        var checkedPrerequisiteRows = $(this).closest('.border.rounded-lg')
            .find('.prerequisite-data-check:checked');

        if (checkedPrerequisiteRows.length) {
            checkedPrerequisiteRows.each(function () {
                $(this).closest('.flex.items-center.border-b').remove();
            });
        } else {
            var lastPrerequisiteDataRow = $(this).closest('.border.rounded-lg')
                .find('.flex.items-center.border-b:last');
            if (lastPrerequisiteDataRow.length) {
                lastPrerequisiteDataRow.remove();
            }
        }
    });

    // Assessment Section
    var assessmentRowTemplate = select_assessment_row(assessments);

    $('#add-assessment-btn').click(function () {
        var assessmentContainer = $(this).closest('.border.rounded-lg');
        var lastAssessmentDataRow = assessmentContainer.find('.flex.items-center.border-b:last');

        if (lastAssessmentDataRow.length) {
            $(assessmentRowTemplate).insertAfter(lastAssessmentDataRow);
        } else {
            assessmentContainer.find('.bg-gray-800.text-white').after(assessmentRowTemplate);
        }
    });

    $('#assessment-check').on('change', function () {
        var isChecked = $(this).is(':checked');
        $('.assessment-data-check').prop('checked', isChecked);
    });

    // Update assessment header checkbox based on individual data-check checkboxes
    $(document).on('change', '.assessment-data-check', function () {
        var totalCheckboxes = $('.assessment-data-check').length;
        var checkedCheckboxes = $('.assessment-data-check:checked').length;
        $('#assessment-check').prop('checked', totalCheckboxes === checkedCheckboxes);
    });

    $('#rmv-assessment-btn').click(function () {
        var checkedRows = $(this).closest('.border.rounded-lg')
            .find('.assessment-data-check:checked');

        if (checkedRows.length) {
            checkedRows.each(function () {
                $(this).closest('.flex.items-center.border-b').remove();
            });
        } else {
            var lastRow = $(this).closest('.border.rounded-lg')
                .find('.flex.items-center.border-b:last');
            if (lastRow.length) lastRow.remove();
        }
    });
    console.log('SNG');
});