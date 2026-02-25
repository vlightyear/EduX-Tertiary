document.addEventListener('DOMContentLoaded', function () {
    // Initialize variables and elements
    const searchInput = document.getElementById('searchInput');
    const filterStatus = document.getElementById('filterStatus');
    const campusesTable = document.getElementById('campusesTable');
    const campusesTableBody = document.getElementById('campusesTableBody');
    const emptyStateMessage = document.getElementById('emptyStateMessage');
    const totalCampuses = document.getElementById('totalCampuses');
    const totalHostels = document.getElementById('totalHostels');
    const activeCampuses = document.getElementById('activeCampuses');

    // Modal elements
    const createCampusModal = document.getElementById('createCampusModal');
    const updateCampusModal = document.getElementById('updateCampusModal');
    const deleteCampusModal = document.getElementById('deleteCampusModal');

    // Form elements
    const createCampusForm = document.getElementById('createCampusForm');
    const updateCampusForm = document.getElementById('updateCampusForm');
    const deleteCampusForm = document.getElementById('deleteCampusForm');

    // Campus filtering
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
    if (campusesTableBody.children.length > 0) {
        initializeCampusDistributionChart();
    }

    // Table filtering function
    function filterTable() {
        const searchTerm = searchInput.value.toLowerCase();
        const filterValue = filterStatus.value;
        let visibleRows = 0;
        let visibleHostels = 0;
        let visibleActive = 0;

        // Get all rows
        const rows = campusesTableBody.querySelectorAll('tr');

        rows.forEach(row => {
            const name = row.children[1].textContent.toLowerCase();
            const location = row.children[2].textContent.toLowerCase();
            const hostelCount = parseInt(row.children[3].textContent.trim());
            const status = row.getAttribute('data-status');

            // Combine search term and filter conditions
            let showRow = (name.includes(searchTerm) || location.includes(searchTerm));

            if (filterValue === 'active' && status !== 'active') {
                showRow = false;
            } else if (filterValue === 'inactive' && status !== 'inactive') {
                showRow = false;
            }

            // Show/hide the row
            row.style.display = showRow ? '' : 'none';
            if (showRow) {
                visibleRows++;
                visibleHostels += hostelCount;
                if (status === 'active') {
                    visibleActive++;
                }
            }
        });

        // Update stats for filtered view
        totalCampuses.textContent = visibleRows;
        totalHostels.textContent = visibleHostels;
        activeCampuses.textContent = visibleActive;

        // Show empty state if no visible rows
        emptyStateMessage.style.display = visibleRows === 0 ? 'flex' : 'none';
        campusesTable.style.display = visibleRows === 0 ? 'none' : '';
    }

    // Table sorting function
    function sortTable(key) {
        const rows = Array.from(campusesTableBody.querySelectorAll('tr'));

        // Define sort functions for different columns
        const sortFunctions = {
            'index': (a, b) => {
                return parseInt(a.children[0].textContent) - parseInt(b.children[0].textContent);
            },
            'name': (a, b) => {
                return a.children[1].textContent.localeCompare(b.children[1].textContent);
            },
            'hostels': (a, b) => {
                return parseInt(a.children[3].textContent) - parseInt(b.children[3].textContent);
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
        campusesTableBody.innerHTML = '';
        rows.forEach(row => campusesTableBody.appendChild(row));
    }

    // Initialize ApexCharts donut chart with campus data
    function initializeCampusDistributionChart() {
        const chartContainer = document.getElementById('campusDistributionChart');

        // Check if we have actual data
        const rows = campusesTableBody.querySelectorAll('tr');
        if (rows.length === 0) {
            chartContainer.innerHTML = `
                <div class="flex flex-col items-center justify-center h-full text-slate-500">
                    <p class="text-sm">Chart data will appear here once campuses are added.</p>
                </div>
            `;
            return;
        }

        // Count active and inactive campuses
        let activeCampusCount = 0;
        let inactiveCampusCount = 0;

        rows.forEach(row => {
            const status = row.getAttribute('data-status');
            if (status === 'active') {
                activeCampusCount++;
            } else {
                inactiveCampusCount++;
            }
        });

        const options = {
            series: [activeCampusCount, inactiveCampusCount],
            chart: {
                type: 'donut',
                height: '90%'
            },
            labels: ['Active Campuses', 'Inactive Campuses'],
            colors: ['#10B981', '#EF4444'],
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
    window.showCreateCampusModal = function () {
        createCampusForm.reset();
        createCampusModal.classList.remove('hidden');
    };

    window.hideCreateCampusModal = function () {
        createCampusModal.classList.add('hidden');
    };

    window.showUpdateCampusModal = async function (id) {
        try {
            // Show loading state
            document.getElementById('loadingIndicator').style.display = 'flex';

            const response = await fetch(`/Admin/GetCampus/${id}`);
            if (!response.ok) {
                throw new Error('Failed to fetch campus data');
            }

            const campus = await response.json();

            // Populate form fields
            document.getElementById('updateCampusId').value = campus.id;
            document.getElementById('updateCampusName').value = campus.name;
            document.getElementById('updateLocation').value = campus.location || '';
            document.getElementById('updateDescription').value = campus.description || '';
            document.getElementById('updateIsActive').checked = campus.isActive;

            // Show modal
            updateCampusModal.classList.remove('hidden');
        } catch (error) {
            console.error('Error fetching campus details:', error);
            alert('Error loading campus details. Please try again.');
        } finally {
            document.getElementById('loadingIndicator').style.display = 'none';
        }
    };

    window.hideUpdateCampusModal = function () {
        updateCampusModal.classList.add('hidden');
    };

    window.showDeleteCampusModal = function (id, name, hostelCount) {
        document.getElementById('deleteCampusId').value = id;
        document.getElementById('deleteCampusName').textContent = name;
        document.getElementById('deleteCampusHostelCount').textContent = `${hostelCount} Hostels`;

        // Show warning if campus has hostels
        const hasHostelsWarning = document.getElementById('hasHostelsWarning');
        if (parseInt(hostelCount) > 0) {
            hasHostelsWarning.classList.remove('hidden');
        } else {
            hasHostelsWarning.classList.add('hidden');
        }

        deleteCampusModal.classList.remove('hidden');
    };

    window.hideDeleteCampusModal = function () {
        deleteCampusModal.classList.add('hidden');
    };

    // Handle Escape key to close modals
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') {
            hideCreateCampusModal();
            hideUpdateCampusModal();
            hideDeleteCampusModal();
        }
    });
    console.log('Campuses');
});