// coordinator_dashboard.js
document.addEventListener('DOMContentLoaded', function () {
    // Program Enrollment Trends Chart
    const enrollmentTrendsData = window.coordinatorData.enrollmentTrends;
    const programSelect = document.getElementById('programSelect');
    const selectedProgram = programSelect ? programSelect.value : Object.keys(enrollmentTrendsData)[0];

    // Initialize the enrollment trends chart
    initEnrollmentTrendsChart(selectedProgram);

    // Initialize gender distribution chart
    initGenderDistributionChart();

    // Add event listener to program select dropdown
    if (programSelect) {
        programSelect.addEventListener('change', function () {
            initEnrollmentTrendsChart(this.value);
        });
    }
});

// Initialize enrollment trends chart
function initEnrollmentTrendsChart(programName) {
    const trendsData = window.coordinatorData.enrollmentTrends[programName];

    if (!trendsData) return;

    const options = {
        chart: {
            type: 'line',
            height: 300,
            fontFamily: 'Inter, sans-serif',
            toolbar: {
                show: false
            },
            animations: {
                enabled: true
            }
        },
        series: [{
            name: 'Enrollments',
            data: trendsData.enrollments
        }],
        xaxis: {
            categories: trendsData.years,
            labels: {
                style: {
                    colors: '#64748b',
                    fontSize: '12px',
                    fontWeight: 500
                }
            }
        },
        yaxis: {
            labels: {
                style: {
                    colors: '#64748b',
                    fontSize: '12px',
                    fontWeight: 500
                }
            }
        },
        stroke: {
            curve: 'smooth',
            width: 3
        },
        colors: ['#4f46e5'], // Indigo color
        grid: {
            borderColor: '#e2e8f0',
            strokeDashArray: 4,
            xaxis: {
                lines: {
                    show: true
                }
            },
            yaxis: {
                lines: {
                    show: true
                }
            },
            padding: {
                top: 0,
                right: 0,
                bottom: 0,
                left: 0
            }
        },
        markers: {
            size: 5,
            colors: ['#4f46e5'],
            strokeColors: '#ffffff',
            strokeWidth: 2,
            hover: {
                size: 7
            }
        },
        tooltip: {
            theme: 'light',
            marker: {
                show: true
            },
            x: {
                show: true
            }
        }
    };

    // Clear existing chart if any
    const chartElement = document.getElementById('enrollmentTrendsChart');
    if (chartElement) {
        chartElement.innerHTML = '';
        const chart = new ApexCharts(chartElement, options);
        chart.render();
    }
}

// Initialize gender distribution chart
function initGenderDistributionChart() {
    const genderData = window.coordinatorData.genderDistribution;

    if (!genderData) return;

    const options = {
        chart: {
            type: 'donut',
            height: 250,
            fontFamily: 'Inter, sans-serif',
            toolbar: {
                show: false
            },
            animations: {
                enabled: true
            }
        },
        series: genderData.data,
        labels: genderData.labels,
        colors: ['#4f46e5', '#ec4899'], // Indigo for male, Pink for female
        legend: {
            position: 'bottom',
            horizontalAlign: 'center',
            fontWeight: 500,
            fontSize: '12px',
            labels: {
                colors: '#64748b'
            },
            markers: {
                width: 10,
                height: 10,
                offsetX: -2
            }
        },
        stroke: {
            width: 0
        },
        tooltip: {
            theme: 'light',
            y: {
                formatter: function (value) {
                    return value + '%';
                }
            }
        },
        plotOptions: {
            pie: {
                donut: {
                    size: '65%',
                    labels: {
                        show: true,
                        name: {
                            show: true,
                            fontSize: '14px',
                            fontWeight: 600,
                            color: '#1e293b'
                        },
                        value: {
                            show: true,
                            fontSize: '16px',
                            fontWeight: 700,
                            color: '#1e293b',
                            formatter: function (val) {
                                return val + '%';
                            }
                        },
                        total: {
                            show: false,
                            label: 'Total',
                            fontSize: '14px',
                            fontWeight: 600,
                            color: '#1e293b',
                            formatter: function (w) {
                                return w.globals.seriesTotals.reduce((a, b) => a + b, 0) + '%';
                            }
                        }
                    }
                }
            }
        }
    };

    // Render chart
    const chartElement = document.getElementById('genderDistributionChart');
    if (chartElement) {
        const chart = new ApexCharts(chartElement, options);
        chart.render();
    }
}