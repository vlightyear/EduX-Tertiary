document.addEventListener("DOMContentLoaded", function () {
    // Initialize all dashboard components
    initGradeTrendsChart();
    populateTodaySchedule();
    populateUpcomingDeadlines();
    updateCourseOverview(); // ⭐ UPDATED: Changed from updateAttendanceOverview

    // Set up course select change event
    const courseSelect = document.getElementById('courseSelect');
    if (courseSelect) {
        courseSelect.addEventListener('change', function () {
            updateGradeTrendsChart(this.value);
        });
    }
});

function initGradeTrendsChart() {
    const courseSelect = document.getElementById('courseSelect');
    if (courseSelect && courseSelect.value) {
        updateGradeTrendsChart(courseSelect.value);
    }
}

function updateGradeTrendsChart(courseCode) {
    const gradeTrends = window.lecturerData?.gradeTrends || {};

    // Get the data for the selected course
    const courseData = gradeTrends[courseCode];

    if (!courseData || !courseData.assessments || courseData.assessments.length === 0) {
        document.getElementById('gradeTrendsChart').innerHTML = `
            <div class="flex items-center justify-center h-full text-slate-500">
                <div class="text-center">
                    <i class="material-icons text-4xl mb-2">analytics</i>
                    <p>No assessment data available for this course</p>
                    <p class="text-sm mt-1">Assessments will appear here once published</p>
                </div>
            </div>
        `;
        return;
    }

    // Prepare chart options
    const options = {
        series: [
            {
                name: 'Average Score',
                data: courseData.averageScores || []
            },
            {
                name: 'Highest Score',
                data: courseData.highestScores || []
            },
            {
                name: 'Lowest Score',
                data: courseData.lowestScores || []
            }
        ],
        chart: {
            height: 300,
            type: 'line',
            dropShadow: {
                enabled: true,
                color: '#000',
                top: 18,
                left: 7,
                blur: 10,
                opacity: 0.2
            },
            toolbar: {
                show: false
            },
            fontFamily: 'Inter, sans-serif'
        },
        colors: ['#3B82F6', '#10B981', '#F59E0B'],
        dataLabels: {
            enabled: false,
        },
        stroke: {
            curve: 'smooth',
            width: 3
        },
        markers: {
            size: 5
        },
        title: {
            text: `${courseData.courseName} - ${courseData.academicYear} (${courseData.semester})`,
            align: 'left',
            style: {
                fontSize: '14px',
                fontWeight: 'normal',
                color: '#64748b'
            }
        },
        xaxis: {
            categories: courseData.assessments,
            title: {
                text: 'Assessments',
                style: {
                    color: '#475569'
                }
            },
            labels: {
                style: {
                    colors: '#475569'
                }
            }
        },
        yaxis: {
            title: {
                text: 'Score (%)',
                style: {
                    color: '#475569'
                }
            },
            min: 0,
            max: 100,
            labels: {
                style: {
                    colors: '#475569'
                },
                formatter: function (val) {
                    return val + "%";
                }
            }
        },
        grid: {
            borderColor: '#f1f5f9',
            strokeDashArray: 4
        },
        fill: {
            opacity: 1
        },
        legend: {
            position: 'top',
            horizontalAlign: 'right',
            floating: true,
            offsetY: -25,
            offsetX: -5
        },
        tooltip: {
            y: {
                formatter: function (val) {
                    return val + "%";
                }
            },
            theme: 'light'
        }
    };

    // Clear the previous chart if it exists
    document.getElementById('gradeTrendsChart').innerHTML = '';

    // Create and render the new chart
    const chart = new ApexCharts(document.getElementById('gradeTrendsChart'), options);
    chart.render();
}

// ⭐ UPDATED: Today's schedule shows "feature not ready" message
function populateTodaySchedule() {
    const scheduleContainer = document.getElementById('todaySchedule');
    const scheduleData = window.lecturerData?.todaySchedule || [];

    // Always show "feature not ready" message since schedule feature isn't implemented
    scheduleContainer.innerHTML = `
        <div class="text-center py-8 text-slate-500 bg-slate-50 rounded-lg border-2 border-dashed border-slate-200">
            <i class="material-icons text-5xl mb-3 text-slate-400">event_note</i>
            <p class="font-medium text-slate-700 mb-1">Timetable Feature Coming Soon</p>
            <p class="text-sm">Class scheduling and timetable management will be available in a future update</p>
        </div>
    `;
}

function populateUpcomingDeadlines() {
    const deadlinesContainer = document.getElementById('upcomingDeadlines');
    const deadlinesData = window.lecturerData?.upcomingDeadlines || [];

    if (deadlinesData.length === 0) {
        deadlinesContainer.innerHTML = `
            <div class="text-center py-8 text-slate-500">
                <i class="material-icons text-4xl mb-2 text-green-400">check_circle</i>
                <p class="font-medium text-slate-700">All caught up!</p>
                <p class="text-sm mt-1">No pending grading tasks</p>
            </div>
        `;
        return;
    }

    const deadlinesHTML = deadlinesData.map(deadline => {
        const priorityColors = {
            high: 'bg-red-50 border-red-200',
            medium: 'bg-amber-50 border-amber-200',
            low: 'bg-blue-50 border-blue-200'
        };

        const badgeColors = {
            high: 'bg-red-100 text-red-800',
            medium: 'bg-amber-100 text-amber-800',
            low: 'bg-blue-100 text-blue-800'
        };

        const iconColors = {
            high: 'bg-red-100 text-red-600',
            medium: 'bg-amber-100 text-amber-600',
            low: 'bg-blue-100 text-blue-600'
        };

        return `
            <div class="flex gap-3 p-3 ${priorityColors[deadline.priority]} border rounded-lg">
                <div class="${iconColors[deadline.priority]} p-2 rounded-lg">
                    <i class="material-icons">assignment</i>
                </div>
                <div class="flex-1">
                    <div class="flex justify-between items-start">
                        <div>
                            <p class="font-medium text-slate-800">${deadline.title}</p>
                            <p class="text-xs text-slate-500 mt-1">${deadline.description}</p>
                        </div>
                        <span class="text-xs font-medium ${badgeColors[deadline.priority]} px-2 py-0.5 rounded-full ml-2">
                            ${deadline.priority.toUpperCase()}
                        </span>
                    </div>
                </div>
            </div>
        `;
    }).join('');

    deadlinesContainer.innerHTML = deadlinesHTML;
}

// ⭐ UPDATED: New function to handle course overview data
function updateCourseOverview() {
    const courseOverviewData = window.lecturerData?.courseOverview || [];
    const tbody = document.getElementById('courseOverview');

    if (!tbody) return;

    if (courseOverviewData.length === 0) {
        tbody.innerHTML = `
            <tr>
                <td colspan="5" class="py-6 text-center text-slate-500">No courses assigned</td>
            </tr>
        `;
        return;
    }

    const courseHTML = courseOverviewData.map(course => {
        // Status badge styling
        const statusClass = course.isFullyGraded
            ? 'bg-green-100 text-green-700 border border-green-200'
            : 'bg-amber-100 text-amber-700 border border-amber-200';

        const statusIcon = course.isFullyGraded
            ? '<i class="material-icons text-sm mr-1">check_circle</i>'
            : '<i class="material-icons text-sm mr-1">pending</i>';

        return `
            <tr class="hover:bg-slate-50 transition-colors">
                <td class="py-3 px-4 text-sm">
                    <div class="font-medium text-slate-800">${course.courseName}</div>
                    <div class="text-xs text-slate-500">${course.courseCode}</div>
                </td>
                <td class="py-3 px-4 text-sm text-slate-800">
                    <div class="flex items-center">
                        <i class="material-icons text-sm text-slate-400 mr-1">people</i>
                        ${course.totalStudents}
                    </div>
                </td>
                <td class="py-3 px-4 text-sm">
                    ${course.averagePerformance !== 'N/A'
                ? `<span class="font-medium text-slate-800">${course.averagePerformance}</span>`
                : `<span class="text-slate-400">Not graded</span>`
            }
                </td>
                <td class="py-3 px-4 text-sm">
                    <span class="inline-flex items-center px-2 py-1 rounded-full text-xs font-medium ${statusClass}">
                        ${statusIcon}${course.gradingStatus}
                    </span>
                </td>
                <td class="py-3 px-4 text-sm">
                    <a href="/StudentResults/CourseAssessments/${course.courseId}" 
                       class="text-blue-600 hover:text-blue-800 font-medium">
                        Manage
                    </a>
                </td>
            </tr>
        `;
    }).join('');

    tbody.innerHTML = courseHTML;
}

// Helper function to format dates
function formatDate(dateString) {
    const date = new Date(dateString);
    const now = new Date();
    const diffTime = date - now;
    const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));

    if (diffDays === 0) return 'Today';
    if (diffDays === 1) return 'Tomorrow';
    if (diffDays < 7) return `${diffDays} days`;

    return date.toLocaleDateString();
}

// Function to refresh dashboard data (can be called periodically)
function refreshDashboard() {
    // You can implement AJAX calls here to refresh data without page reload
    console.log('Refreshing dashboard data...');
}

// Auto-refresh every 5 minutes (optional)
// setInterval(refreshDashboard, 5 * 60 * 1000);