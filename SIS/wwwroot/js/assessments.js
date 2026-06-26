document.addEventListener('DOMContentLoaded', function () {
    // Modal elements
    const createAssessmentModal = document.getElementById('createAssessmentModal');
    const updateAssessmentModal = document.getElementById('updateAssessmentModal');
    const deleteAssessmentModal = document.getElementById('deleteAssessmentModal');

    // Form elements
    const createAssessmentForm = document.getElementById('createAssessmentForm');
    const updateAssessmentForm = document.getElementById('updateAssessmentForm');
    const deleteAssessmentForm = document.getElementById('deleteAssessmentForm');

    // Modal handling functions
    function showModal(modalElement) {
        modalElement.classList.remove('hidden');
        document.body.style.overflow = 'hidden';
    }

    function hideModal(modalElement) {
        modalElement.classList.add('hidden');
        document.body.style.overflow = 'auto';
    }

    // Close modal when clicking outside
    function setupModalCloseHandlers(modalElement) {
        modalElement.addEventListener('click', (e) => {
            if (e.target === modalElement) {
                hideModal(modalElement);
            }
        });

        const closeButtons = modalElement.querySelectorAll('[data-bs-dismiss="modal"]');
        closeButtons.forEach(button => {
            button.addEventListener('click', () => hideModal(modalElement));
        });
    }

    // Setup close handlers for all modals
    [createAssessmentModal, updateAssessmentModal, deleteAssessmentModal].forEach(setupModalCloseHandlers);

    // Show/hide submission fields for create form
    const requiresSubmissionCheckbox = document.getElementById('requiresSubmission');
    if (requiresSubmissionCheckbox) {
        requiresSubmissionCheckbox.addEventListener('change', function () {
            const submissionSection = document.querySelector('.submission-section');
            submissionSection.classList.toggle('hidden', !this.checked);
        });
    }

    // Show/hide submission fields for update form
    const updateRequiresSubmissionCheckbox = document.getElementById('updateRequiresSubmission');
    if (updateRequiresSubmissionCheckbox) {
        updateRequiresSubmissionCheckbox.addEventListener('change', function () {
            const submissionSection = document.querySelector('.update-submission-section');
            submissionSection.classList.toggle('hidden', !this.checked);
        });
    }

    // Show/hide resit fields for create form
    const allowResitCheckbox = document.getElementById('allowResit');
    if (allowResitCheckbox) {
        allowResitCheckbox.addEventListener('change', function () {
            const resitSection = document.querySelector('.resit-section');
            resitSection.classList.toggle('hidden', !this.checked);
        });
    }

    // Show/hide resit fields for update form
    const updateAllowResitCheckbox = document.getElementById('updateAllowResit');
    if (updateAllowResitCheckbox) {
        updateAllowResitCheckbox.addEventListener('change', function () {
            const resitSection = document.querySelector('.update-resit-section');
            resitSection.classList.toggle('hidden', !this.checked);
        });
    }

    // Modal show functions
    window.showCreateAssessmentModal = function () {
        createAssessmentForm.reset();
        showModal(createAssessmentModal);
    };

    window.showUpdateAssessmentModal = async function (id) {
        try {
            console.log('Fetching assessment with ID:', id);
            const response = await fetch(`/Admin/GetAssessment/${id}`);

            if (!response.ok) throw new Error('Network response was not ok');

            const assessment = await response.json();
            console.log('Assessment data:', assessment);

            // Safely set form values with error checking
            const setFormValue = (id, value) => {
                const element = document.getElementById(id);
                if (element) {
                    if (element.type === 'checkbox') {
                        element.checked = !!value; // Convert to boolean
                    } else {
                        element.value = value || '';
                    }
                } else {
                    console.warn(`Element with ID '${id}' not found`);
                }
            };

            // Format date properly if it exists
            let formattedDate = '';
            if (assessment.dueDate) {
                // Handle different date formats
                const dueDate = new Date(assessment.dueDate);
                if (!isNaN(dueDate.getTime())) {
                    // Format as YYYY-MM-DDThh:mm
                    formattedDate = dueDate.toISOString().slice(0, 16);
                }
            }

            // Set values with safe handling
            setFormValue('updateAssessmentId', assessment.id);
            setFormValue('updateName', assessment.name);
            setFormValue('updateType', assessment.type);
            setFormValue('updateWeightPercentage', assessment.weightPercentage);
            setFormValue('updatePassMark', assessment.passMark);
            setFormValue('updateDescription', assessment.description);
            setFormValue('updateRequiresSubmission', assessment.requiresSubmission);
            setFormValue('updateDueDate', formattedDate);

            // Check if these elements exist before trying to set values
            if (document.getElementById('updateSubmissionInstructions')) {
                setFormValue('updateSubmissionInstructions', assessment.submissionInstructions);
            }

            setFormValue('updateAllowResit', assessment.allowResit);
            setFormValue('updateMaximumResitMark', assessment.maximumResitMark);
            setFormValue('updateIsActive', assessment.isActive);

            // Toggle sections based on checkboxes
            // Make sure we're using the class that actually exists in your HTML
            const resitSection = document.querySelector('.resit-section'); // Using the class from your HTML
            if (resitSection) {
                resitSection.classList.toggle('hidden', !assessment.allowResit);
            }

            // For submission instructions section - check what exists in your HTML
            // Your code was looking for '.update-submission-section' which might not exist
            const submissionSection = document.querySelector('.submission-section');
            if (submissionSection) {
                submissionSection.classList.toggle('hidden', !assessment.requiresSubmission);
            }

            // Show the modal
            showModal(updateAssessmentModal);
        } catch (error) {
            console.error('Detailed error:', error);
            window.showAppToast(`Error fetching assessment details: ${error.message}`);
        }
    }

    window.showDeleteAssessmentModal = function (id, name) {
        document.getElementById('deleteAssessmentId').value = id;
        document.getElementById('deleteAssessmentName').textContent = name;
        showModal(deleteAssessmentModal);
    };

    // Form submissions
    const confirmCreate = document.getElementById('confirmCreate');
    if (confirmCreate) {
        confirmCreate.addEventListener('click', function () {
            if (createAssessmentForm.checkValidity()) {
                createAssessmentForm.submit();
            } else {
                createAssessmentForm.reportValidity();
            }
        });
    }

    const confirmUpdate = document.getElementById('confirmUpdate');
    if (confirmUpdate) {
        confirmUpdate.addEventListener('click', function () {
            if (updateAssessmentForm.checkValidity()) {
                updateAssessmentForm.submit();
            } else {
                updateAssessmentForm.reportValidity();
            }
        });
    }

    const confirmDelete = document.getElementById('confirmDelete');
    if (confirmDelete) {
        confirmDelete.addEventListener('click', function () {
            deleteAssessmentForm.submit();
        });
    }

    // Initialize DataTable
    const dataTable = new simpleDatatables.DataTable("#assessmentTable", {
        perPage: 10,
        searchable: true,
        sortable: true,
        labels: {
            placeholder: "Search...",
            perPage: "Show {select} entries",
            noRows: "No assessments found",
            info: "Showing {start} to {end} of {rows} assessments",
        },
        classes: {
            wrapper: "datatable-wrapper",
            input: "datatable-input px-3 py-2 text-sm border border-gray-300 rounded-lg",
            selector: "datatable-selector px-3 py-2 text-sm border border-gray-300 rounded-lg",
            paginationButton: "px-3 py-1 text-sm text-secondary-600 hover:bg-gray-100 rounded-md",
            paginationButtonActive: "bg-blue-50 text-blue-600 hover:bg-blue-100",
            paginationButtonDisabled: "text-gray-400 hover:bg-transparent cursor-not-allowed",
        }
    });

    // Function to count elements containing specific text
    function countElementsWithText(selector, text) {
        const elements = document.querySelectorAll(selector);
        let count = 0;

        elements.forEach(element => {
            if (element.textContent.includes(text)) {
                count++;
            }
        });

        return count;
    }

    // Initialize Distribution Chart
    const chartData = {
        series: [
            countElementsWithText('tr:not(.hidden) td:nth-child(3)', 'Exam'),
            countElementsWithText('tr:not(.hidden) td:nth-child(3)', 'Assignment'),
            countElementsWithText('tr:not(.hidden) td:nth-child(3)', 'Project'),
            countElementsWithText('tr:not(.hidden) td:nth-child(3)', 'Quiz')
        ],
        chart: {
            type: 'donut',
            height: '100%'
        },
        labels: ['Exams', 'Assignments', 'Projects', 'Quizzes'],
        colors: ['#4F46E5', '#10B981', '#F59E0B', '#EF4444'],
        plotOptions: {
            pie: {
                donut: {
                    size: '70%',
                    labels: {
                        show: true,
                        name: {
                            show: true,
                            fontSize: '14px',
                            fontFamily: 'inherit',
                            offsetY: -4
                        },
                        value: {
                            show: true,
                            fontSize: '16px',
                            fontFamily: 'inherit',
                            formatter: function (val) {
                                return val + ' assessments'
                            }
                        },
                        total: {
                            show: true,
                            label: 'Total',
                            color: '#374151',
                            formatter: function (w) {
                                return w.globals.seriesTotals.reduce((a, b) => a + b, 0) + ' assessments'
                            }
                        }
                    }
                }
            }
        },
        legend: {
            show: false
        },
        responsive: [{
            breakpoint: 480,
            options: {
                chart: {
                    height: 200
                }
            }
        }]
    };

    const chart = new ApexCharts(document.querySelector("#assessment-distribution-chart"), chartData);
    chart.render();
    console.log('sgvss');
    // Handle Escape key to close modals
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') {
            [createAssessmentModal, updateAssessmentModal, deleteAssessmentModal].forEach(modal => {
                if (!modal.classList.contains('hidden')) {
                    hideModal(modal);
                }
            });
        }
    });
});