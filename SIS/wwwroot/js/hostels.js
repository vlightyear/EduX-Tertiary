document.addEventListener('DOMContentLoaded', function () {
    // Initialize variables and elements
    const searchInput = document.getElementById('searchInput');
    const filterCampus = document.getElementById('filterCampus');
    const filterGender = document.getElementById('filterGender');
    const filterStatus = document.getElementById('filterStatus');
    const hostelsTable = document.getElementById('hostelsTable');
    const hostelsTableBody = document.getElementById('hostelsTableBody');
    const emptyStateMessage = document.getElementById('emptyStateMessage');
    const totalHostels = document.getElementById('totalHostels');
    const loadingIndicator = document.getElementById('loadingIndicator');

    // Modal elements
    const createHostelModal = document.getElementById('createHostelModal');
    const updateHostelModal = document.getElementById('updateHostelModal');
    const deleteHostelModal = document.getElementById('deleteHostelModal');

    // Form elements
    const createHostelForm = document.getElementById('createHostelForm');
    const updateHostelForm = document.getElementById('updateHostelForm');
    const deleteHostelForm = document.getElementById('deleteHostelForm');
    const confirmDeleteBtn = document.getElementById('confirmDeleteBtn');

    // Add event listeners for filtering
    searchInput.addEventListener('input', filterTable);
    filterCampus.addEventListener('change', filterTable);
    filterGender.addEventListener('change', filterTable);
    filterStatus.addEventListener('change', filterTable);

    // Setup sorting buttons
    document.querySelectorAll('.sort-btn').forEach(button => {
        button.addEventListener('click', function () {
            const sortKey = this.getAttribute('data-sort');
            sortTable(sortKey);
        });
    });

    // Initialize charts if we have data
    if (hostelsTableBody.children.length > 0) {
        initializeGenderChart();
        initializeCampusChart();
    }

    // Table filtering function
    function filterTable() {
        const searchTerm = searchInput.value.toLowerCase();
        const campusFilter = filterCampus.value;
        const genderFilter = filterGender.value;
        const statusFilter = filterStatus.value;

        let visibleRows = 0;

        // Get all rows
        const rows = hostelsTableBody.querySelectorAll('tr');

        rows.forEach(row => {
            const name = row.children[1].textContent.toLowerCase();
            const campus = row.getAttribute('data-campus');
            const gender = row.getAttribute('data-gender');
            const status = row.getAttribute('data-status');

            // Apply all filters
            let showRow = name.includes(searchTerm);

            if (campusFilter !== 'all' && campus !== campusFilter) {
                showRow = false;
            }

            if (genderFilter !== 'all' && gender !== genderFilter) {
                showRow = false;
            }

            if (statusFilter !== 'all' && status !== statusFilter) {
                showRow = false;
            }

            // Show/hide the row
            row.style.display = showRow ? '' : 'none';
            if (showRow) {
                visibleRows++;
            }
        });

        // Update stats for filtered view
        totalHostels.textContent = visibleRows;

        // Show empty state if no visible rows
        emptyStateMessage.style.display = visibleRows === 0 ? 'flex' : 'none';
        hostelsTable.style.display = visibleRows === 0 ? 'none' : '';
    }

    // Table sorting function
    function sortTable(key) {
        const rows = Array.from(hostelsTableBody.querySelectorAll('tr'));

        // Define sort functions for different columns
        const sortFunctions = {
            'index': (a, b) => {
                return parseInt(a.children[0].textContent) - parseInt(b.children[0].textContent);
            },
            'name': (a, b) => {
                return a.children[1].textContent.localeCompare(b.children[1].textContent);
            },
            'capacity': (a, b) => {
                return parseInt(a.children[4].textContent.trim()) - parseInt(b.children[4].textContent.trim());
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
        hostelsTableBody.innerHTML = '';
        rows.forEach(row => hostelsTableBody.appendChild(row));
    }

    // Initialize ApexCharts donut chart for gender distribution
    function initializeGenderChart() {
        const chartContainer = document.getElementById('hostelGenderChart');

        // Check if we have actual data
        const rows = hostelsTableBody.querySelectorAll('tr');
        if (rows.length === 0) {
            chartContainer.innerHTML = `
                <div class="flex flex-col items-center justify-center h-full text-slate-500">
                    <p class="text-sm">Chart data will appear here once hostels are added.</p>
                </div>
            `;
            return;
        }

        // Count hostels by gender
        let maleCount = 0;
        let femaleCount = 0;
        let mixedCount = 0;

        rows.forEach(row => {
            const gender = row.getAttribute('data-gender');
            if (gender === 'male') {
                maleCount++;
            } else if (gender === 'female') {
                femaleCount++;
            } else {
                mixedCount++;
            }
        });

        const options = {
            series: [maleCount, femaleCount, mixedCount],
            chart: {
                type: 'donut',
                height: '90%'
            },
            labels: ['Male Hostels', 'Female Hostels', 'Mixed Hostels'],
            colors: ['#3B82F6', '#EC4899', '#8B5CF6'],
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

    // Initialize ApexCharts bar chart for campus distribution
    function initializeCampusChart() {
        const chartContainer = document.getElementById('hostelCampusChart');

        // Check if we have actual data
        const rows = hostelsTableBody.querySelectorAll('tr');
        if (rows.length === 0) {
            chartContainer.innerHTML = `
                <div class="flex flex-col items-center justify-center h-full text-slate-500">
                    <p class="text-sm">Chart data will appear here once hostels are added.</p>
                </div>
            `;
            return;
        }

        // Count hostels by campus
        const campusCounts = {};
        const campusNames = {};

        // Get campus options from filter dropdown
        Array.from(filterCampus.options).forEach(option => {
            if (option.value !== 'all') {
                campusCounts[option.value] = 0;
                campusNames[option.value] = option.text;
            }
        });

        // Count hostels per campus
        rows.forEach(row => {
            const campusId = row.getAttribute('data-campus');
            if (campusId in campusCounts) {
                campusCounts[campusId]++;
            }
        });

        // Prepare data for chart
        const campusData = [];
        for (const campusId in campusCounts) {
            campusData.push({
                x: campusNames[campusId] || `Campus ${campusId}`,
                y: campusCounts[campusId]
            });
        }

        // Sort data by count descending
        campusData.sort((a, b) => b.y - a.y);

        const options = {
            series: [{
                name: 'Hostels',
                data: campusData
            }],
            chart: {
                type: 'bar',
                height: '90%',
                toolbar: {
                    show: false
                }
            },
            plotOptions: {
                bar: {
                    borderRadius: 4,
                    horizontal: true,
                    barHeight: '70%',
                    distributed: true
                }
            },
            colors: ['#06b6d4', '#0891b2', '#0e7490', '#155e75', '#164e63'],
            dataLabels: {
                enabled: true,
                formatter: function (val) {
                    return val;
                },
                style: {
                    fontSize: '12px',
                    colors: ['#fff']
                }
            },
            xaxis: {
                categories: campusData.map(item => item.x),
                labels: {
                    style: {
                        fontSize: '12px'
                    }
                }
            },
            yaxis: {
                labels: {
                    show: true
                }
            },
            grid: {
                show: false
            },
            legend: {
                show: false
            }
        };

        // Create the chart
        const chart = new ApexCharts(chartContainer, options);
        chart.render();
    }

    // Modal functions
    window.showCreateHostelModal = function () {
        createHostelForm.reset();
        createHostelModal.classList.remove('hidden');
    };

    window.hideCreateHostelModal = function () {
        createHostelModal.classList.add('hidden');
    };

    window.showUpdateHostelModal = async function (id) {
        // Show loading indicator
        document.getElementById('loadingIndicator').style.display = 'flex';

        // Fetch hostel data
        fetch(`/Hostel/GetHostel/${id}`)
            .then(response => {
                if (!response.ok) {
                    throw new Error('Failed to fetch hostel data');
                }
                return response.json();
            })
            .then(hostel => {
                // Set existing form values
                document.getElementById('updateHostelID').value = hostel.id;
                document.getElementById('updateHostelName').value = hostel.name;
                document.getElementById('updateCampusID').value = hostel.campusId;
                document.getElementById('updateGender').value = hostel.gender;
                document.getElementById('updateWardenID').value = hostel.wardenId || '';
                document.getElementById('updateTotalRooms').value = hostel.totalRooms;
                document.getElementById('updateTotalCapacity').value = hostel.totalCapacity;
                document.getElementById('updateStatus').value = hostel.status;
                document.getElementById('updateDescription').value = hostel.description || '';

                // Set new room generation fields
                document.getElementById('updateDefaultRoomType').value = hostel.defaultRoomType;
                document.getElementById('updateDefaultCapacity').value = hostel.defaultCapacity;
                document.getElementById('updateRoomsPerFloor').value = hostel.roomsPerFloor;
                document.getElementById('updateRoomNumberingPattern').value = hostel.roomNumberingPattern;
                document.getElementById('updateAutoGenerateBeds').checked = hostel.autoGenerateBeds;

                // Show the modal
                document.getElementById('updateHostelModal').classList.remove('hidden');
            })
            .catch(error => {
                console.error('Error fetching hostel data:', error);
                window.showAppToast('Error loading hostel data. Please try again.');
            })
            .finally(() => {
                document.getElementById('loadingIndicator').style.display = 'none';
            });
    };

    window.hideUpdateHostelModal = function () {
        updateHostelModal.classList.add('hidden');
    };

    window.showDeleteHostelModal = async function (id, name) {
        try {
            document.getElementById('deleteHostelID').value = id;
            document.getElementById('deleteHostelName').textContent = name;

            // Fetch hostel details to check if it has rooms
            loadingIndicator.style.display = 'flex';

            const response = await fetch(`/Hostel/GetHostel/${id}`);
            if (!response.ok) {
                throw new Error('Failed to fetch hostel data');
            }

            const hostel = await response.json();

            // Show warning if hostel has rooms
            const hasRoomsWarning = document.getElementById('hasRoomsWarning');
            const hasRooms = hostel.statistics && hostel.statistics.totalRooms > 0;

            hasRoomsWarning.classList.toggle('hidden', !hasRooms);
            confirmDeleteBtn.disabled = hasRooms;
            confirmDeleteBtn.classList.toggle('opacity-50', hasRooms);
            confirmDeleteBtn.classList.toggle('cursor-not-allowed', hasRooms);

            // Show the modal
            deleteHostelModal.classList.remove('hidden');
        } catch (error) {
            console.error('Error checking hostel details:', error);
            // Still show modal but without room checks
            deleteHostelModal.classList.remove('hidden');
        } finally {
            loadingIndicator.style.display = 'none';
        }
    };

    window.hideDeleteHostelModal = function () {
        deleteHostelModal.classList.add('hidden');
    };

    // Handle Escape key to close modals
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') {
            hideCreateHostelModal();
            hideUpdateHostelModal();
            hideDeleteHostelModal();
        }
    });
});