/**
 * Student Accommodation Dashboard JavaScript
 * Handles functionality for the accommodation dashboard including:
 * - Loading recent applications
 * - Maintenance issue reporting
 * - Modal functionality
 */

document.addEventListener('DOMContentLoaded', function () {
    // Load recent applications via AJAX
    loadRecentApplications();

    // Report Issue Modal Functionality
    setupIssueReportingSystem();
});

/**
 * Load the student's recent accommodation applications
 */
function loadRecentApplications() {
    // Get the container
    const recentApplicationsContainer = document.getElementById('recentApplications');

    // Ensure the container exists
    if (!recentApplicationsContainer) return;

    // Display loading indicator
    recentApplicationsContainer.innerHTML = `
        <div class="flex justify-center p-8">
            <div class="animate-spin rounded-full h-12 w-12 border-b-2 border-cyan-950"></div>
        </div>
    `;

    // Fetch recent applications from the server
    fetch('/StudentAccommodation/GetRecentApplications')
        .then(response => {
            if (!response.ok) {
                throw new Error('Network response was not ok');
            }
            return response.json();
        })
        .then(data => {
            if (!data || data.length === 0) {
                // Handle no applications
                recentApplicationsContainer.innerHTML = `
                    <div class="flex items-center justify-center p-6 bg-slate-50 rounded-lg">
                        <div class="text-center">
                            <i class="material-icons text-slate-400 text-3xl mb-2">history</i>
                            <p class="text-slate-600">No application history found</p>
                        </div>
                    </div>
                `;
                return;
            }

            // Start building HTML for applications
            let html = '<div class="space-y-4">';

            // Process each application
            data.forEach(app => {
                // Determine status styling
                let statusClass, statusIcon;

                switch (app.status) {
                    case 'Pending':
                        statusClass = 'bg-amber-100 text-amber-800';
                        statusIcon = 'hourglass_empty';
                        break;
                    case 'Approved':
                        statusClass = 'bg-green-100 text-green-800';
                        statusIcon = 'check_circle';
                        break;
                    case 'Rejected':
                        statusClass = 'bg-red-100 text-red-800';
                        statusIcon = 'cancel';
                        break;
                    case 'Waitlisted':
                        statusClass = 'bg-blue-100 text-blue-800';
                        statusIcon = 'pending';
                        break;
                    case 'Canceled':
                        statusClass = 'bg-slate-100 text-slate-800';
                        statusIcon = 'highlight_off';
                        break;
                    default:
                        statusClass = 'bg-slate-100 text-slate-800';
                        statusIcon = 'help';
                }

                // Format the application date
                const applicationDate = new Date(app.applicationDate).toLocaleDateString();

                // Add application to HTML
                html += `
                    <div class="flex justify-between items-start p-4 border border-slate-200 rounded-lg hover:border-blue-300 hover:shadow-sm transition-colors">
                        <div class="flex items-start gap-3">
                            <div class="bg-cyan-950 p-2 text-white rounded">
                                <i class="material-icons">home</i>
                            </div>
                            <div>
                                <h4 class="font-medium text-slate-800">${app.periodName}</h4>
                                <p class="text-sm text-slate-600">Applied: ${applicationDate}</p>
                            </div>
                        </div>
                        <div class="flex items-center">
                            <span class="inline-flex items-center px-2 py-1 rounded ${statusClass} text-xs">
                                <i class="material-icons text-xs mr-1">${statusIcon}</i>
                                ${app.status}
                            </span>
                        </div>
                    </div>
                `;
            });

            // Close HTML container
            html += '</div>';

            // Update the container with the applications
            recentApplicationsContainer.innerHTML = html;
        })
        .catch(error => {
            console.error('Error loading recent applications:', error);
            recentApplicationsContainer.innerHTML = `
                <div class="flex items-center gap-2 p-4 bg-red-50 text-red-800 rounded-lg">
                    <i class="material-icons">error</i>
                    <p>Error loading recent applications. Please try refreshing the page.</p>
                </div>
            `;
        });
}

/**
 * Setup the maintenance issue reporting system
 */
function setupIssueReportingSystem() {
    // Get modal elements
    const reportIssueBtn = document.getElementById('reportIssueBtn');
    const reportIssueModal = document.getElementById('reportIssueModal');
    const closeIssueModal = document.getElementById('closeIssueModal');
    const cancelIssueReport = document.getElementById('cancelIssueReport');
    const reportIssueForm = document.getElementById('reportIssueForm');

    // Success modal elements
    const successModal = document.getElementById('successModal');
    const closeSuccessModal = document.getElementById('closeSuccessModal');

    // Only setup if elements exist (student is accommodated)
    if (!reportIssueBtn || !reportIssueModal) return;

    // Create success modal if it doesn't exist
    if (!successModal) {
        createSuccessModal();
    }

    // Show report issue modal
    reportIssueBtn.addEventListener('click', function () {
        reportIssueModal.classList.remove('hidden');
    });

    // Hide report issue modal on close button click
    if (closeIssueModal) {
        closeIssueModal.addEventListener('click', function () {
            reportIssueModal.classList.add('hidden');
        });
    }

    // Hide report issue modal on cancel button click
    if (cancelIssueReport) {
        cancelIssueReport.addEventListener('click', function () {
            reportIssueModal.classList.add('hidden');
        });
    }

    // Handle form submission
    if (reportIssueForm) {
        reportIssueForm.addEventListener('submit', function (e) {
            e.preventDefault();

            // Get form data
            const issueType = document.getElementById('issueType').value;
            const issuePriority = document.querySelector('input[name="issuePriority"]:checked')?.value || 'medium';
            const issueDescription = document.getElementById('issueDescription').value;

            // Form validation
            if (!issueType) {
                showFormError('Please select an issue type');
                return;
            }

            if (!issueDescription) {
                showFormError('Please provide a description of the issue');
                return;
            }

            // Get the CSRF token
            const token = document.querySelector('input[name="__RequestVerificationToken"]').value;

            // Submit maintenance request via AJAX
            fetch('/StudentAccommodation/ReportIssue', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token
                },
                body: JSON.stringify({
                    issueType: issueType,
                    priority: issuePriority,
                    description: issueDescription
                })
            })
                .then(response => {
                    if (!response.ok) {
                        throw new Error('Network response was not ok');
                    }
                    return response.json();
                })
                .then(data => {
                    // Hide the report issue modal
                    reportIssueModal.classList.add('hidden');

                    // Show success modal
                    showSuccessModal();

                    // Reset form
                    reportIssueForm.reset();
                })
                .catch(error => {
                    console.error('Error:', error);
                    showFormError('An error occurred while submitting your report. Please try again.');
                });
        });
    }

    // Close success modal when close button is clicked
    if (closeSuccessModal) {
        closeSuccessModal.addEventListener('click', function () {
            document.getElementById('successModal').classList.add('hidden');
        });
    }
}

/**
 * Create the success modal if it doesn't exist
 */
function createSuccessModal() {
    // Create modal element
    const modal = document.createElement('div');
    modal.id = 'successModal';
    modal.className = 'fixed inset-0 z-50 flex items-center justify-center hidden';

    // Set modal HTML
    modal.innerHTML = `
        <div class="absolute inset-0 bg-black bg-opacity-50"></div>
        <div class="bg-white rounded-xl shadow-xl p-6 w-full max-w-md relative z-10 mx-4">
            <div class="flex flex-col items-center text-center">
                <div class="w-16 h-16 bg-green-100 rounded-full flex items-center justify-center mb-4">
                    <i class="material-icons text-green-600 text-3xl">check_circle</i>
                </div>
                <h3 class="text-xl font-semibold text-slate-800 mb-2">Success!</h3>
                <p class="text-slate-600 mb-6">Your issue has been reported successfully. Maintenance staff will address it soon.</p>
                <button type="button" id="closeSuccessModal" class="px-6 py-2 bg-cyan-950 text-white rounded-lg hover:bg-cyan-900 transition-colors">
                    Close
                </button>
            </div>
        </div>
    `;

    // Add modal to the document
    document.body.appendChild(modal);

    // Add event listener for close button
    document.getElementById('closeSuccessModal').addEventListener('click', function () {
        document.getElementById('successModal').classList.add('hidden');
    });
}

/**
 * Show the success modal
 */
function showSuccessModal() {
    const successModal = document.getElementById('successModal');

    // Create the modal if it doesn't exist
    if (!successModal) {
        createSuccessModal();
    }

    // Show the modal
    document.getElementById('successModal').classList.remove('hidden');
}

/**
 * Show form validation error
 * @param {string} message - The error message to display
 */
function showFormError(message) {
    // Check if an error message already exists
    let errorElement = document.getElementById('issueFormError');

    // Create error element if it doesn't exist
    if (!errorElement) {
        errorElement = document.createElement('div');
        errorElement.id = 'issueFormError';
        errorElement.className = 'flex items-center gap-2 p-3 bg-red-50 text-red-700 rounded-lg mb-4';

        // Get the form
        const form = document.getElementById('reportIssueForm');

        // Insert error at the top of the form
        form.insertBefore(errorElement, form.firstChild);
    }

    // Set error message
    errorElement.innerHTML = `
        <i class="material-icons text-red-700">error</i>
        <p>${message}</p>
    `;

    // Auto-hide after 5 seconds
    setTimeout(() => {
        if (errorElement && errorElement.parentNode) {
            errorElement.parentNode.removeChild(errorElement);
        }
    }, 5000);
}