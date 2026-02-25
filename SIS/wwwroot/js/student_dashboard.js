const isRegistered = window.studentData?.isRegistered || false;
const performanceData = window.studentData?.performanceData || null;
const canViewCompleteResults = window.studentData?.canViewCompleteResults || false;

if (!isRegistered) {
    document.querySelector("#coursePerformanceChart").innerHTML = `
        <div class="flex items-center justify-center h-64">
            <div class="text-center">
                <i class="material-icons text-5xl text-slate-400">folder_open</i>
                <p class="mt-2 text-slate-500">Not registered for any courses</p>
            </div>
        </div>`;
} else if (!performanceData || !performanceData.courses || performanceData.courses.length === 0) {
    document.querySelector("#coursePerformanceChart").innerHTML = `
        <div class="flex items-center justify-center h-64">
            <div class="text-center">
                <i class="material-icons text-5xl text-slate-400">assessment</i>
                <p class="mt-2 text-slate-500">No course performance data available</p>
            </div>
        </div>`;
} else {
    // Transform the real performance data for the chart
    function transformDataForChart(data) {
        if (!data?.courses?.length) return { series: [], categories: [] };

        const series = [{
            name: 'Course Performance',
            data: data.courses.map(course => {
                // Use the calculated total score from the controller
                return Math.round(course.totalScore || 0);
            })
        }];

        const categories = data.courses.map(course => course.courseCode);
        return { series, categories };
    }

    const { series, categories } = transformDataForChart(performanceData);

    // Create chart options
    var options = {
        series: series,
        chart: {
            type: 'bar',
            height: 350,
            toolbar: {
                show: false
            },
            fontFamily: 'inherit'
        },
        plotOptions: {
            bar: {
                horizontal: false,
                borderRadius: 6,
                columnWidth: '55%',
                distributed: true
            },
        },
        dataLabels: {
            enabled: true,
            formatter: function (val) {
                return val + "%";
            },
            style: {
                fontSize: '12px',
                fontFamily: 'inherit',
                colors: ["#475569"]
            },
            offsetY: -20
        },
        xaxis: {
            categories: categories,
            labels: {
                style: {
                    fontSize: '12px',
                    fontFamily: 'inherit',
                    colors: Array(categories.length).fill("#475569")
                }
            },
            axisBorder: {
                show: false
            },
            axisTicks: {
                show: false
            }
        },
        yaxis: {
            min: 0,
            max: 100,
            labels: {
                style: {
                    fontSize: '12px',
                    fontFamily: 'inherit',
                    colors: ["#475569"]
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
        // Dynamic colors based on performance
        colors: series[0].data.map(score => {
            if (score >= 70) return '#10b981'; // Green for good performance
            if (score >= 50) return '#f59e0b'; // Amber for average performance
            return '#ef4444'; // Red for poor performance
        }),
        tooltip: {
            y: {
                formatter: function (val) {
                    return val + "%";
                }
            },
            theme: 'light',
            custom: function ({ series, seriesIndex, dataPointIndex, w }) {
                const course = performanceData.courses[dataPointIndex];
                const score = series[seriesIndex][dataPointIndex];

                let tooltipContent = `
                    <div class="p-3">
                        <div class="font-semibold">${course.courseCode}</div>
                        <div class="text-sm text-gray-600 mb-2">${course.courseName}</div>
                        <div class="text-lg font-bold">${score}%</div>
                `;

                // Show assessment breakdown if available
                if (course.scores && Object.keys(course.scores).length > 0) {
                    tooltipContent += `<div class="mt-2 text-xs">`;
                    for (const [assessmentName, scoreData] of Object.entries(course.scores)) {
                        tooltipContent += `<div>${assessmentName}: ${scoreData.score}%</div>`;
                    }
                    tooltipContent += `</div>`;
                }

                // Add restriction notice if applicable
                if (!canViewCompleteResults) {
                    tooltipContent += `
                        <div class="mt-2 text-xs text-yellow-600 border-t pt-2">
                            <i class="material-icons text-xs">info</i>
                            Exam scores excluded (fees pending)
                        </div>`;
                }

                tooltipContent += `</div>`;
                return tooltipContent;
            }
        },
        legend: {
            show: false
        },
        // Add subtitle to show restriction status
        subtitle: {
            text: !canViewCompleteResults ? 'Continuous assessments only (clear fees to view exam scores)' : 'Complete performance including all assessments',
            align: 'center',
            style: {
                fontSize: '11px',
                color: !canViewCompleteResults ? '#f59e0b' : '#6b7280'
            }
        }
    };

    var chart = new ApexCharts(document.querySelector("#coursePerformanceChart"), options);
    chart.render();
}

// Timetable data - keep existing implementation
const timetableData = {
    //Monday: [
    //    { code: 'CS101', name: 'Introduction to Computing', time: '08:00 - 10:00', venue: 'Lab 1' },
    //    { code: 'MATH201', name: 'Calculus I', time: '14:00 - 16:00', venue: 'LT2' }
    //],
    //Tuesday: [
    //    { code: 'PHY301', name: 'Physics', time: '10:00 - 12:00', venue: 'LT1' },
    //    { code: 'ENG102', name: 'Communication Skills', time: '13:00 - 15:00', venue: 'Room 201' }
    //],
    //Wednesday: [
    //    { code: 'CS101', name: 'Introduction to Computing', time: '09:00 - 11:00', venue: 'Lab 2' }
    //],
    //Thursday: [
    //    { code: 'MATH201', name: 'Calculus I', time: '11:00 - 13:00', venue: 'LT3' },
    //    { code: 'PHY301', name: 'Physics', time: '15:00 - 17:00', venue: 'Lab 3' }
    //],
    //Friday: [
    //    { code: 'ENG102', name: 'Communication Skills', time: '08:00 - 10:00', venue: 'Room 202' }
    //]
};

// Get today's day name
const days = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
const today = days[new Date().getDay()];

// Get the timetable content element
const timetableContent = document.querySelector('#timetableContent');

// Check registration status and render appropriate content
if (!isRegistered) {
    timetableContent.innerHTML = `
        <div class="flex items-center justify-center p-6 text-slate-500">
            <div class="text-center">
                <i class="material-icons text-5xl text-slate-400">folder_open</i>
                <p class="mt-2">Please complete your registration to view your timetable</p>
            </div>
        </div>
    `;
} else {
    const todayClasses = timetableData[today] || [];

    if (today === 'Sunday' || today === 'Saturday' || todayClasses.length === 0) {
        timetableContent.innerHTML = `
            <div class="flex items-center justify-center p-6 text-slate-500">
                <div class="text-center">
                    <i class="material-icons text-5xl text-slate-400">event_busy</i>
                    <p class="mt-2">No classes scheduled for today</p>
                </div>
            </div>
        `;
    } else {
        let timetableHTML = `
            <div class="flex items-center justify-between mb-4">
                <p class="font-medium text-slate-600">Today's Classes</p>
                <span class="text-sm font-medium text-blue-600 bg-blue-50 px-2 py-1 rounded">${today}</span>
            </div>
            <div class="space-y-3">
        `;

        // Sort classes by time
        todayClasses.sort((a, b) => {
            const timeA = a.time.split(' - ')[0];
            const timeB = b.time.split(' - ')[0];
            return timeA.localeCompare(timeB);
        });

        todayClasses.forEach(course => {
            timetableHTML += `
                <div class="bg-slate-50 p-4 rounded-lg">
                    <div class="flex justify-between items-start">
                        <div>
                            <div class="flex items-center gap-2">
                                <p class="font-medium text-slate-800">${course.code}</p>
                                <span class="text-sm text-slate-600">•</span>
                                <p class="text-sm text-slate-600">${course.time}</p>
                            </div>
                            <p class="text-sm text-slate-700 mt-1">${course.name}</p>
                        </div>
                        <span class="text-sm font-medium text-blue-600 bg-blue-50 px-2 py-1 rounded">
                            ${course.venue}
                        </span>
                    </div>
                </div>
            `;
        });

        timetableHTML += '</div>';
        timetableContent.innerHTML = timetableHTML;
    }
}