document.addEventListener('DOMContentLoaded', function () {
    // Initialize DataTable
    const dataTable = new simpleDatatables.DataTable("#schoolsTable", {
        perPage: 15,
        searchable: true,
        sortable: true,
        tableRender: (_data, table, type) => {
            if (type === "print") {
                return table
            }
            const tHead = table.childNodes[0]
            const filterHeaders = {
                nodeName: "TR",
                attributes: {
                    class: "search-filtering-row"
                },
                //childNodes: tHead.childNodes[0].childNodes.map(
                //    (_th, index) => {
                //        // Don't add search input for the Actions column
                //        if (index === 3) {
                //            return { nodeName: "TH" }
                //        }
                //        return {
                //            nodeName: "TH",
                //            childNodes: [
                //                {
                //                    nodeName: "INPUT",
                //                    attributes: {
                //                        class: "datatable-input px-3 py-2 text-sm border border-gray-300 rounded-lg w-full",
                //                        type: "search",
                //                        placeholder: "Search...",
                //                        "data-columns": "[" + index + "]"
                //                    }
                //                }
                //            ]
                //        }
                //    }
                //)
            }
            tHead.childNodes.push(filterHeaders)
            return table
        },
        labels: {
            placeholder: "Search...",
            perPage: "Show {select} entries",
            noRows: "No schools found",
            info: "Showing {start} to {end} of {rows} schools",
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

    // Make the table responsive
    window.addEventListener('resize', () => {
        dataTable.columns().rebuild();
    });

    // Modal elements
    const createSchoolModal = document.getElementById('createSchoolModal');
    const updateSchoolModal = document.getElementById('updateSchoolModal');
    const deleteSchoolModal = document.getElementById('deleteSchoolModal');

    // Form elements
    const createSchoolForm = document.getElementById('createSchoolForm');
    const updateSchoolForm = document.getElementById('updateSchoolForm');
    const deleteSchoolForm = document.getElementById('deleteSchoolForm');

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
    [createSchoolModal, updateSchoolModal, deleteSchoolModal].forEach(setupModalCloseHandlers);

    // Function to toggle between the modes based on the selected radio button
    function toggleMode() {
        const selectedMode = document.querySelector('input[name="mode"]:checked').value;
        if (selectedMode === 'single') {
            document.getElementById('singleSchool').classList.remove('hidden');
            document.getElementById('bulkSchool').classList.add('hidden');
        } else if (selectedMode === 'bulk') {
            document.getElementById('singleSchool').classList.add('hidden');
            document.getElementById('bulkSchool').classList.remove('hidden');
        }
    }

    // Add event listeners to handle mode changes (single / bulk)
    document.querySelectorAll('input[name="mode"]').forEach(radio => {
        radio.addEventListener('change', toggleMode);
    });

    // Show create modal
    window.showCreateSchoolModal = function () {
        createSchoolForm.reset();
        showModal(createSchoolModal);
        toggleMode(); // Initially set the mode
    };

    // Show update modal
    window.showUpdateSchoolModal = async function (id) {
        // Fetch school data
        fetch(`/Admin/GetSchool/${id}`)
            .then(response => response.json())
            .then(data => {
                document.getElementById('updateSchoolId').value = data.id;
                document.getElementById('updateSchoolName').value = data.name;
                document.getElementById('updateDescription').value = data.description;

                // Set Dean and Assistant Dean dropdown values
                if (data.deanId) {
                    document.getElementById('updateDeanId').value = data.deanId;
                }

                if (data.assistantDeanId) {
                    document.getElementById('updateAssistantDeanId').value = data.assistantDeanId;
                }

                // Show the modal
                document.getElementById('updateSchoolModal').classList.remove('hidden');
            })
            .catch(error => console.error('Error fetching school data:', error));


    };

    // Show delete modal
    window.showDeleteSchoolModal = function (id, name) {
        document.getElementById('deleteSchoolId').value = id;
        document.getElementById('deleteSchoolName').textContent = name;
        showModal(deleteSchoolModal);
    };

    // Handle form submissions
    confirmCreateBtn.addEventListener('click', function () {
        if (createSchoolForm.checkValidity()) {
            createSchoolForm.submit();
        } else {
            createSchoolForm.reportValidity();
        }
    });

    confirmUpdateBtn.addEventListener('click', function () {
        if (updateSchoolForm.checkValidity()) {
            updateSchoolForm.submit();
        } else {
            updateSchoolForm.reportValidity();
        }
    });

    confirmDeleteBtn.addEventListener('click', function () {
        deleteSchoolForm.submit();
    });

    // Handle Escape key to close modals
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') {
            [createSchoolModal, updateSchoolModal, deleteSchoolModal].forEach(modal => {
                if (!modal.classList.contains('hidden')) {
                    hideModal(modal);
                }
            });
        }
    });

    // File upload UI enhancement
    const fileInput = document.getElementById('file');
    if (fileInput) {
        const fileLabel = fileInput.nextElementSibling;
        fileInput.addEventListener('change', function (e) {
            if (e.target.files.length > 0) {
                fileLabel.textContent = e.target.files[0].name;
            } else {
                fileLabel.textContent = 'Upload a file';
            }
        });
    }

    // Initialize School Statistics Chart with dynamic data
    // Check if studentDistributionData exists (passed from server)
    if (typeof studentDistributionData !== 'undefined' && studentDistributionData && studentDistributionData.length > 0) {
        const chartData = {
            series: studentDistributionData.map(item => item.studentCount),
            labels: studentDistributionData.map(item => item.schoolName)
        };

        const schoolStatsChart = new ApexCharts(document.querySelector("#schoolStatsChart"), {
            series: chartData.series,
            chart: {
                type: 'donut',
                height: '100%'
            },
            labels: chartData.labels,
            colors: ['#4F46E5', '#10B981', '#F59E0B', '#EF4444', '#8B5CF6'],
            legend: {
                show: false  // This disables the legend
            },
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
                                    return val
                                }
                            },
                            total: {
                                show: true,
                                label: 'Total Students',
                                color: '#373d3f',
                                formatter: function (w) {
                                    return w.globals.seriesTotals.reduce((a, b) => a + b, 0)
                                }
                            }
                        }
                    }
                }
            },
            dataLabels: {
                enabled: false
            },
            responsive: [{
                breakpoint: 480,
                options: {
                    chart: {
                        height: 200
                    },
                    legend: {
                        show: false
                    }
                }
            }]
        });

        schoolStatsChart.render();
    } else {
        // Show a message if no data is available
        document.querySelector("#schoolStatsChart").innerHTML = '<div class="flex items-center justify-center h-32 text-gray-500"><p>No student data available</p></div>';
    }
});