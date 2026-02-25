document.addEventListener('DOMContentLoaded', function () {
    // Initialize all dashboard components
    initRevenueChart();
    initSchoolPerformanceChart();
    initPaymentMethodsChart();
    populateTransactionsTables();

    // Auto-refresh every 10 minutes
    setInterval(refreshDashboard, 600000);
});

function initRevenueChart() {
    const monthLabels = JSON.parse(document.getElementById('monthLabels').textContent);
    const revenueAmounts = JSON.parse(document.getElementById('revenueAmounts').textContent);

    const options = {
        series: [{
            name: 'Revenue',
            data: revenueAmounts
        }],
        chart: {
            type: 'area',
            height: 350,
            background: 'transparent',
            toolbar: {
                show: false
            },
            fontFamily: 'Inter, sans-serif'
        },
        colors: ['#3B82F6'],
        dataLabels: {
            enabled: false,
        },
        stroke: {
            curve: 'smooth',
            width: 3
        },
        fill: {
            type: 'gradient',
            gradient: {
                shadeIntensity: 1,
                opacityFrom: 0.7,
                opacityTo: 0.1,
                stops: [0, 100]
            }
        },
        xaxis: {
            categories: monthLabels,
            labels: {
                style: {
                    colors: '#64748B',
                    fontSize: '12px'
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
            labels: {
                style: {
                    colors: '#64748B',
                    fontSize: '12px'
                },
                formatter: function (val) {
                    return 'K' + val.toLocaleString();
                }
            }
        },
        grid: {
            borderColor: '#E2E8F0',
            strokeDashArray: 5
        },
        tooltip: {
            y: {
                formatter: function (val) {
                    return "K" + val.toLocaleString();
                }
            },
            theme: 'light'
        },
        markers: {
            size: 6,
            strokeColors: '#fff',
            strokeWidth: 2,
            hover: {
                size: 8
            }
        }
    };

    const chart = new ApexCharts(document.getElementById('revenueChart'), options);
    chart.render();
}

function initSchoolPerformanceChart() {
    const schoolNames = JSON.parse(document.getElementById('schoolNames').textContent);
    const schoolRevenues = JSON.parse(document.getElementById('schoolRevenues').textContent);

    const options = {
        series: [{
            name: 'Revenue',
            data: schoolRevenues
        }],
        chart: {
            type: 'bar',
            height: 400,
            background: 'transparent',
            toolbar: {
                show: false
            },
            fontFamily: 'Inter, sans-serif'
        },
        colors: ['#10B981'],
        plotOptions: {
            bar: {
                borderRadius: 8,
                horizontal: false,
                columnWidth: '60%',
                dataLabels: {
                    position: 'top'
                }
            }
        },
        dataLabels: {
            enabled: true,
            formatter: function (val) {
                return 'K' + val.toLocaleString();
            },
            offsetY: -20,
            style: {
                fontSize: '12px',
                colors: ['#374151']
            }
        },
        xaxis: {
            categories: schoolNames,
            labels: {
                style: {
                    colors: '#64748B',
                    fontSize: '12px'
                },
                rotate: -45
            },
            axisBorder: {
                show: false
            },
            axisTicks: {
                show: false
            }
        },
        yaxis: {
            labels: {
                style: {
                    colors: '#64748B',
                    fontSize: '12px'
                },
                formatter: function (val) {
                    return 'K' + val.toLocaleString();
                }
            }
        },
        grid: {
            borderColor: '#E2E8F0',
            strokeDashArray: 5
        },
        tooltip: {
            y: {
                formatter: function (val) {
                    return "K" + val.toLocaleString();
                }
            },
            theme: 'light'
        }
    };

    const chart = new ApexCharts(document.getElementById('schoolPerformanceChart'), options);
    chart.render();
}

function initPaymentMethodsChart() {
    const paymentMethods = JSON.parse(document.getElementById('paymentMethods').textContent);
    const paymentAmounts = JSON.parse(document.getElementById('paymentAmounts').textContent);

    const options = {
        series: paymentAmounts,
        chart: {
            type: 'donut',
            height: 300,
            background: 'transparent',
            fontFamily: 'Inter, sans-serif'
        },
        colors: ['#8B5CF6', '#06B6D4', '#F59E0B', '#EF4444', '#10B981'],
        labels: paymentMethods,
        plotOptions: {
            pie: {
                donut: {
                    size: '70%',
                    labels: {
                        show: true,
                        total: {
                            show: true,
                            label: 'Total',
                            fontSize: '16px',
                            fontWeight: 600,
                            color: '#374151',
                            formatter: function (w) {
                                const total = w.globals.seriesTotals.reduce((a, b) => a + b, 0);
                                return 'K' + total.toLocaleString();
                            }
                        },
                        value: {
                            show: true,
                            fontSize: '20px',
                            fontWeight: 600,
                            color: '#1F2937',
                            formatter: function (val) {
                                return 'K' + parseFloat(val).toLocaleString();
                            }
                        }
                    }
                }
            }
        },
        dataLabels: {
            enabled: true,
            formatter: function (val, opts) {
                return opts.w.config.series[opts.seriesIndex].toLocaleString();
            },
            style: {
                fontSize: '12px',
                fontWeight: 600,
                colors: ['#fff']
            }
        },
        legend: {
            position: 'bottom',
            fontSize: '12px',
            fontWeight: 500,
            labels: {
                colors: '#64748B'
            }
        },
        tooltip: {
            y: {
                formatter: function (val) {
                    return "K" + val.toLocaleString();
                }
            },
            theme: 'light'
        }
    };

    const chart = new ApexCharts(document.getElementById('paymentMethodsChart'), options);
    chart.render();
}

function populateTransactionsTables() {
    // Recent Transactions
    const recentTransactions = JSON.parse(document.getElementById('recentTransactions').textContent);
    const transactionsTableBody = document.getElementById('recentTransactionsTable');

    if (recentTransactions && recentTransactions.length > 0) {
        const transactionsHTML = recentTransactions.map(transaction => {
            const paymentDate = new Date(transaction.PaymentDate);
            const formattedDate = paymentDate.toLocaleDateString('en-US', {
                month: 'short',
                day: 'numeric',
                year: 'numeric'
            });

            return `
                <tr class="hover:bg-slate-50 transition-colors">
                    <td class="py-3 px-4">
                        <div>
                            <p class="font-medium text-slate-800">${transaction.StudentName}</p>
                            <p class="text-sm text-slate-500">${transaction.StudentId}</p>
                        </div>
                    </td>
                    <td class="py-3 px-4">
                        <span class="font-semibold text-green-600">K${transaction.Amount.toLocaleString()}</span>
                    </td>
                    <td class="py-3 px-4">
                        <span class="px-2 py-1 bg-blue-100 text-blue-800 text-xs font-medium rounded-full">
                            ${transaction.PaymentMethod}
                        </span>
                    </td>
                    <td class="py-3 px-4 text-sm text-slate-600">${formattedDate}</td>
                </tr>
            `;
        }).join('');

        transactionsTableBody.innerHTML = transactionsHTML;
    } else {
        transactionsTableBody.innerHTML = `
            <tr>
                <td colspan="4" class="py-8 text-center text-slate-500">
                    <i class="material-icons text-4xl mb-2 opacity-50">receipt_long</i>
                    <p>No high-value transactions found</p>
                </td>
            </tr>
        `;
    }

    // Overdue Students
    const overdueStudents = JSON.parse(document.getElementById('overdueStudents').textContent);
    const overdueTableBody = document.getElementById('overdueStudentsTable');

    if (overdueStudents && overdueStudents.length > 0) {
        const overdueHTML = overdueStudents.map(student => {
            const urgencyClass = student.OutstandingAmount > 5000 ? 'text-red-600 font-bold' :
                student.OutstandingAmount > 2000 ? 'text-orange-600 font-semibold' :
                    'text-amber-600';

            return `
                <tr class="hover:bg-slate-50 transition-colors">
                    <td class="py-3 px-4">
                        <div>
                            <p class="font-medium text-slate-800">${student.StudentName}</p>
                            <p class="text-sm text-slate-500">${student.StudentId}</p>
                        </div>
                    </td>
                    <td class="py-3 px-4">
                        <span class="text-sm text-slate-600">${student.Programme}</span>
                    </td>
                    <td class="py-3 px-4">
                        <span class="${urgencyClass}">K${student.OutstandingAmount.toLocaleString()}</span>
                    </td>
                </tr>
            `;
        }).join('');

        overdueTableBody.innerHTML = overdueHTML;
    } else {
        overdueTableBody.innerHTML = `
            <tr>
                <td colspan="3" class="py-8 text-center text-slate-500">
                    <i class="material-icons text-4xl mb-2 opacity-50 text-green-400">check_circle</i>
                    <p>No students with outstanding payments</p>
                </td>
            </tr>
        `;
    }
}

function refreshDashboard() {
    // Optional: Add AJAX call to refresh data without page reload
    console.log('Dashboard auto-refresh triggered at:', new Date().toLocaleString());

    // You can implement an AJAX endpoint to get fresh data
    // fetch('/Home/GetDashboardData')
    //     .then(response => response.json())
    //     .then(data => {
    //         // Update charts and tables with new data
    //         updateChartsWithNewData(data);
    //     })
    //     .catch(error => console.error('Dashboard refresh failed:', error));
}

function updateChartsWithNewData(data) {
    // Implementation for updating charts with new data
    // This would be called when refreshDashboard() gets fresh data
}

// Helper function to format currency
function formatCurrency(amount) {
    return 'K' + amount.toLocaleString('en-US', {
        minimumFractionDigits: 0,
        maximumFractionDigits: 0
    });
}

// Helper function to format dates
function formatDate(dateString) {
    const date = new Date(dateString);
    return date.toLocaleDateString('en-US', {
        year: 'numeric',
        month: 'short',
        day: 'numeric'
    });
}

// Export functions for potential external use
window.VCDashboard = {
    refresh: refreshDashboard,
    formatCurrency: formatCurrency,
    formatDate: formatDate
};