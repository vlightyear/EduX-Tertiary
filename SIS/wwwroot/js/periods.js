document.addEventListener('DOMContentLoaded', function () {
    // Initialize variables and elements
    const searchInput = document.getElementById('searchInput');
    const filterStatus = document.getElementById('filterStatus');
    const periodsTable = document.getElementById('periodsTable');
    const periodsTableBody = document.getElementById('periodsTableBody');
    const emptyStateMessage = document.getElementById('emptyStateMessage');
    const totalPeriods = document.getElementById('totalPeriods');
    const activePeriods = document.getElementById('activePeriods');
    const upcomingPeriods = document.getElementById('upcomingPeriods');

    // Modal elements
    const createPeriodModal = document.getElementById('createPeriodModal');
    const updatePeriodModal = document.getElementById('updatePeriodModal');
    const deletePeriodModal = document.getElementById('deletePeriodModal');

    // Form elements
    const createPeriodForm = document.getElementById('createPeriodForm');
    const updatePeriodForm = document.getElementById('updatePeriodForm');
    const deletePeriodForm = document.getElementById('deletePeriodForm');

    // Period filtering
    searchInput.addEventListener('input', filterTable);
    filterStatus.addEventListener('change', filterTable);

    // Setup sorting buttons
    document.querySelectorAll('.sort-btn').forEach(button => {
        button.addEventListener('click', function () {
            const sortKey = this.getAttribute('data-sort');
            sortTable(sortKey);
        });
    });

    // Initialize chart if we have data
    if (periodsTableBody.children.length > 0) {
        initializePeriodStatusChart();
    }

    // Table filtering function
    function filterTable() {
        const searchTerm = searchInput.value.toLowerCase();
        const filterValue = filterStatus.value;
        let visibleRows = 0;
        let visibleActive = 0;
        let visibleUpcoming = 0;
        let visibleClosed = 0;

        // Get all rows
        const rows = periodsTableBody.querySelectorAll('tr');

        rows.forEach(row => {
            const periodInfo = row.children[1].textContent.toLowerCase();
            const year = row.children[2].textContent.toLowerCase();
            const type = row.children[3].textContent.toLowerCase();
            const status = row.getAttribute('data-status');

            // Combine search term and filter conditions
            let showRow = (periodInfo.includes(searchTerm) || year.includes(searchTerm) || type.includes(searchTerm));

            if (filterValue === 'active' && status !== 'active') {
                showRow = false;
            } else if (filterValue === 'upcoming' && status !== 'upcoming') {
                showRow = false;
            } else if (filterValue === 'closed' && status !== 'closed') {
                showRow = false;
            }

            // Show/hide the row
            row.style.display = showRow ? '' : 'none';
            if (showRow) {
                visibleRows++;
                if (status === 'active') {
                    visibleActive++;
                } else if (status === 'upcoming') {
                    visibleUpcoming++;
                } else if (status === 'closed') {
                    visibleClosed++;
                }
            }
        });

        // Update stats for filtered view
        totalPeriods.textContent = visibleRows;
        activePeriods.textContent = visibleActive;
        upcomingPeriods.textContent = visibleUpcoming;

        // Show empty state if no visible rows
        emptyStateMessage.style.display = visibleRows === 0 ? 'flex' : 'none';
        periodsTable.style.display = visibleRows === 0 ? 'none' : '';
    }

    // Table sorting function
    function sortTable(key) {
        const rows = Array.from(periodsTableBody.querySelectorAll('tr'));

        // Define sort functions for different columns
        const sortFunctions = {
            'index': (a, b) => {
                return parseInt(a.children[0].textContent) - parseInt(b.children[0].textContent);
            },
            'name': (a, b) => {
                return a.children[1].textContent.localeCompare(b.children[1].textContent);
            },
            'year': (a, b) => {
                return a.children[2].textContent.localeCompare(b.children[2].textContent);
            },
            'duration': (a, b) => {
                // Extract date from text format "MMM dd, yyyy - MMM dd, yyyy"
                const dateA = new Date(a.children[4].querySelector('span').textContent.split(' - ')[0]);
                const dateB = new Date(b.children[4].querySelector('span').textContent.split(' - ')[0]);
                return dateA - dateB;
            }
        };

        // Sort the rows
        rows.sort(sortFunctions[key]);

        // Check if we need to reverse the order (if already sorted)
        const button = document.querySelector(`.sort-btn[data-sort="${key}"]`);
        if (button.classList.contains('sorted-asc')) {
            rows.reverse();
            button.classList.remove('sorted-asc');
            button.classList.add('sorted-desc');
        } else {
            // Clear all other sort classes
            document.querySelectorAll('.sort-btn').forEach(btn => {
                btn.classList.remove('sorted-asc');
                btn.classList.remove('sorted-desc');
            });
            button.classList.add('sorted-asc');
        }

        // Reorder the table
        periodsTableBody.innerHTML = '';
        rows.forEach(row => periodsTableBody.appendChild(row));
    }

    // Initialize ApexCharts donut chart with period data
    function initializePeriodStatusChart() {
        const chartContainer = document.getElementById('periodStatusChart');

        // Check if we have actual data
        const rows = periodsTableBody.querySelectorAll('tr');
        if (rows.length === 0) {
            chartContainer.innerHTML = `
                <div class="flex flex-col items-center justify-center h-full text-slate-500">
                    <p class="text-sm">Chart data will appear here once periods are added.</p>
                </div>
            `;
            return;
        }

        // Count periods by status
        let activePeriodCount = 0;
        let upcomingPeriodCount = 0;
        let closedPeriodCount = 0;

        rows.forEach(row => {
            const status = row.getAttribute('data-status');
            if (status === 'active') {
                activePeriodCount++;
            } else if (status === 'upcoming') {
                upcomingPeriodCount++;
            } else if (status === 'closed') {
                closedPeriodCount++;
            }
        });

        const options = {
            series: [activePeriodCount, upcomingPeriodCount, closedPeriodCount],
            chart: {
                type: 'donut',
                height: '90%'
            },
            labels: ['Active Periods', 'Upcoming Periods', 'Closed Periods'],
            colors: ['#10B981', '#3B82F6', '#6B7280'],
            plotOptions: {
                pie: {
                    donut: {
                        size: '65%',
                        labels: {
                            show: true,
                            name: {
                                show: true
                            },
                            value: {
                                show: true,
                                formatter: function (val) {
                                    return val;
                                }
                            },
                            total: {
                                show: true,
                                label: 'Total',
                                formatter: function (w) {
                                    return w.globals.seriesTotals.reduce((a, b) => a + b, 0);
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
            }],
            legend: {
                show: false
            }
        };

        // Create the chart
        const chart = new ApexCharts(chartContainer, options);
        chart.render();
    }

    // Modal functions
    window.showCreatePeriodModal = function () {
        createPeriodForm.reset();

        // Set initial state for checkboxes and their dependent fields
        const isPermanentCheckbox = document.getElementById('IsPermanentUntilGraduation');
        const appliesUniversallyCheckbox = document.getElementById('AppliesUniversally');

        if (isPermanentCheckbox) {
            isPermanentCheckbox.checked = false;
            toggleEndDateField(isPermanentCheckbox, 'EndDate');
        }

        if (appliesUniversallyCheckbox) {
            appliesUniversallyCheckbox.checked = false;
            toggleFilterFields(appliesUniversallyCheckbox);
        }

        createPeriodModal.classList.remove('hidden');
    };

    window.hideCreatePeriodModal = function () {
        createPeriodModal.classList.add('hidden');
    };

    window.showUpdatePeriodModal = async function (id) {
        try {
            // Show loading state
            document.getElementById('loadingIndicator').style.display = 'flex';

            const response = await fetch(`/StudentAccommodation/GetPeriod/${id}`);
            if (!response.ok) {
                throw new Error('Failed to fetch period data');
            }

            const period = await response.json();

            // Format dates for HTML date inputs (YYYY-MM-DD)
            const formatDate = (dateStr) => {
                if (!dateStr) return '';
                const date = new Date(dateStr);
                return date.toISOString().split('T')[0];
            };

            // Populate form fields
            document.getElementById('updatePeriodId').value = period.id;
            document.getElementById('updateAcademicYearId').value = period.academicYearId;
            document.getElementById('updateType').value = period.type || '';
            document.getElementById('updateStartDate').value = formatDate(period.startDate);
            document.getElementById('updateEndDate').value = formatDate(period.endDate);
            document.getElementById('updateApplicationStartDate').value = formatDate(period.applicationStartDate);
            document.getElementById('updateApplicationEndDate').value = formatDate(period.applicationEndDate);
            document.getElementById('updateStatus').value = period.status;

            // Set school, programme, and other fields
            // Set school, programme, and other fields
            try {
                if (document.getElementById('updateSchoolId')) {
                    document.getElementById('updateSchoolId').value = period.schoolId || '';
                }

                if (document.getElementById('updateProgrammeId')) {
                    document.getElementById('updateProgrammeId').value = period.programmeId || '';
                }

                if (document.getElementById('updateModeOfStudyId')) {
                    document.getElementById('updateModeOfStudyId').value = period.modeOfStudyId || '';
                }

                if (document.getElementById('updateYearOfStudy')) {
                    document.getElementById('updateYearOfStudy').value = period.yearOfStudy || '';
                }

                if (document.getElementById('updateProgramLevelId')) {
                    document.getElementById('updateProgramLevelId').value = period.programLevelId || '';
                }

                if (document.getElementById('updateFeeConfigurationId')) {
                    document.getElementById('updateFeeConfigurationId').value = period.feeConfigurationId || '';
                }
            } catch (err) {
                console.error("Error setting dropdown values:", err);
            }

            // Set checkboxes
            const isPermanentCheckbox = document.getElementById('updateIsPermanentUntilGraduation');
            if (isPermanentCheckbox) {
                isPermanentCheckbox.checked = period.isPermanentUntilGraduation;
                toggleEndDateField(isPermanentCheckbox, 'updateEndDate');
            }

            const appliesUniversallyCheckbox = document.getElementById('updateAppliesUniversally');
            if (appliesUniversallyCheckbox) {
                appliesUniversallyCheckbox.checked = period.appliesUniversally;
                toggleUpdateFilterFields(appliesUniversallyCheckbox);
            }

            // Show modal
            updatePeriodModal.classList.remove('hidden');
        } catch (error) {
            console.error('Error fetching period details:', error);
            alert('Error loading period details. Please try again.');
        } finally {
            document.getElementById('loadingIndicator').style.display = 'none';
        }
    };

    window.hideUpdatePeriodModal = function () {
        updatePeriodModal.classList.add('hidden');
    };

    window.showDeletePeriodModalFromData = function (button) {
        const id = button.getAttribute('data-id');
        const displayName = button.getAttribute('data-name');
        const applicationCount = button.getAttribute('data-applications');
        showDeletePeriodModal(id, displayName, applicationCount);
    };

    window.showDeletePeriodModal = function (id, displayName, applicationCount) {
        document.getElementById('deletePeriodId').value = id;
        document.getElementById('deletePeriodName').textContent = displayName;

        const appCountNumber = parseInt(applicationCount);
        document.getElementById('deletePeriodApplicationCount').textContent =
            appCountNumber > 0 ? `${applicationCount} Applications` : 'No Applications';

        // Show warning if period has applications and disable delete button
        const hasApplicationsWarning = document.getElementById('hasApplicationsWarning');
        const deleteButton = document.getElementById('deleteButton');

        if (appCountNumber > 0) {
            hasApplicationsWarning.classList.remove('hidden');
            deleteButton.disabled = true;
            deleteButton.classList.add('opacity-50', 'cursor-not-allowed');
        } else {
            hasApplicationsWarning.classList.add('hidden');
            deleteButton.disabled = false;
            deleteButton.classList.remove('opacity-50', 'cursor-not-allowed');
        }

        deletePeriodModal.classList.remove('hidden');
    };

    window.hideDeletePeriodModal = function () {
        deletePeriodModal.classList.add('hidden');
    };

    // Toggle end date field based on permanent until graduation checkbox
    window.toggleEndDateField = function (checkbox, endDateId) {
        const endDateField = document.getElementById(endDateId);
        if (checkbox.checked) {
            endDateField.value = "";
            endDateField.disabled = true;
        } else {
            endDateField.disabled = false;
        }
    };

    // Toggle filter fields based on applies universally checkbox
    window.toggleFilterFields = function (checkbox) {
        const filterFields = document.getElementById('filterFields');
        if (checkbox.checked) {
            filterFields.style.opacity = '0.5';
            filterFields.querySelectorAll('select').forEach(select => {
                select.disabled = true;
                select.value = "";
            });
        } else {
            filterFields.style.opacity = '1';
            filterFields.querySelectorAll('select').forEach(select => {
                select.disabled = false;
            });
        }
    };

    // Toggle update filter fields based on applies universally checkbox
    window.toggleUpdateFilterFields = function (checkbox) {
        const filterFields = document.getElementById('updateFilterFields');
        if (checkbox.checked) {
            filterFields.style.opacity = '0.5';
            filterFields.querySelectorAll('select').forEach(select => {
                select.disabled = true;
                select.value = "";
            });
        } else {
            filterFields.style.opacity = '1';
            filterFields.querySelectorAll('select').forEach(select => {
                select.disabled = false;
            });
        }
    };

    // Toggle status dropdown
    window.toggleStatusDropdown = function (id) {
        const dropdown = document.getElementById(`statusDropdown-${id}`);
        if (dropdown.classList.contains('hidden')) {
            // Close any open dropdowns first
            document.querySelectorAll('[id^="statusDropdown-"]').forEach(el => {
                if (el.id !== `statusDropdown-${id}`) {
                    el.classList.add('hidden');
                }
            });
            dropdown.classList.remove('hidden');
        } else {
            dropdown.classList.add('hidden');
        }
    };

    // Close dropdowns when clicking outside
    document.addEventListener('click', function (e) {
        if (!e.target.closest('[onclick^="toggleStatusDropdown"]')) {
            document.querySelectorAll('[id^="statusDropdown-"]').forEach(el => {
                el.classList.add('hidden');
            });
        }
    });

    // Handle Escape key to close modals
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') {
            hideCreatePeriodModal();
            hideUpdatePeriodModal();
            hideDeletePeriodModal();
        }
    });

    // Function to handle Permanent Until Graduation toggle
    window.handlePermanentToggle = function (checkbox) {
        console.log("IsPermanentUntilGraduation toggled to:", checkbox.checked);
        console.log("Checkbox value that will be sent:", checkbox.checked ? checkbox.value : "nothing (hidden field will send false)");

        // Handle the UI changes (disable the end date field when checked)
        const endDateField = document.getElementById('EndDate');
        if (checkbox.checked) {
            endDateField.value = "";
            endDateField.disabled = true;
        } else {
            endDateField.disabled = false;
        }
    }

    // Function to handle Applies Universally toggle
    window.handleUniversalToggle = function (checkbox) {
        console.log("AppliesUniversally toggled to:", checkbox.checked);
        console.log("Checkbox value that will be sent:", checkbox.checked ? checkbox.value : "nothing (hidden field will send false)");

        // Handle the UI changes (disable filter fields when checked, except FeeConfigurationId)
        const filterFields = document.getElementById('filterFields');
        const feeConfigSelect = document.getElementById('FeeConfigurationId');
        const feeConfigParent = feeConfigSelect ? feeConfigSelect.closest('.group') : null;

        if (checkbox.checked) {
            filterFields.style.opacity = '0.5';

            // First, disable all selects
            filterFields.querySelectorAll('select').forEach(select => {
                select.disabled = true;
                select.value = "";
            });

            // Then, re-enable FeeConfigurationId specifically
            if (feeConfigSelect) {
                feeConfigSelect.disabled = false;
                feeConfigSelect.style.opacity = '1';

                // Find the parent .group element and reset its opacity
                if (feeConfigParent) {
                    feeConfigParent.style.opacity = '1';
                    console.log("FeeConfigurationId parent opacity set to 1");
                }

                console.log("FeeConfigurationId select explicitly enabled");
            } else {
                console.log("FeeConfigurationId select not found!");
            }
        } else {
            filterFields.style.opacity = '1';
            filterFields.querySelectorAll('select').forEach(select => {
                select.disabled = false;
            });
        }
    }

    // Add event listeners when the DOM is loaded
    document.addEventListener('DOMContentLoaded', function () {
        console.log("DOM loaded, initializing checkbox event listeners...");

        // Get the checkbox elements
        const permanentCheckbox = document.getElementById('IsPermanentUntilGraduation');
        const universalCheckbox = document.getElementById('AppliesUniversally');

        if (permanentCheckbox) {
            console.log("IsPermanentUntilGraduation checkbox found, initial state:", permanentCheckbox.checked);
        } else {
            console.log("IsPermanentUntilGraduation checkbox NOT found!");
        }

        if (universalCheckbox) {
            console.log("AppliesUniversally checkbox found, initial state:", universalCheckbox.checked);
        } else {
            console.log("AppliesUniversally checkbox NOT found!");
        }
    });

    

    console.log('Accommodation Periods initialized');
});