document.addEventListener('DOMContentLoaded', function () {
    // Modal elements
    const createRoomModal = document.getElementById('createRoomModal');
    const updateRoomModal = document.getElementById('updateRoomModal');
    const deleteRoomModal = document.getElementById('deleteRoomModal');

    // Form elements
    const createRoomForm = document.getElementById('createRoomForm');
    const updateRoomForm = document.getElementById('updateRoomForm');
    const deleteRoomForm = document.getElementById('deleteRoomForm');

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

        // Close button handlers
        const closeButtons = modalElement.querySelectorAll('[data-bs-dismiss="modal"]');
        closeButtons.forEach(button => {
            button.addEventListener('click', () => hideModal(modalElement));
        });
    }

    // Setup close handlers for all modals
    [createRoomModal, updateRoomModal, deleteRoomModal].forEach(setupModalCloseHandlers);

    // Show create modal
    window.showCreateRoomModal = function () {
        createRoomForm.reset();
        showModal(createRoomModal);
    };

    // Show update modal
    window.showUpdateRoomModal = function (id, name, buildingId, roomType, learningCapacity, examCapacity, area, description) {
        document.getElementById('updateRoomId').value = id;
        document.getElementById('updateRoomName').value = name;
        document.getElementById('updateBuildingId').value = buildingId;
        document.getElementById('updateRoomType').value = roomType;
        document.getElementById('updateLearningCapacity').value = learningCapacity;
        document.getElementById('updateExamCapacity').value = examCapacity;
        document.getElementById('updateArea').value = area;
        document.getElementById('updateDescription').value = description;
        showModal(updateRoomModal);
    };

    // Show delete modal
    window.showDeleteRoomModal = function (id, name) {
        document.getElementById('deleteRoomId').value = id;
        document.getElementById('deleteRoomName').textContent = name;
        showModal(deleteRoomModal);
    };

    // Handle form submissions
    confirmCreateBtn.addEventListener('click', function () {
        if (createRoomForm.checkValidity()) {
            createRoomForm.submit();
        } else {
            createRoomForm.reportValidity();
        }
    });

    confirmUpdateBtn.addEventListener('click', function () {
        if (updateRoomForm.checkValidity()) {
            updateRoomForm.submit();
        } else {
            updateRoomForm.reportValidity();
        }
    });

    confirmDeleteBtn.addEventListener('click', function () {
        deleteRoomForm.submit();
    });

    // Handle Escape key to close modals
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') {
            [createRoomModal, updateRoomModal, deleteRoomModal].forEach(modal => {
                if (!modal.classList.contains('hidden')) {
                    hideModal(modal);
                }
            });
        }
    });

    // Make the table responsive
    window.addEventListener('resize', () => {
        dataTable.columns().rebuild();
    });

    // Initialize Room Statistics Chart
    const roomData = [
        { type: "Lecture Hall", count: 8 },
        { type: "Laboratory", count: 5 },
        { type: "Computer Lab", count: 4 },
        { type: "Seminar Room", count: 6 },
        { type: "Workshop", count: 3 }
    ];

    const roomStatsChart = new ApexCharts(document.querySelector("#roomStatsChart"), {
        series: roomData.map(item => item.count),
        chart: {
            type: 'donut',
            height: '100%'
        },
        labels: roomData.map(item => item.type),
        colors: ['#4F46E5', '#10B981', '#F59E0B', '#EF4444', '#8B5CF6'],
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
                                return val + ' rooms'
                            }
                        },
                        total: {
                            show: true,
                            label: 'Total Rooms',
                            color: '#373d3f',
                            formatter: function (w) {
                                const total = w.globals.seriesTotals.reduce((a, b) => a + b, 0)
                                return total + ' rooms'
                            }
                        }
                    }
                }
            }
        },
        legend: {
            show: false
        },
        dataLabels: {
            enabled: false
        },
        responsive: [{
            breakpoint: 480,
            options: {
                chart: {
                    height: 200
                }
            }
        }]
    });

    roomStatsChart.render(); 

    // Initialize DataTable
    const dataTable = new simpleDatatables.DataTable("#roomsTable", {
        perPage: 10,
        searchable: true,
        sortable: true,
        labels: {
            placeholder: "Search...",
            perPage: "Show {select} entries",
            noRows: "No learning rooms found",
            info: "Showing {start} to {end} of {rows} learning rooms",
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