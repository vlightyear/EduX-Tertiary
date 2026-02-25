document.addEventListener('DOMContentLoaded', function () {
    // Helper function for safely accessing nested properties
    function safeGet(obj, path, defaultValue = "N/A") {
        try {
            return path.split('.').reduce((acc, part) => acc && acc[part], obj) ?? defaultValue;
        } catch (e) {
            return defaultValue;
        }
    }

    // Modal elements
    const detailsModal = document.getElementById('detailsModal');
    const fileModal = document.getElementById('fileModal');

    // Modal handling functions
    function showModal(modalElement) {
        modalElement.classList.remove('hidden');
        document.body.style.overflow = 'hidden';
    }

    function hideModal(modalElement) {
        modalElement.classList.add('hidden');
        document.body.style.overflow = 'auto';
    }

    // Alert handling function
    function showAlert({ type, title, text, isConfirm = false }) {
        return new Promise((resolve) => {
            // Clone the alert template
            const alertTemplate = document.getElementById('alertTemplate');
            const alertEl = alertTemplate.cloneNode(true);
            alertEl.id = '';
            alertEl.classList.remove('hidden');

            // Set the appropriate colors based on type
            const alertHeader = alertEl.querySelector('.alert-header');
            const alertIcon = alertEl.querySelector('.alert-icon');
            const alertTitle = alertEl.querySelector('.alert-title');
            const alertText = alertEl.querySelector('.alert-text');
            const alertButtons = alertEl.querySelector('.alert-buttons');

            let bgColor, textColor, iconName;

            switch (type) {
                case 'error':
                    bgColor = 'text-red-600';
                    textColor = 'text-red-600';
                    iconName = 'error';
                    break;
                case 'success':
                    bgColor = 'text-secondary-600';
                    textColor = 'text-secondary-600';
                    iconName = 'check_circle';
                    break;
                case 'warning':
                    bgColor = 'text-amber-600';
                    textColor = 'text-amber-600';
                    iconName = 'warning';
                    break;
                default:
                    bgColor = 'text-primary-600';
                    textColor = 'text-primary-600';
                    iconName = 'info';
            }

            alertHeader.className = `flex items-center ${textColor}`;
            alertIcon.textContent = iconName;
            alertTitle.textContent = title;
            alertText.textContent = text;

            // Set the buttons based on isConfirm
            if (isConfirm) {
                alertButtons.innerHTML = `
                    <div class="flex space-x-3">
                        <button class="flex-1 bg-secondary-600 text-white py-2 px-4 rounded-lg hover:bg-secondary-700 transition-colors confirm-yes">
                            Yes, Proceed
                        </button>
                        <button class="flex-1 bg-slate-200 text-slate-700 py-2 px-4 rounded-lg hover:bg-slate-300 transition-colors confirm-no">
                            Cancel
                        </button>
                    </div>
                `;
            } else {
                alertButtons.innerHTML = `
                    <button class="w-full bg-primary-600 text-white py-2 px-4 rounded-lg hover:bg-primary-700 transition-colors">
                        OK
                    </button>
                `;
            }

            document.body.appendChild(alertEl);

            // Handle button clicks
            if (isConfirm) {
                alertEl.querySelector('.confirm-yes').onclick = () => {
                    alertEl.classList.add('opacity-0');
                    setTimeout(() => alertEl.remove(), 300);
                    resolve(true);
                };
                alertEl.querySelector('.confirm-no').onclick = () => {
                    alertEl.classList.add('opacity-0');
                    setTimeout(() => alertEl.remove(), 300);
                    resolve(false);
                };
            } else {
                const closeBtn = alertEl.querySelector('button');
                closeBtn.onclick = () => {
                    alertEl.classList.add('opacity-0');
                    setTimeout(() => alertEl.remove(), 300);
                    resolve(true);
                };
            }

            // Auto-remove after 5 seconds only for non-confirm alerts
            if (!isConfirm) {
                setTimeout(() => {
                    if (alertEl.parentNode) {
                        alertEl.classList.add('opacity-0');
                        setTimeout(() => {
                            alertEl.remove();
                            resolve(false);
                        }, 300);
                    }
                }, 5000);
            }
        });
    }

    // Table filtering functionality
    initializeTableFilters();

    function initializeTableFilters() {
        const filterSchool = document.getElementById('filterSchool');
        const filterProgramme = document.getElementById('filterProgramme');
        const filterQualification = document.getElementById('filterQualification');
        const searchInput = document.getElementById('searchApplicants');

        // Add event listeners to all filters
        [filterSchool, filterProgramme, filterQualification].forEach(filter => {
            if (filter) {
                filter.addEventListener('change', applyFilters);
            }
        });

        if (searchInput) {
            searchInput.addEventListener('input', applyFilters);
        }

        // Export data button
        const exportButton = document.getElementById('exportData');
        if (exportButton) {
            exportButton.addEventListener('click', exportTableData);
        }

        // Initialize pagination
        initializePagination();
    }

    // Apply all filters to the table
    function applyFilters() {
        const filterSchool = document.getElementById('filterSchool').value;
        const filterProgramme = document.getElementById('filterProgramme').value;
        const filterQualification = document.getElementById('filterQualification').value;
        const searchValue = document.getElementById('searchApplicants').value.toLowerCase();

        const rows = document.querySelectorAll('.applicant-row');
        let visibleRowCount = 0;

        rows.forEach(row => {
            // Get row data attributes
            const schoolId = row.getAttribute('data-school');
            const programmeId = row.getAttribute('data-programme');
            const isQualified = row.getAttribute('data-qualified');
            const rowText = row.textContent.toLowerCase();

            // Check if row matches all filters
            const matchesSchool = !filterSchool || schoolId === filterSchool;
            const matchesProgramme = !filterProgramme || programmeId === filterProgramme;
            const matchesQualification = !filterQualification || isQualified === filterQualification;
            const matchesSearch = !searchValue || rowText.includes(searchValue);

            // Show/hide row based on filter matches
            if (matchesSchool && matchesProgramme && matchesQualification && matchesSearch) {
                row.classList.remove('hidden');
                visibleRowCount++;
            } else {
                row.classList.add('hidden');
            }
        });

        // Update pagination after filtering
        updatePagination();

        // Show message if no results
        const table = document.getElementById('pendingAdmissions');
        const tableBody = table.querySelector('tbody');

        if (visibleRowCount === 0) {
            // Check if no-results message already exists
            if (!document.getElementById('no-results-message')) {
                const noResultsRow = document.createElement('tr');
                noResultsRow.id = 'no-results-message';
                noResultsRow.innerHTML = `<td colspan="8" class="px-6 py-4 text-center text-slate-500">No applications match your filter criteria</td>`;
                tableBody.appendChild(noResultsRow);
            }
        } else {
            // Remove no-results message if it exists
            const noResultsMessage = document.getElementById('no-results-message');
            if (noResultsMessage) {
                noResultsMessage.remove();
            }
        }
    }

    // Pagination functionality
    function initializePagination() {
        const rowsPerPage = 10;
        const table = document.getElementById('pendingAdmissions');
        const rows = table.querySelectorAll('tbody tr');
        const totalPages = Math.ceil(rows.length / rowsPerPage);

        // Set data attributes for pagination
        table.setAttribute('data-current-page', '1');
        table.setAttribute('data-rows-per-page', rowsPerPage);
        table.setAttribute('data-total-pages', totalPages);

        // Create pagination numbers
        updatePaginationNumbers();

        // Initialize page
        showPage(1);

        // Add event listeners to pagination controls
        document.getElementById('prevPage').addEventListener('click', function () {
            const currentPage = parseInt(table.getAttribute('data-current-page'));
            if (currentPage > 1) {
                showPage(currentPage - 1);
            }
        });

        document.getElementById('nextPage').addEventListener('click', function () {
            const currentPage = parseInt(table.getAttribute('data-current-page'));
            const totalPages = parseInt(table.getAttribute('data-total-pages'));
            if (currentPage < totalPages) {
                showPage(currentPage + 1);
            }
        });
    }

    function updatePaginationNumbers() {
        const table = document.getElementById('pendingAdmissions');
        const currentPage = parseInt(table.getAttribute('data-current-page'));
        const totalPages = parseInt(table.getAttribute('data-total-pages'));
        const paginationNumbers = document.getElementById('paginationNumbers');

        // Clear existing numbers
        paginationNumbers.innerHTML = '';

        // Determine range of pages to show
        let startPage = Math.max(1, currentPage - 2);
        let endPage = Math.min(totalPages, startPage + 4);

        // Adjust start if we're near the end
        if (endPage - startPage < 4) {
            startPage = Math.max(1, endPage - 4);
        }

        // Add first page if needed
        if (startPage > 1) {
            addPageButton(1);
            if (startPage > 2) {
                addEllipsis();
            }
        }

        // Add page numbers
        for (let i = startPage; i <= endPage; i++) {
            addPageButton(i);
        }

        // Add last page if needed
        if (endPage < totalPages) {
            if (endPage < totalPages - 1) {
                addEllipsis();
            }
            addPageButton(totalPages);
        }

        // Enable/disable prev/next buttons
        document.getElementById('prevPage').disabled = currentPage === 1;
        document.getElementById('nextPage').disabled = currentPage === totalPages || totalPages === 0;

        function addPageButton(pageNum) {
            const button = document.createElement('button');
            button.className = `px-3 py-1 text-sm rounded-md ${currentPage === pageNum ? 'bg-primary-100 text-primary-700' : 'text-slate-600 hover:bg-slate-100'}`;
            button.textContent = pageNum;
            button.addEventListener('click', function () {
                showPage(pageNum);
            });
            paginationNumbers.appendChild(button);
        }

        function addEllipsis() {
            const span = document.createElement('span');
            span.className = 'px-3 py-1 text-sm text-slate-600';
            span.textContent = '...';
            paginationNumbers.appendChild(span);
        }
    }

    function showPage(pageNum) {
        const table = document.getElementById('pendingAdmissions');
        const rowsPerPage = parseInt(table.getAttribute('data-rows-per-page'));
        const visibleRows = Array.from(table.querySelectorAll('tbody tr:not(.hidden)'));

        // Update current page
        table.setAttribute('data-current-page', pageNum);

        // Hide all rows
        visibleRows.forEach(row => {
            row.classList.add('pagination-hidden');
        });

        // Show rows for current page
        const startIndex = (pageNum - 1) * rowsPerPage;
        const endIndex = Math.min(startIndex + rowsPerPage, visibleRows.length);

        for (let i = startIndex; i < endIndex; i++) {
            if (visibleRows[i]) {
                visibleRows[i].classList.remove('pagination-hidden');
            }
        }

        // Update pagination UI
        updatePaginationNumbers();

        // Update showing X to Y of Z entries text
        document.getElementById('pageStart').textContent = visibleRows.length > 0 ? startIndex + 1 : 0;
        document.getElementById('pageEnd').textContent = endIndex;
        document.getElementById('totalEntries').textContent = visibleRows.length;
    }

    function updatePagination() {
        const table = document.getElementById('pendingAdmissions');
        const visibleRows = table.querySelectorAll('tbody tr:not(.hidden)');
        const rowsPerPage = parseInt(table.getAttribute('data-rows-per-page'));
        const totalPages = Math.ceil(visibleRows.length / rowsPerPage);

        // Update total pages
        table.setAttribute('data-total-pages', totalPages);

        // Reset to first page when filters change
        table.setAttribute('data-current-page', '1');

        // Show first page
        showPage(1);
    }

    // Export table data to CSV
    function exportTableData() {
        const table = document.getElementById('pendingAdmissions');
        const visibleRows = Array.from(table.querySelectorAll('tbody tr:not(.hidden)'));

        // Get headers
        const headers = Array.from(table.querySelectorAll('thead th'))
            .map(th => th.textContent.trim());

        // Get visible rows data
        const rows = visibleRows.map(row => {
            return Array.from(row.querySelectorAll('td'))
                .map(td => {
                    // Get text content, removing any excess whitespace
                    return td.textContent.replace(/\s+/g, ' ').trim();
                });
        });

        // Combine headers and rows
        const csvData = [headers].concat(rows);

        // Convert to CSV format
        const csvContent = csvData.map(row => row.join(',')).join('\n');

        // Create download link
        const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');

        // Set link properties
        link.setAttribute('href', url);
        link.setAttribute('download', 'pending_applications.csv');
        link.style.visibility = 'hidden';

        // Add to document, click and remove
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    }

    // Function to populate applicant details
    function populateApplicantDetails(applicant) {
        if (!applicant) return;

        // Set action button IDs for the modal
        document.querySelector('.admit-applicant').setAttribute('data-id', applicant.applicantId);
        document.querySelector('.waitlist-applicant').setAttribute('data-id', applicant.applicantId);
        document.querySelector('.reject-applicant').setAttribute('data-id', applicant.applicantId);

        const fields = {
            // Personal Information
            'nrcOrPassport': 'nrcOrPassport',
            'fullName': 'fullName',
            'dob': applicant.dateOfBirth ? new Date(applicant.dateOfBirth).toLocaleDateString() : "N/A",
            'gender': 'gender',
            'nationality': 'nationality',
            'maritalStatus': 'maritalStatus',
            'religion': 'religion',
            'foreigner': applicant.isForeigner ? "Yes" : "No",
            'email': 'email',
            'phone': 'phone',

            // Contact Information
            'address': `${safeGet(applicant, 'addressLine1')}, ${safeGet(applicant, 'addressLine2')}, ${safeGet(applicant, 'city')}, ${safeGet(applicant, 'state')}, ${safeGet(applicant, 'postalCode')}, ${safeGet(applicant, 'country')}`.replace(/,\s*,/g, ',').replace(/^,\s*/, '').replace(/,\s*$/, ''),

            // Next of Kin Information
            'kinName': 'nextOfKinName',
            'kinRelation': 'nextOfKinRelation',
            'kinPhone': 'nextOfKinPhone',
            'kinEmail': 'nextOfKinEmail',
            'kinAddress': 'nextOfKinAddress',

            // School Information
            'school': 'school.name',
            'programme': 'programme.name',
            'modeOfStudy': 'modeOfStudy.modeName',
            'level': 'programmeLevel.name',

            // Academic Information
            'primarySchoolName': 'primarySchoolName',
            'primarySchoolAddress': 'primarySchoolAddress',
            'priPeriod': 'primarySchoolPeriod',
            'secPeriod': 'secondarySchoolPeriod',
            'highSchoolName': 'secondarySchoolName',
            'highSchoolAddress': 'secondarySchoolAddress',
            'schlLevel': 'formerSchoolLevel',
            'tertiarySchoolName': 'formerSchoolName',
            'tertiarySchoolAddress': 'formerSchoolAddress',
            'educationLevel': 'formerSchoolLevel',
            'yearOfCompletion': 'yearOfCompletion'
        };

        // Populate fields
        Object.entries(fields).forEach(([elementId, path]) => {
            const element = document.getElementById(elementId);
            if (element) {
                element.textContent = typeof path === 'string' ? safeGet(applicant, path) : path;
            }
        });

        // Special handling for academic year
        const academicYear = document.getElementById('academicYear');
        if (academicYear) {
            academicYear.textContent = safeGet(applicant, 'academicYear.yearValue');
        }

        // Show tertiary school section if available
        const tertiarySchoolSection = document.getElementById('tertiarySchoolSection');
        if (tertiarySchoolSection) {
            if (applicant.formerSchoolName) {
                tertiarySchoolSection.classList.remove('hidden');
            } else {
                tertiarySchoolSection.classList.add('hidden');
            }
        }

        // Handle Subject Grades
        const subjectGradesSection = document.getElementById('subjectGradesSection');
        if (subjectGradesSection) {
            const subjectGrades = applicant.subjectGrades;
            let totalPoints = 0;

            const gradesList = subjectGrades
                .sort((a, b) => (b.grade?.gradePoint || 0) - (a.grade?.gradePoint || 0))
                .slice(0, 5)
                .map(grade => {
                    const subjectName = grade.subject?.subjectName || 'N/A';
                    const gradeValue = grade.grade?.gradeValue || 'N/A';
                    const gradePoint = parseFloat(grade.grade?.gradePoint || 0);

                    if (!isNaN(gradePoint)) {
                        totalPoints += gradePoint;
                    }

                    return `
                    <div class="flex justify-between border-b border-slate-200 py-2">
                        <span class="font-medium">${subjectName}</span>
                        <span>${gradeValue} (${gradePoint} points)</span>
                    </div>`;
                }).join('');

            const subjectGradesList = document.getElementById('subjectGradesList');
            if (subjectGradesList) {
                subjectGradesList.innerHTML = gradesList;
            }

            const totalPointsElement = document.getElementById('totalPoints');
            if (totalPointsElement) {
                const minPoints = safeGet(applicant, 'programme.minimumPointsTop5Subjects', 0);
                totalPointsElement.textContent = `${minPoints}/${totalPoints}`;

                // Add color based on qualification
                if (totalPoints >= minPoints) {
                    totalPointsElement.className = 'text-xl font-bold text-secondary-600';
                } else {
                    totalPointsElement.className = 'text-xl font-bold text-amber-600';
                }
            }
        }

        // Setup file links
        setupFilePreviewLinks(applicant);
    }

    // View Details Handler
    document.querySelectorAll('.view-details').forEach(button => {
        button.addEventListener('click', function () {
            try {
                const applicantId = parseInt(this.dataset.applicant);

                // Find the applicant in the array
                const foundApplicant = applicants.find(a => a.applicantId === applicantId);

                if (!foundApplicant) {
                    throw new Error('Applicant not found');
                }

                // Populate details
                populateApplicantDetails(foundApplicant);

                // Setup action handlers for modal buttons AFTER populating details
                setupModalActionHandlers(foundApplicant.applicantId);

                // Show the modal
                showModal(detailsModal);

            } catch (error) {
                console.error('Error showing details:', error);
                showAlert({
                    type: 'error',
                    title: 'Error',
                    text: 'Failed to load applicant details'
                });
            }
        });
    });



    // New function to handle modal action buttons
    function setupModalActionHandlers(applicantId) {
        // Set the applicant ID for all modal action buttons
        document.querySelector('.admit-applicant').setAttribute('data-id', applicantId);
        document.querySelector('.waitlist-applicant').setAttribute('data-id', applicantId);
        document.querySelector('.reject-applicant').setAttribute('data-id', applicantId);

        // Remove any existing event listeners to prevent duplicates
        const modalButtons = document.querySelectorAll('.admit-applicant, .waitlist-applicant, .reject-applicant');
        modalButtons.forEach(button => {
            const newButton = button.cloneNode(true);
            button.parentNode.replaceChild(newButton, button);
        });

        // Setup event listeners for modal action buttons
        handleModalAction('.admit-applicant', 'admit');
        handleModalAction('.waitlist-applicant', 'waitlist');
        handleModalAction('.reject-applicant', 'reject');
    }

    // Generic handler for modal actions
    function handleModalAction(selector, action) {
        const button = document.querySelector(selector);
        if (!button) return;

        button.addEventListener('click', async function () {
            try {
                const applicantId = this.getAttribute('data-id');
                if (!applicantId) {
                    throw new Error('Applicant ID not found');
                }

                // Confirm action if it's a rejection
                if (action === 'reject') {
                    const confirmed = await showAlert({
                        type: 'warning',
                        title: 'Confirm Rejection',
                        text: 'Are you sure you want to reject this application? This action cannot be undone.',
                        isConfirm: true
                    });

                    if (!confirmed) {
                        return;
                    }
                }

                // Disable the button and show loading state
                this.disabled = true;
                const originalText = this.innerHTML;
                this.innerHTML = `<i class="material-icons text-xs mr-1">hourglass_empty</i>Processing...`;

                // Create form data
                const formData = new FormData();
                formData.append('applicantId', applicantId);

                const response = await fetch(`/AdmissionProcess/${action}`, {
                    method: 'POST',
                    body: formData
                });

                if (!response.ok) {
                    throw new Error(`Failed to ${action} application`);
                }

                const data = await response.json();

                if (data.success) {
                    showAlert({
                        type: 'success',
                        title: 'Success',
                        text: data.message || `Application successfully ${action}ed`
                    });

                    // Close modal
                    hideModal(detailsModal);

                    // Reload after success
                    setTimeout(() => location.reload(), 1500);
                } else {
                    throw new Error(data.message || `Failed to ${action} application`);
                }
            } catch (error) {
                console.error(`${action} action error:`, error);
                showAlert({
                    type: 'error',
                    title: 'Error',
                    text: error.message || `Failed to process request`
                });

                // Reset button on error
                this.disabled = false;
                this.innerHTML = originalText;
            }
        });
    }




















































































    // File preview handler
    function viewFileInModal(filePath, fileType) {
        if (!filePath) {
            showAlert({
                type: 'error',
                title: 'Error',
                text: 'File not available for viewing!'
            });
            return;
        }

        // Hide the details modal temporarily
        hideModal(detailsModal);

        // Get the file preview iframe
        const filePreview = document.getElementById('filePreview');
        if (filePreview) {
            // Sanitize and encode the file path
            const encodedPath = encodeURIComponent(filePath.replace(/\\/g, '/'));

            // Check if it's an image file
            const isImage = fileType === 'passportPhoto' ||
                filePath.toLowerCase().match(/\.(jpg|jpeg|png|gif|webp)$/);

            if (isImage) {
                // For images, create an img element instead of using iframe
                const previewUrl = `/AdmissionProcess/PreviewFile?filePath=${encodedPath}`;
                filePreview.style.display = 'none';

                // Create or update image container
                let imageContainer = document.getElementById('imagePreviewContainer');
                if (!imageContainer) {
                    imageContainer = document.createElement('div');
                    imageContainer.id = 'imagePreviewContainer';
                    imageContainer.className = 'w-full h-[500px] flex items-center justify-center bg-slate-50';
                    filePreview.parentNode.insertBefore(imageContainer, filePreview);
                }

                imageContainer.innerHTML = `
                <img src="${previewUrl}" 
                     alt="Passport Photo" 
                     class="max-w-full max-h-full object-contain rounded-lg shadow-sm"
                     style="max-height: 480px;"
                     onerror="this.parentElement.innerHTML='<div class=&quot;text-center text-slate-500&quot;><i class=&quot;material-icons text-4xl mb-2&quot;>broken_image</i><br>Unable to load image</div>'">
            `;
                imageContainer.style.display = 'flex';
            } else {
                // For PDFs and other documents, use iframe as before
                const imageContainer = document.getElementById('imagePreviewContainer');
                if (imageContainer) {
                    imageContainer.style.display = 'none';
                }
                filePreview.style.display = 'block';
                filePreview.src = `/AdmissionProcess/PreviewFile?filePath=${encodedPath}`;
            }

            // Show the file modal
            showModal(fileModal);

            // Handle modal close
            const closeButtons = fileModal.querySelectorAll('.close-modal');
            closeButtons.forEach(button => {
                button.addEventListener('click', () => {
                    hideModal(fileModal);
                    showModal(detailsModal);

                    // Clean up image container when closing
                    const imageContainer = document.getElementById('imagePreviewContainer');
                    if (imageContainer) {
                        imageContainer.style.display = 'none';
                    }
                }, { once: true });
            });
        }
    }

    function setupFilePreviewLinks(applicant) {
        const fileTypes = {
            'nrcCopy': 'nrcOrPassportCopy',
            'resultsAttachment': 'resultsAttachmentCopy',
            'studyPermit': 'studyPermitCopy',
            'passportPhoto': 'passportPhotoPath' // ADD THIS LINE
        };

        Object.entries(fileTypes).forEach(([elementId, fileProperty]) => {
            const element = document.getElementById(elementId);
            if (!element) return;

            const filePath = applicant[fileProperty];
            if (filePath) {
                element.innerHTML = '<i class="material-icons text-xs mr-1">visibility</i> View';
                element.href = '#';
                element.onclick = (e) => {
                    e.preventDefault();
                    viewFileInModal(filePath, elementId);
                };
            } else {
                element.innerHTML = 'No file uploaded';
                element.removeAttribute('href');
                element.onclick = null;
            }
        });
    }

    // Add modal close handlers
    document.querySelectorAll('.close-modal').forEach(button => {
        button.addEventListener('click', function () {
            const modal = this.closest('[id$="Modal"]');
            if (modal) hideModal(modal);
        });
    });

    // Direct action handlers (admit-direct, waitlist-direct, admit-applicant, waitlist-applicant, reject-applicant)
    function setupActionHandlers() {
        // Button selectors and their corresponding actions
        const actionButtons = [
            { selector: '.admit-direct', action: 'admit' },
            { selector: '.waitlist-direct', action: 'waitlist' },
            { selector: '.admit-applicant', action: 'admit' },
            { selector: '.waitlist-applicant', action: 'waitlist' },
            { selector: '.reject-applicant', action: 'reject' }
        ];

        actionButtons.forEach(({ selector, action }) => {
            document.querySelectorAll(selector).forEach(button => {
                button.addEventListener('click', async function () {
                    try {
                        const applicantId = this.getAttribute('data-id');
                        if (!applicantId) {
                            throw new Error('Applicant ID not found');
                        }

                        // Confirm action if it's a rejection
                        if (action === 'reject') {
                            const confirmed = await showAlert({
                                type: 'warning',
                                title: 'Confirm Rejection',
                                text: 'Are you sure you want to reject this application? This action cannot be undone.',
                                isConfirm: true
                            });

                            if (!confirmed) {
                                return;
                            }
                        }

                        // Disable the button and show loading state
                        this.disabled = true;
                        const originalText = this.innerHTML;
                        this.innerHTML = `<i class="material-icons text-xs mr-1">hourglass_empty</i>Processing...`;

                        // Create form data
                        const formData = new FormData();
                        formData.append('applicantId', applicantId);

                        const response = await fetch(`/AdmissionProcess/${action}`, {
                            method: 'POST',
                            body: formData
                        });

                        if (!response.ok) {
                            throw new Error(`Failed to ${action} application`);
                        }

                        const data = await response.json();

                        if (data.success) {
                            showAlert({
                                type: 'success',
                                title: 'Success',
                                text: data.message || `Application successfully ${action}ed`
                            });

                            // Close modal if action was from modal
                            if (selector.includes('applicant')) {
                                hideModal(detailsModal);
                            }

                            // Reload after success
                            setTimeout(() => location.reload(), 1500);
                        } else {
                            throw new Error(data.message || `Failed to ${action} application`);
                        }
                    } catch (error) {
                        console.error(`${action} action error:`, error);
                        showAlert({
                            type: 'error',
                            title: 'Error',
                            text: error.message || `Failed to process request`
                        });

                        // Reset button on error
                        this.disabled = false;
                        this.innerHTML = originalText;
                    }
                });
            });
        });
    }

    // Initialize action handlers
    setupActionHandlers();

    // Add bulk admit handler
    document.getElementById('admitAllQualified')?.addEventListener('click', async function () {
        try {
            // Get qualified applicants directly from the array
            const qualifiedIds = applicants
                .filter(a => a.isQualified)
                .map(a => a.applicantId);

            if (!qualifiedIds || !qualifiedIds.length) {
                showAlert({
                    type: 'warning',
                    title: 'No Qualified Applicants',
                    text: 'There are no qualified applicants to admit.'
                });
                return;
            }

            // Use custom confirm dialog
            const confirmed = await showAlert({
                type: 'success',
                title: 'Confirm Bulk Admission',
                text: `Are you sure you want to admit all ${qualifiedIds.length} qualified applicants?`,
                isConfirm: true
            });

            if (!confirmed) {
                return;
            }

            // Disable button and show loading state
            this.disabled = true;
            const originalText = this.innerHTML;
            this.innerHTML = `<i class="material-icons text-xs mr-1">hourglass_empty</i>Processing...`;

            const response = await fetch('/AdmissionProcess/AdmitQualified', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify(qualifiedIds)  // Send the array of IDs
            });

            if (!response.ok) {
                throw new Error('Failed to process bulk admission');
            }

            const data = await response.json();

            if (data.success) {
                showAlert({
                    type: 'success',
                    title: 'Success',
                    text: data.message || `Successfully admitted ${qualifiedIds.length} applicants`
                });

                if (data.errors?.length > 0) {
                    console.error('Admission errors:', data.errors);

                    // Show error summary if there were issues
                    setTimeout(() => {
                        showAlert({
                            type: 'warning',
                            title: 'Some Admissions Failed',
                            text: `${data.errors.length} applicants could not be admitted. See console for details.`
                        });
                    }, 1500);
                }

                // Reload after success
                setTimeout(() => location.reload(), 2000);
            } else {
                throw new Error(data.message || 'Failed to process bulk admission');
            }
        } catch (error) {
            console.error('Bulk admit error:', error);
            showAlert({
                type: 'error',
                title: 'Error',
                text: error.message || 'Failed to process bulk admission'
            });

            // Reset button
            this.disabled = false;
            this.innerHTML = originalText;
        }
    });

    // Add CSS for pagination
    const style = document.createElement('style');
    style.textContent = `
        .pagination-hidden {
            display: none !important;
        }
    `;
    document.head.appendChild(style);

    // Apply initial filters
    applyFilters();
});