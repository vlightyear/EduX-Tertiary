document.addEventListener("DOMContentLoaded", function () {
    // Get data from ViewBag
    var faculties = JSON.parse(document.getElementById('faculties').textContent);
    var enrolledStudents = JSON.parse(document.getElementById('enrolledStudents').textContent);
    var applicationsReceived = JSON.parse(document.getElementById('applicationsReceived').textContent);
    var admittedStudents = JSON.parse(document.getElementById('admittedStudents').textContent);
    var months = JSON.parse(document.getElementById('months').textContent);
    var highGrades = JSON.parse(document.getElementById('highGrades').textContent);
    var lowGrades = JSON.parse(document.getElementById('lowGrades').textContent);
    var donutChartData = JSON.parse(document.getElementById('donutChartData').textContent);

    // Bar Chart Configuration
    var options = {
        series: [
            {
                name: 'Students Enrolled',
                data: enrolledStudents
            },
            {
                name: 'Applications Received',
                data: applicationsReceived
            },
            {
                name: 'Admitted Students',
                data: admittedStudents
            }
        ],
        chart: {
            type: 'bar',
            height: '100%',
            fontFamily: 'Inter, sans-serif',
            toolbar: {
                show: false
            }
        },
        plotOptions: {
            bar: {
                horizontal: false,
                columnWidth: '55%',
                borderRadius: 6,
                borderRadiusApplication: 'end'
            },
        },
        dataLabels: {
            enabled: false
        },
        stroke: {
            show: true,
            width: 2,
            colors: ['transparent']
        },
        xaxis: {
            categories: faculties,
            labels: {
                style: {
                    colors: '#475569'
                }
            }
        },
        yaxis: {
            title: {
                text: 'Number of Students',
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
        colors: ['#0EA5E9', '#6366F1', '#10B981'],
        fill: {
            opacity: 1
        },
        tooltip: {
            y: {
                formatter: function (val) {
                    return val + " students"
                }
            }
        },
        responsive: [{
            breakpoint: 640,
            options: {
                plotOptions: {
                    bar: {
                        columnWidth: '80%'
                    }
                },
                legend: {
                    position: 'bottom'
                }
            }
        }]
    };

    // Line Chart Configuration
    var options1 = {
        series: [
            {
                name: "High Grades",
                data: highGrades
            },
            {
                name: "Low Grades",
                data: lowGrades
            }
        ],
        chart: {
            height: '100%',
            type: 'line',
            fontFamily: 'Inter, sans-serif',
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
            }
        },
        colors: ['#0EA5E9', '#6366F1'],
        dataLabels: {
            enabled: false,
        },
        stroke: {
            curve: 'smooth',
            width: 3
        },
        title: {
            text: 'Grade Performance Trends',
            align: 'left',
            style: {
                fontSize: '16px',
                color: '#475569'
            }
        },
        grid: {
            borderColor: '#e2e8f0',
            strokeDashArray: 4,
            xaxis: {
                lines: {
                    show: true
                }
            }
        },
        markers: {
            size: 6,
            strokeWidth: 0
        },
        xaxis: {
            categories: months,
            labels: {
                style: {
                    colors: '#475569'
                }
            }
        },
        yaxis: {
            title: {
                text: 'Grades (%)',
                style: {
                    color: '#475569'
                }
            },
            min: 50,
            max: 100,
            labels: {
                style: {
                    colors: '#475569'
                }
            }
        },
        legend: {
            position: 'top',
            horizontalAlign: 'right',
            floating: true,
            offsetY: -25,
            offsetX: -5
        },
        responsive: [{
            breakpoint: 640,
            options: {
                legend: {
                    position: 'bottom',
                    offsetY: 0,
                    offsetX: 0
                }
            }
        }]
    };

    // Donut Chart Configuration
    var options2 = {
        series: donutChartData,
        chart: {
            type: 'donut',
            height: '100%',
            fontFamily: 'Inter, sans-serif'
        },
        labels: ['Total Students', 'New Admissions', 'Graduated Students', 'Active Students'],
        colors: ['#0EA5E9', '#10B981', '#6366F1', '#F59E0B'],
        plotOptions: {
            pie: {
                donut: {
                    size: '70%'
                }
            }
        },
        legend: {
            position: 'bottom',
            formatter: function (val, opts) {
                return val + " - " + opts.w.globals.series[opts.seriesIndex]
            }
        },
        responsive: [{
            breakpoint: 480,
            options: {
                legend: {
                    position: 'bottom'
                },
                plotOptions: {
                    pie: {
                        donut: {
                            size: '75%'
                        }
                    }
                }
            }
        }],
        stroke: {
            show: false
        }
    };

    // Initialize Charts
    var chart = new ApexCharts(document.querySelector("#chart"), options);
    var chart1 = new ApexCharts(document.querySelector("#chart2"), options1);
    var chart2 = new ApexCharts(document.querySelector("#chart3"), options2);

    // Render Charts
    chart.render();
    chart1.render();
    chart2.render();

});