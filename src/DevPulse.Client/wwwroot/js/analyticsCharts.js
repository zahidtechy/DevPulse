const charts = new Map();

const palette = {
    primary: '#2563eb',
    primarySoft: 'rgba(37, 99, 235, 0.88)',
    accent: '#0284c7',
    success: '#059669',
    successSoft: 'rgba(5, 150, 105, 0.9)',
    warning: '#d97706',
    warningSoft: 'rgba(217, 119, 6, 0.88)',
    danger: '#dc2626',
    muted: '#94a3b8',
    grid: 'rgba(148, 163, 184, 0.16)',
    text: '#64748b',
    ink: '#0f172a'
};

const workspaceColors = [
    '#2563eb', '#0891b2', '#059669', '#d97706', '#4f46e5', '#0ea5e9', '#ea580c', '#0284c7'
];

function ensureChartJs() {
    if (typeof globalThis.Chart === 'undefined') {
        throw new Error('Chart.js is not loaded. Ensure the Chart.js script is included.');
    }
}

function destroyChart(canvasId) {
    const existing = charts.get(canvasId);
    if (existing) {
        existing.destroy();
        charts.delete(canvasId);
    }
}

function tooltipDefaults() {
    return {
        backgroundColor: palette.ink,
        titleColor: '#f8fafc',
        bodyColor: '#e2e8f0',
        padding: 12,
        cornerRadius: 10,
        displayColors: true,
        boxPadding: 4,
        titleFont: { size: 12, weight: '600' },
        bodyFont: { size: 12, weight: '500' }
    };
}

function legendDefaults() {
    return {
        position: 'top',
        align: 'end',
        labels: {
            boxWidth: 10,
            boxHeight: 10,
            usePointStyle: true,
            pointStyle: 'rectRounded',
            color: palette.text,
            font: { size: 12, weight: '500' },
            padding: 16
        }
    };
}

export function destroy(canvasId) {
    destroyChart(canvasId);
}

export function destroyAll() {
    for (const id of [...charts.keys()]) {
        destroyChart(id);
    }
}

export function renderDeveloperThroughput(canvasId, labels, completed, inProgress) {
    ensureChartJs();
    destroyChart(canvasId);

    const canvas = document.getElementById(canvasId);
    if (!canvas) {
        return;
    }

    const chart = new Chart(canvas, {
        type: 'bar',
        data: {
            labels,
            datasets: [
                {
                    label: 'Completed',
                    data: completed,
                    backgroundColor: palette.successSoft,
                    hoverBackgroundColor: palette.success,
                    borderRadius: 8,
                    borderSkipped: false,
                    maxBarThickness: 34,
                    barPercentage: 0.72,
                    categoryPercentage: 0.7
                },
                {
                    label: 'In Progress',
                    data: inProgress,
                    backgroundColor: palette.warningSoft,
                    hoverBackgroundColor: palette.warning,
                    borderRadius: 8,
                    borderSkipped: false,
                    maxBarThickness: 34,
                    barPercentage: 0.72,
                    categoryPercentage: 0.7
                }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            interaction: { mode: 'index', intersect: false },
            animation: {
                duration: 700,
                easing: 'easeOutQuart'
            },
            plugins: {
                legend: legendDefaults(),
                tooltip: tooltipDefaults()
            },
            scales: {
                x: {
                    stacked: true,
                    grid: { display: false },
                    border: { display: false },
                    ticks: {
                        color: palette.text,
                        font: { size: 11, weight: '500' },
                        maxRotation: 40,
                        minRotation: 0
                    }
                },
                y: {
                    stacked: true,
                    beginAtZero: true,
                    border: { display: false },
                    ticks: {
                        precision: 0,
                        color: palette.text,
                        font: { size: 11 }
                    },
                    grid: {
                        color: palette.grid,
                        drawTicks: false
                    }
                }
            }
        }
    });

    charts.set(canvasId, chart);
}

export function renderWorkspaceShare(canvasId, labels, values, chartType = 'pie') {
    ensureChartJs();
    destroyChart(canvasId);

    const canvas = document.getElementById(canvasId);
    if (!canvas) {
        return;
    }

    const isDoughnut = chartType === 'doughnut';
    const chart = new Chart(canvas, {
        type: isDoughnut ? 'doughnut' : 'pie',
        data: {
            labels,
            datasets: [{
                data: values,
                backgroundColor: labels.map((_, i) => workspaceColors[i % workspaceColors.length]),
                borderWidth: 3,
                borderColor: '#ffffff',
                hoverOffset: 8,
                spacing: 2
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            cutout: isDoughnut ? '70%' : 0,
            animation: {
                animateRotate: true,
                duration: 800
            },
            plugins: {
                legend: {
                    position: 'bottom',
                    labels: {
                        boxWidth: 10,
                        boxHeight: 10,
                        usePointStyle: true,
                        pointStyle: 'circle',
                        color: palette.text,
                        font: { size: 12, weight: '500' },
                        padding: 14
                    }
                },
                tooltip: {
                    ...tooltipDefaults(),
                    callbacks: {
                        label(context) {
                            const total = context.dataset.data.reduce((a, b) => a + b, 0);
                            const value = context.parsed;
                            const pct = total > 0 ? ((value / total) * 100).toFixed(1) : 0;
                            return ` ${context.label}: ${value} (${pct}%)`;
                        }
                    }
                }
            }
        }
    });

    charts.set(canvasId, chart);
}

export function renderStatusMix(canvasId, completed, inProgress, overdue) {
    ensureChartJs();
    destroyChart(canvasId);

    const canvas = document.getElementById(canvasId);
    if (!canvas) {
        return;
    }

    const chart = new Chart(canvas, {
        type: 'doughnut',
        data: {
            labels: ['Completed', 'In Progress', 'Overdue'],
            datasets: [{
                data: [completed, inProgress, overdue],
                backgroundColor: [palette.success, palette.warning, palette.danger],
                borderWidth: 3,
                borderColor: '#ffffff',
                hoverOffset: 8,
                spacing: 2
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            cutout: '70%',
            animation: {
                animateRotate: true,
                duration: 800
            },
            plugins: {
                legend: {
                    position: 'bottom',
                    labels: {
                        boxWidth: 10,
                        boxHeight: 10,
                        usePointStyle: true,
                        pointStyle: 'circle',
                        color: palette.text,
                        font: { size: 12, weight: '500' },
                        padding: 14
                    }
                },
                tooltip: {
                    ...tooltipDefaults(),
                    callbacks: {
                        label(context) {
                            const total = context.dataset.data.reduce((a, b) => a + b, 0);
                            const value = context.parsed;
                            const pct = total > 0 ? ((value / total) * 100).toFixed(1) : 0;
                            return ` ${context.label}: ${value} (${pct}%)`;
                        }
                    }
                }
            }
        }
    });

    charts.set(canvasId, chart);
}

export function renderWeeklyThroughput(canvasId, labels, values) {
    ensureChartJs();
    destroyChart(canvasId);

    const canvas = document.getElementById(canvasId);
    if (!canvas) {
        return;
    }

    const chart = new Chart(canvas, {
        type: 'line',
        data: {
            labels,
            datasets: [{
                label: 'Tasks completed',
                data: values,
                borderColor: palette.success,
                backgroundColor: 'rgba(5, 150, 105, 0.12)',
                fill: true,
                tension: 0.35,
                pointRadius: 4,
                pointHoverRadius: 6,
                pointBackgroundColor: palette.success,
                pointBorderColor: '#ffffff',
                pointBorderWidth: 2,
                borderWidth: 2.5
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            interaction: { mode: 'index', intersect: false },
            animation: {
                duration: 700,
                easing: 'easeOutQuart'
            },
            plugins: {
                legend: { display: false },
                tooltip: tooltipDefaults()
            },
            scales: {
                x: {
                    grid: { display: false },
                    border: { display: false },
                    ticks: {
                        color: palette.text,
                        font: { size: 11, weight: '500' },
                        maxRotation: 45,
                        minRotation: 0
                    }
                },
                y: {
                    beginAtZero: true,
                    border: { display: false },
                    ticks: {
                        precision: 0,
                        color: palette.text,
                        font: { size: 11 }
                    },
                    grid: {
                        color: palette.grid,
                        drawTicks: false
                    }
                }
            }
        }
    });

    charts.set(canvasId, chart);
}

export function renderLeaveBalanceMix(canvasId, consumed, pending, remaining) {
    ensureChartJs();
    destroyChart(canvasId);

    const canvas = document.getElementById(canvasId);
    if (!canvas) {
        return;
    }

    const chart = new Chart(canvas, {
        type: 'doughnut',
        data: {
            labels: ['Consumed', 'Pending', 'Remaining'],
            datasets: [{
                data: [consumed, pending, remaining],
                backgroundColor: [palette.success, palette.warning, palette.muted],
                borderWidth: 3,
                borderColor: '#ffffff',
                hoverOffset: 8,
                spacing: 2
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            cutout: '68%',
            animation: {
                animateRotate: true,
                duration: 800
            },
            plugins: {
                legend: {
                    position: 'bottom',
                    labels: {
                        boxWidth: 10,
                        boxHeight: 10,
                        usePointStyle: true,
                        pointStyle: 'circle',
                        color: palette.text,
                        font: { size: 12, weight: '500' },
                        padding: 14
                    }
                },
                tooltip: {
                    ...tooltipDefaults(),
                    callbacks: {
                        label(context) {
                            const total = context.dataset.data.reduce((a, b) => a + b, 0);
                            const value = context.parsed;
                            const pct = total > 0 ? ((value / total) * 100).toFixed(1) : 0;
                            return ` ${context.label}: ${value.toFixed(1)} day(s) (${pct}%)`;
                        }
                    }
                }
            }
        }
    });

    charts.set(canvasId, chart);
}

export function renderDeveloperLeaveStacked(canvasId, labels, consumed, pending, remaining) {
    ensureChartJs();
    destroyChart(canvasId);

    const canvas = document.getElementById(canvasId);
    if (!canvas) {
        return;
    }

    const chart = new Chart(canvas, {
        type: 'bar',
        data: {
            labels,
            datasets: [
                {
                    label: 'Consumed',
                    data: consumed,
                    backgroundColor: palette.successSoft,
                    hoverBackgroundColor: palette.success,
                    borderRadius: 6,
                    borderSkipped: false,
                    maxBarThickness: 36,
                    barPercentage: 0.72,
                    categoryPercentage: 0.7
                },
                {
                    label: 'Pending',
                    data: pending,
                    backgroundColor: palette.warningSoft,
                    hoverBackgroundColor: palette.warning,
                    borderRadius: 6,
                    borderSkipped: false,
                    maxBarThickness: 36,
                    barPercentage: 0.72,
                    categoryPercentage: 0.7
                },
                {
                    label: 'Remaining',
                    data: remaining,
                    backgroundColor: 'rgba(148, 163, 184, 0.55)',
                    hoverBackgroundColor: palette.muted,
                    borderRadius: 6,
                    borderSkipped: false,
                    maxBarThickness: 36,
                    barPercentage: 0.72,
                    categoryPercentage: 0.7
                }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            interaction: { mode: 'index', intersect: false },
            animation: {
                duration: 700,
                easing: 'easeOutQuart'
            },
            plugins: {
                legend: legendDefaults(),
                tooltip: {
                    ...tooltipDefaults(),
                    callbacks: {
                        label(context) {
                            return ` ${context.dataset.label}: ${context.parsed.y.toFixed(1)} day(s)`;
                        }
                    }
                }
            },
            scales: {
                x: {
                    stacked: true,
                    grid: { display: false },
                    border: { display: false },
                    ticks: {
                        color: palette.text,
                        font: { size: 11, weight: '500' },
                        maxRotation: 45,
                        minRotation: 0
                    }
                },
                y: {
                    stacked: true,
                    beginAtZero: true,
                    border: { display: false },
                    ticks: {
                        color: palette.text,
                        font: { size: 11 },
                        callback(value) {
                            return `${value}d`;
                        }
                    },
                    grid: {
                        color: palette.grid,
                        drawTicks: false
                    }
                }
            }
        }
    });

    charts.set(canvasId, chart);
}

export function renderLeaveTypeBreakdown(canvasId, labels, consumed, pending) {
    ensureChartJs();
    destroyChart(canvasId);

    const canvas = document.getElementById(canvasId);
    if (!canvas) {
        return;
    }

    const chart = new Chart(canvas, {
        type: 'bar',
        data: {
            labels,
            datasets: [
                {
                    label: 'Consumed',
                    data: consumed,
                    backgroundColor: palette.successSoft,
                    hoverBackgroundColor: palette.success,
                    borderRadius: 8,
                    borderSkipped: false,
                    maxBarThickness: 40,
                    barPercentage: 0.65,
                    categoryPercentage: 0.65
                },
                {
                    label: 'Pending',
                    data: pending,
                    backgroundColor: palette.warningSoft,
                    hoverBackgroundColor: palette.warning,
                    borderRadius: 8,
                    borderSkipped: false,
                    maxBarThickness: 40,
                    barPercentage: 0.65,
                    categoryPercentage: 0.65
                }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            interaction: { mode: 'index', intersect: false },
            animation: {
                duration: 700,
                easing: 'easeOutQuart'
            },
            plugins: {
                legend: legendDefaults(),
                tooltip: {
                    ...tooltipDefaults(),
                    callbacks: {
                        label(context) {
                            return ` ${context.dataset.label}: ${context.parsed.y.toFixed(1)} day(s)`;
                        }
                    }
                }
            },
            scales: {
                x: {
                    grid: { display: false },
                    border: { display: false },
                    ticks: {
                        color: palette.text,
                        font: { size: 11, weight: '500' }
                    }
                },
                y: {
                    beginAtZero: true,
                    border: { display: false },
                    ticks: {
                        color: palette.text,
                        font: { size: 11 },
                        callback(value) {
                            return `${value}d`;
                        }
                    },
                    grid: {
                        color: palette.grid,
                        drawTicks: false
                    }
                }
            }
        }
    });

    charts.set(canvasId, chart);
}

export function renderLeavePendingRanking(canvasId, labels, pendingDays) {
    ensureChartJs();
    destroyChart(canvasId);

    const canvas = document.getElementById(canvasId);
    if (!canvas) {
        return;
    }

    const chart = new Chart(canvas, {
        type: 'bar',
        data: {
            labels,
            datasets: [{
                label: 'Pending days',
                data: pendingDays,
                backgroundColor: palette.warningSoft,
                hoverBackgroundColor: palette.warning,
                borderRadius: 8,
                borderSkipped: false,
                maxBarThickness: 28,
                barPercentage: 0.6,
                categoryPercentage: 0.75
            }]
        },
        options: {
            indexAxis: 'y',
            responsive: true,
            maintainAspectRatio: false,
            animation: {
                duration: 700,
                easing: 'easeOutQuart'
            },
            plugins: {
                legend: { display: false },
                tooltip: {
                    ...tooltipDefaults(),
                    callbacks: {
                        label(context) {
                            return ` ${context.parsed.x.toFixed(1)} day(s) pending`;
                        }
                    }
                }
            },
            scales: {
                x: {
                    beginAtZero: true,
                    border: { display: false },
                    ticks: {
                        color: palette.text,
                        font: { size: 11 },
                        callback(value) {
                            return `${value}d`;
                        }
                    },
                    grid: {
                        color: palette.grid,
                        drawTicks: false
                    }
                },
                y: {
                    grid: { display: false },
                    border: { display: false },
                    ticks: {
                        color: palette.text,
                        font: { size: 11, weight: '500' }
                    }
                }
            }
        }
    });

    charts.set(canvasId, chart);
}
