document.addEventListener('DOMContentLoaded', function () {
    // Modal elements
    const createGradeConfigModal = document.getElementById('createGradeConfigModal');
    const updateGradeConfigModal = document.getElementById('updateGradeConfigModal');
    const deleteGradeConfigModal = document.getElementById('deleteGradeConfigModal');

    // Form elements
    const createGradeConfigForm = document.getElementById('createGradeConfigForm');
    const updateGradeConfigForm = document.getElementById('updateGradeConfigForm');
    const deleteGradeConfigForm = document.getElementById('deleteGradeConfigForm');

    // Button elements
    const confirmCreateBtn = document.getElementById('confirmCreate');
    const confirmUpdateBtn = document.getElementById('confirmUpdate');
    const confirmDeleteBtn = document.getElementById('confirmDelete');

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
    [createGradeConfigModal, updateGradeConfigModal, deleteGradeConfigModal].forEach(setupModalCloseHandlers);

    // Show create modal
    window.showCreateGradeConfigModal = function () {
        createGradeConfigForm.reset();
        showModal(createGradeConfigModal);
    };

    // Show update modal
    window.showUpdateGradeConfigModal = async function (id) {
        try {
            const response = await fetch(`/Admin/GetGradeConfig/${id}`);
            if (!response.ok) throw new Error('Network response was not ok');
            const grade = await response.json();

            document.getElementById('updateGradeId').value = grade.id;
            document.getElementById('updateGradeLetter').value = grade.gradeLetter;
            document.getElementById('updateMinScore').value = grade.minScore;
            document.getElementById('updateMaxScore').value = grade.maxScore;
            document.getElementById('updateGPAValue').value = grade.gpaValue;
            document.getElementById('updateDescription').value = grade.description;
            document.getElementById('updateIsPassingGrade').checked = grade.isPassingGrade;
            document.getElementById('updateIsActive').checked = grade.isActive;

            showModal(updateGradeConfigModal);
        } catch (error) {
            console.error('Error:', error);
            alert('Error fetching grade configuration details');
        }
    };

    // Show delete modal
    window.showDeleteGradeModal = function (id, gradeLetter) {
        document.getElementById('deleteGradeId').value = id;
        document.getElementById('deleteGradeLetter').textContent = gradeLetter;
        showModal(deleteGradeConfigModal);
    };

    // Form validation and submission handlers
    confirmCreateBtn.addEventListener('click', function () {
        if (createGradeConfigForm.checkValidity()) {
            const minScore = parseFloat(document.getElementById('minScore').value);
            const maxScore = parseFloat(document.getElementById('maxScore').value);

            if (minScore >= maxScore) {
                alert('Minimum score must be less than maximum score');
                return;
            }

            createGradeConfigForm.submit();
        } else {
            createGradeConfigForm.reportValidity();
        }
    });

    confirmUpdateBtn.addEventListener('click', function () {
        if (updateGradeConfigForm.checkValidity()) {
            const minScore = parseFloat(document.getElementById('updateMinScore').value);
            const maxScore = parseFloat(document.getElementById('updateMaxScore').value);

            if (minScore >= maxScore) {
                alert('Minimum score must be less than maximum score');
                return;
            }

            updateGradeConfigForm.submit();
        } else {
            updateGradeConfigForm.reportValidity();
        }
    });

    confirmDeleteBtn.addEventListener('click', function () {
        deleteGradeConfigForm.submit();
    });

    // Handle Escape key to close modals
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') {
            [createGradeConfigModal, updateGradeConfigModal, deleteGradeConfigModal].forEach(modal => {
                if (!modal.classList.contains('hidden')) {
                    hideModal(modal);
                }
            });
        }
    });

    // Initialize DataTable
    const dataTable = new simpleDatatables.DataTable("#gradeConfigTable", {
        perPage: 10,
        searchable: true,
        sortable: true,
        labels: {
            placeholder: "Search...",
            perPage: "Show {select} entries",
            noRows: "No grade configurations found",
            info: "Showing {start} to {end} of {rows} grades",
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

    // Initialize Distribution Chart
    const chartOptions = {
        series: [
            document.querySelectorAll('span.bg-green-100').length, // Active grades
            document.querySelectorAll('span.bg-red-100').length    // Inactive grades
        ],
        chart: {
            type: 'donut',
            height: '90%'
        },
        labels: ['Active Grades', 'Inactive Grades'],
        colors: ['#10B981', '#EF4444'],
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
                                return val + ' grades'
                            }
                        },
                        total: {
                            show: true,
                            label: 'Total Grades',
                            color: '#374151',
                            formatter: function (w) {
                                return w.globals.seriesTotals.reduce((a, b) => a + b, 0) + ' grades'
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

    const chart = new ApexCharts(document.querySelector("#grade-distribution-chart"), chartOptions);
    chart.render();
});

// Additional input validation
function validateScoreRange(minInput, maxInput) {
    const min = parseFloat(minInput.value);
    const max = parseFloat(maxInput.value);

    if (min >= max) {
        minInput.setCustomValidity('Minimum score must be less than maximum score');
    } else {
        minInput.setCustomValidity('');
    }
    minInput.reportValidity();
}