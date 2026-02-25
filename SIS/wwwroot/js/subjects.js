document.addEventListener('DOMContentLoaded', function () {
    // Modal elements
    const createSubjectModal = document.getElementById('createSubjectModal');
    const updateSubjectModal = document.getElementById('updateSubjectModal');
    const deleteSubjectModal = document.getElementById('deleteSubjectModal');

    // Form elements
    const createSubjectForm = document.getElementById('createSubjectForm');
    const updateSubjectForm = document.getElementById('updateSubjectForm');
    const deleteSubjectForm = document.getElementById('deleteSubjectForm');

    // Button elements
    const confirmCreateBtn = document.getElementById('confirmCreate');
    const confirmUpdateBtn = document.getElementById('confirmUpdate');
    const confirmDeleteBtn = document.getElementById('confirmDelete');

    // Mode selection fields
    const singleSubjectFields = document.getElementById('singleSubjectFields');
    const bulkSubjectFields = document.getElementById('bulkSubjectFields');
    const modeRadios = document.querySelectorAll('input[name="mode"]');

    // Handle mode switching
    modeRadios.forEach(radio => {
        radio.addEventListener('change', () => {
            if (radio.value === 'single') {
                singleSubjectFields.classList.remove('hidden');
                bulkSubjectFields.classList.add('hidden');
                // Reset file input
                document.getElementById('fileUpload').value = '';
            } else {
                singleSubjectFields.classList.add('hidden');
                bulkSubjectFields.classList.remove('hidden');
                // Reset single input fields
                document.getElementById('subjectName').value = '';
                document.getElementById('subjectCode').value = '';
            }
        });
    });

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

        // Close button handlers
        const closeButtons = modalElement.querySelectorAll('[data-bs-dismiss="modal"]');
        closeButtons.forEach(button => {
            button.addEventListener('click', () => hideModal(modalElement));
        });
    }

    // Setup close handlers for all modals
    [createSubjectModal, updateSubjectModal, deleteSubjectModal].forEach(setupModalCloseHandlers);

    // Show create modal
    window.showCreateSubjectModal = function () {
        createSubjectForm.reset();
        // Reset to single mode by default
        document.querySelector('input[name="mode"][value="single"]').checked = true;
        singleSubjectFields.classList.remove('hidden');
        bulkSubjectFields.classList.add('hidden');
        showModal(createSubjectModal);
    };

    // Show update modal
    window.showUpdateSubjectModal = function (id, name, code) {
        document.getElementById('updateSubjectId').value = id;
        document.getElementById('updateSubjectName').value = name;
        document.getElementById('updateSubjectCode').value = code;
        showModal(updateSubjectModal);
    };

    // Show delete modal
    window.showDeleteSubjectModal = function (id, name) {
        document.getElementById('deleteSubjectId').value = id;
        document.getElementById('deleteSubjectName').textContent = name;
        showModal(deleteSubjectModal);
    };

    // Handle form submissions
    confirmCreateBtn.addEventListener('click', function () {
        const mode = document.querySelector('input[name="mode"]:checked').value;

        if (mode === 'single') {
            // Validate single subject form
            if (!document.getElementById('subjectName').value ||
                !document.getElementById('subjectCode').value) {
                alert('Please fill in all required fields.');
                return;
            }
        } else {
            // Validate file upload
            const fileInput = document.getElementById('fileUpload');
            if (!fileInput.files.length) {
                alert('Please select a file to upload.');
                return;
            }

            const fileName = fileInput.files[0].name;
            const fileExt = fileName.split('.').pop().toLowerCase();

            if (!['csv', 'xlsx', 'xls'].includes(fileExt)) {
                alert('Please upload a valid CSV or Excel file.');
                return;
            }
        }

        if (createSubjectForm.checkValidity()) {
            createSubjectForm.submit();
        } else {
            createSubjectForm.reportValidity();
        }
    });

    confirmUpdateBtn.addEventListener('click', function () {
        if (updateSubjectForm.checkValidity()) {
            updateSubjectForm.submit();
        } else {
            updateSubjectForm.reportValidity();
        }
    });

    confirmDeleteBtn.addEventListener('click', function () {
        deleteSubjectForm.submit();
    });

    // Handle Escape key to close modals
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') {
            [createSubjectModal, updateSubjectModal, deleteSubjectModal].forEach(modal => {
                if (!modal.classList.contains('hidden')) {
                    hideModal(modal);
                }
            });
        }
    });

    // Initialize Subject Distribution Chart
    const subjectData = [
        { subject: "Mathematics", students: 450 },
        { subject: "Physics", students: 320 },
        { subject: "Chemistry", students: 280 },
        { subject: "Biology", students: 240 },
        { subject: "English", students: 380 }
    ];

    const subjectStatsChart = new ApexCharts(document.querySelector("#subjectStatsChart"), {
        series: [{
            name: 'Enrolled Students',
            data: subjectData.map(item => item.students)
        }],
        chart: {
            type: 'bar',
            height: '100%',
            toolbar: {
                show: false
            }
        },
        plotOptions: {
            bar: {
                borderRadius: 4,
                horizontal: true,
            }
        },
        dataLabels: {
            enabled: false
        },
        xaxis: {
            categories: subjectData.map(item => item.subject),
        },
        colors: ['#4F46E5'],
        tooltip: {
            y: {
                formatter: function (val) {
                    return val + " students"
                }
            }
        }
    });

    subjectStatsChart.render();

    // Initialize DataTable
    const dataTable = new simpleDatatables.DataTable("#subjectsTable", {
        perPage: 15,
        searchable: true,
        sortable: true,
        labels: {
            placeholder: "Search...",
            perPage: "Show {select} entries",
            noRows: "No subjects found",
            info: "Showing {start} to {end} of {rows} subjects",
        },
        classes: {
            wrapper: "datatable-wrapper",
            input: "datatable-input px-3 py-2 text-sm border border-gray-300 rounded-lg",
            selector: "datatable-selector px-3 py-2 text-sm border border-gray-300 rounded-lg",
            paginationButton: "px-3 py-1 text-sm text-gray-600 hover:bg-gray-100 rounded-md",
            paginationButtonActive: "bg-blue-50 text-blue-600 hover:bg-blue-100",
            paginationButtonDisabled: "text-gray-400 hover:bg-transparent cursor-not-allowed",
        }
    });
});