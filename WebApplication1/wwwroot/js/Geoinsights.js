/**
 * geoInsights.js — Módulo compartido para Admin/GeoInsights y Municipio/GeoInsights.
 *
 * Uso:
 *   GeoInsights.init({
 *       geoData:         object|null,    // datos iniciales (null => carga vía AJAX)
 *       geoDataUrlBase:  string|null,    // URL para AJAX (solo Admin)
 *       detailUrlBase:   string,         // ej: '/AdminRecipes/Details/' o '/Municipio/Details/'
 *       serverError:     string|null,    // error del servidor en la carga inicial
 *       useAjaxFilters:  bool,           // true = fetch sin recargar, false = form submit
 *       heatGradient:    object|null     // gradient personalizado para heatmap (null = default)
 *   });
 */
window.GeoInsights = (function () {
    'use strict';

    // ===== STATE =====
    let map, polygonLayers, sensitiveLayer, heatLayer;
    let timelineChart, toxChart, cropChart;
    let isLoading = false;
    let config = {};

    // ===== HELPERS =====

    function escapeHtml(str) {
        if (!str) return '';
        const div = document.createElement('div');
        div.textContent = str;
        return div.innerHTML;
    }

    function toxColor(score) {
        if (score <= 1) return { color: '#8e44ad', fill: 'rgba(142, 68, 173, 0.25)' };
        if (score <= 2) return { color: '#e74c3c', fill: 'rgba(231, 76, 60, 0.25)' };
        if (score <= 3) return { color: '#f39c12', fill: 'rgba(243, 156, 18, 0.25)' };
        if (score <= 4) return { color: '#3498db', fill: 'rgba(52, 152, 219, 0.25)' };
        return { color: '#00b400', fill: 'rgba(0, 180, 0, 0.25)' };
    }

    function showError(msg) {
        const bar = document.getElementById('geoErrorBar');
        const txt = document.getElementById('geoErrorText');
        if (txt) txt.textContent = msg || 'Ocurrió un error.';
        if (bar) bar.style.display = 'flex';
    }

    function hideError() {
        const bar = document.getElementById('geoErrorBar');
        const txt = document.getElementById('geoErrorText');
        if (bar) bar.style.display = 'none';
        if (txt) txt.textContent = '';
    }

    function populateSelect(id, items, selectedValue) {
        const select = document.getElementById(id);
        if (!select) return;
        while (select.options.length > 1) select.remove(1);
        if (!items) return;
        items.forEach(item => {
            const opt = document.createElement('option');
            opt.value = item;
            opt.textContent = item;
            if (selectedValue && item === selectedValue) opt.selected = true;
            select.appendChild(opt);
        });
    }

    // ===== FILTERS =====

    function getFiltersFromForm() {
        const get = id => document.getElementById(id)?.value || '';
        return {
            municipalityId: get('filterMunicipality'),
            dateFrom: get('filterDateFrom'),
            dateTo: get('filterDateTo'),
            crop: get('filterCrop'),
            toxClass: get('filterToxClass'),
            productName: get('filterProduct'),
            advisorName: get('filterAdvisor')
        };
    }

    function setFormFromFilters(filters) {
        if (!filters) return;
        const set = (id, val) => {
            const el = document.getElementById(id);
            if (el) el.value = val ?? '';
        };
        set('filterMunicipality', filters.municipalityId);
        set('filterDateFrom', filters.dateFrom);
        set('filterDateTo', filters.dateTo);
        set('filterCrop', filters.crop);
        set('filterToxClass', filters.toxClass);
        set('filterProduct', filters.productName);
        set('filterAdvisor', filters.advisorName);
    }

    function buildQueryString(filters) {
        const qs = new URLSearchParams();
        Object.entries(filters).forEach(([k, v]) => { if (v) qs.set(k, v); });
        return qs.toString();
    }

    function getInitialFiltersFromQuery() {
        const p = new URLSearchParams(window.location.search);
        return {
            municipalityId: p.get('municipalityId') || '',
            dateFrom: p.get('dateFrom') || '',
            dateTo: p.get('dateTo') || '',
            crop: p.get('crop') || '',
            toxClass: p.get('toxClass') || '',
            productName: p.get('productName') || '',
            advisorName: p.get('advisorName') || ''
        };
    }

    // ===== KPIs =====

    function updateKpis(data) {
        const k = data?.kpis;
        if (!k) return;
        const el = id => document.getElementById(id);
        if (el('kpiTotalApps')) el('kpiTotalApps').textContent = k.totalApplications || 0;
        if (el('kpiHectares')) el('kpiHectares').textContent = (k.totalHectares || 0).toLocaleString('es-AR', { maximumFractionDigits: 0 });
        if (el('kpiProducts')) el('kpiProducts').textContent = k.uniqueProducts || 0;
        if (el('kpiHighTox')) el('kpiHighTox').textContent = k.highToxApplications || 0;
        if (el('kpiAdvisors')) el('kpiAdvisors').textContent = k.uniqueAdvisors || 0;
    }

    // ===== MAP =====

    function initMap() {
        map = L.map('geoMap', { zoomControl: true });

        requestAnimationFrame(() => map.invalidateSize());
        setTimeout(() => map.invalidateSize(), 150);

        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OSM</a>',
            maxZoom: 19
        }).addTo(map);

        polygonLayers = L.layerGroup().addTo(map);
        sensitiveLayer = L.layerGroup().addTo(map);
    }

    function clearMapLayers() {
        polygonLayers.clearLayers();
        sensitiveLayer.clearLayers();
        if (heatLayer) {
            try { map.removeLayer(heatLayer); } catch (e) { /* ignore */ }
            heatLayer = null;
        }
    }

    function tryAddHeatLayer(heatPoints) {
        const size = map.getSize();
        if (!size || size.x <= 0 || size.y <= 0) {
            setTimeout(() => {
                map.invalidateSize();
                tryAddHeatLayer(heatPoints);
            }, 120);
            return;
        }

        if (heatPoints.length > 0 && typeof L.heatLayer === 'function') {
            const opts = {
                radius: 30,
                blur: 20,
                maxZoom: 15,
                max: 1.0
            };
            if (config.heatGradient) opts.gradient = config.heatGradient;

            heatLayer = L.heatLayer(heatPoints, opts).addTo(map);
        }
    }

    function buildPopupHtml(app, tc) {
        const productsHtml = (app.products || []).map(p =>
            '<span class="popup-product">' + escapeHtml(p.productName) +
            ' <small>(' + escapeHtml(p.toxicologicalClass || '?') + ')</small></span>'
        ).join('');

        const statusClass = (app.status || '').toLowerCase();
        const detailUrl = config.detailUrlBase + app.recipeId;

        return '<div class="geo-popup">' +
            '<div class="geo-popup-header">' +
                '<strong>RFD #' + escapeHtml(String(app.rfdNumber)) + '</strong>' +
                '<span class="geo-popup-status geo-popup-status-' + statusClass + '">' + escapeHtml(app.status) + '</span>' +
            '</div>' +
            '<div class="geo-popup-body">' +
                '<div><strong>Lote:</strong> ' + escapeHtml(app.lotName) + '</div>' +
                '<div><strong>Cultivo:</strong> ' + escapeHtml(app.crop || '-') + '</div>' +
                '<div><strong>Superficie:</strong> ' + (app.surfaceHa ? app.surfaceHa.toFixed(1) + ' ha' : '-') + '</div>' +
                '<div><strong>Fecha:</strong> ' + new Date(app.issueDate).toLocaleDateString('es-AR') + '</div>' +
                '<div><strong>Asesor:</strong> ' + escapeHtml(app.advisorName || '-') + '</div>' +
                '<div><strong>Solicitante:</strong> ' + escapeHtml(app.requesterName || '-') + '</div>' +
                '<div class="geo-popup-products"><strong>Productos:</strong><br/>' + productsHtml + '</div>' +
                '<div><strong>Toxicidad máx:</strong> <span style="color:' + tc.color + '; font-weight:700;">' + escapeHtml(app.maxToxClass || 'N/D') + '</span></div>' +
            '</div>' +
            '<a href="' + detailUrl + '" class="geo-popup-link">Ver receta →</a>' +
        '</div>';
    }

    function buildSensitivePopupHtml(sp) {
        return '<div class="geo-popup">' +
            '<div class="geo-popup-header">' +
                '<strong>📍 ' + escapeHtml(sp.name) + '</strong>' +
            '</div>' +
            '<div class="geo-popup-body">' +
                '<div><strong>Tipo:</strong> ' + escapeHtml(sp.type || '-') + '</div>' +
                '<div><strong>Localidad:</strong> ' + escapeHtml(sp.locality || '-') + '</div>' +
            '</div>' +
        '</div>';
    }

    // ===== LOT POPUP (nuevo: muestra historial de aplicaciones del lote) =====

    function buildLotPopupHtml(lot) {
        const tc = toxColor(lot.lastToxScore || 99);
        const apps = lot.applications || [];
        const count = lot.applicationsCount || apps.length;

        let header = '<div class="geo-popup-header">' +
            '<strong>' + escapeHtml(lot.lotName || 'Lote') + '</strong>' +
            '<span style="font-size:0.8rem; color:#666;"> (' + count + ' aplicacion' + (count !== 1 ? 'es' : '') + ')</span>' +
        '</div>';

        let body = '<div class="geo-popup-body">';
        if (lot.locality) body += '<div><strong>Localidad:</strong> ' + escapeHtml(lot.locality) + '</div>';
        if (lot.department) body += '<div><strong>Departamento:</strong> ' + escapeHtml(lot.department) + '</div>';
        if (lot.surfaceHa) body += '<div><strong>Superficie:</strong> ' + lot.surfaceHa.toFixed(1) + ' ha</div>';
        if (lot.lastApplicationDate) body += '<div><strong>Última aplicación:</strong> ' + new Date(lot.lastApplicationDate).toLocaleDateString('es-AR') + '</div>';
        if (lot.lastMaxToxClass) body += '<div><strong>Toxicidad máx (última):</strong> <span style="color:' + tc.color + '; font-weight:700;">' + escapeHtml(lot.lastMaxToxClass) + '</span></div>';

        // Lista de aplicaciones (últimas 5)
        if (apps.length > 0) {
            body += '<hr style="margin:6px 0; border:none; border-top:1px solid #eee;">';
            body += '<div style="font-weight:600; margin-bottom:4px;">Aplicaciones:</div>';
            const showing = apps.slice(0, 5);
            showing.forEach(app => {
                const appTc = toxColor(app.toxScore);
                const date = app.applicationDate
                    ? new Date(app.applicationDate).toLocaleDateString('es-AR')
                    : new Date(app.issueDate).toLocaleDateString('es-AR');
                const prods = (app.products || []).map(p => p.productName).join(', ');
                const detailUrl = config.detailUrlBase + app.recipeId;

                body += '<div style="margin-bottom:4px; padding:3px 0; border-bottom:1px solid #f5f5f5;">' +
                    '<a href="' + detailUrl + '" style="font-weight:600; color:#2c3e50;">RFD #' + app.rfdNumber + '</a>' +
                    ' <small>(' + date + ')</small>' +
                    '<span style="color:' + appTc.color + '; font-weight:600; margin-left:6px;">' + escapeHtml(app.maxToxClass || '') + '</span>' +
                    (prods ? '<div style="font-size:0.8rem; color:#666;">' + escapeHtml(prods) + '</div>' : '') +
                '</div>';
            });
            if (apps.length > 5) {
                body += '<div style="font-size:0.8rem; color:#999;">... y ' + (apps.length - 5) + ' más</div>';
            }
        }

        body += '</div>';
        return '<div class="geo-popup">' + header + body + '</div>';
    }

    function redrawMap(data, preserveView) {
        const prevCenter = preserveView ? map.getCenter() : null;
        const prevZoom = preserveView ? map.getZoom() : null;

        clearMapLayers();

        const bounds = L.latLngBounds();
        const heatPoints = [];

        // Nuevo: usar lots[] si disponible (1 polígono por lote, sin duplicados)
        const lots = data?.lots || [];

        if (lots.length > 0) {
            lots.forEach(lot => {
                if (!lot.vertices || lot.vertices.length === 0) return;

                const coords = lot.vertices.map(v => [v.lat, v.lng]);
                const tc = toxColor(lot.lastToxScore || 99);

                const polygon = L.polygon(coords, {
                    color: tc.color,
                    weight: 2,
                    fillColor: tc.fill,
                    fillOpacity: 0.35,
                    className: 'geo-polygon'
                });

                polygon.bindPopup(buildLotPopupHtml(lot), { maxWidth: 360, maxHeight: 400 });
                polygonLayers.addLayer(polygon);

                coords.forEach(c => bounds.extend(c));

                if (lot.centerLat != null && lot.centerLng != null) {
                    // Heatmap weight = applicationsCount (más reincidencia = más intenso)
                    const weight = lot.lastToxScore <= 2 ? 1.0 : lot.lastToxScore <= 3 ? 0.6 : 0.3;
                    const appWeight = Math.min(lot.applicationsCount || 1, 5);
                    heatPoints.push([lot.centerLat, lot.centerLng, weight * appWeight]);
                }
            });
        } else {
            // Fallback legacy: applications[] (para datos sin lots)
            const apps = data?.applications || [];
            apps.forEach(app => {
                if (!app.vertices || app.vertices.length === 0) return;

                const coords = app.vertices.map(v => [v.lat, v.lng]);
                const tc = toxColor(app.toxScore);

                const polygon = L.polygon(coords, {
                    color: tc.color,
                    weight: 2,
                    fillColor: tc.fill,
                    fillOpacity: 0.35,
                    className: 'geo-polygon'
                });

                polygon.bindPopup(buildPopupHtml(app, tc), { maxWidth: 320 });
                polygonLayers.addLayer(polygon);

                coords.forEach(c => bounds.extend(c));

                if (app.centerLat != null && app.centerLng != null) {
                    const weight = app.toxScore <= 2 ? 1.0 : app.toxScore <= 3 ? 0.6 : 0.3;
                    heatPoints.push([app.centerLat, app.centerLng, weight]);
                }
            });
        }

        const sensitivePoints = data?.sensitivePoints || [];
        sensitivePoints.forEach(sp => {
            const icon = L.divIcon({
                html: '<div class="sp-marker"><span>📍</span></div>',
                className: '',
                iconSize: [28, 28],
                iconAnchor: [14, 14],
                popupAnchor: [0, -16]
            });

            const marker = L.marker([sp.latitude, sp.longitude], { icon });
            marker.bindPopup(buildSensitivePopupHtml(sp), { maxWidth: 300 });
            sensitiveLayer.addLayer(marker);
            bounds.extend([sp.latitude, sp.longitude]);
        });

        tryAddHeatLayer(heatPoints);

        if (preserveView && prevCenter && prevZoom != null) {
            map.setView(prevCenter, prevZoom);
        } else {
            if (bounds.isValid()) map.fitBounds(bounds, { padding: [50, 50], maxZoom: 14 });
            else map.setView([-33.0, -61.0], 8);
        }
    }

    // ===== CHARTS =====

    function renderCharts(data) {
        // Usar applications[] para charts (datos planos, 1 fila por receta/lote)
        const apps = data?.applications || [];
        if (!apps.length || typeof Chart === 'undefined') return;

        if (timelineChart) { try { timelineChart.destroy(); } catch (e) { /* */ } timelineChart = null; }
        if (toxChart) { try { toxChart.destroy(); } catch (e) { /* */ } toxChart = null; }
        if (cropChart) { try { cropChart.destroy(); } catch (e) { /* */ } cropChart = null; }

        // Timeline
        const monthMap = {};
        apps.forEach(app => {
            const d = new Date(app.issueDate);
            const key = d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0');
            if (!monthMap[key]) monthMap[key] = { total: 0, highTox: 0 };
            monthMap[key].total++;
            if (app.toxScore <= 2) monthMap[key].highTox++;
        });

        const sortedMonths = Object.keys(monthMap).sort();
        const timelineLabels = sortedMonths.map(k => {
            const [y, m] = k.split('-');
            return new Date(y, m - 1).toLocaleDateString('es-AR', { month: 'short', year: '2-digit' });
        });

        const tlEl = document.getElementById('timelineChart');
        if (tlEl && sortedMonths.length) {
            timelineChart = new Chart(tlEl, {
                type: 'bar',
                data: {
                    labels: timelineLabels,
                    datasets: [
                        {
                            label: 'Total aplicaciones',
                            data: sortedMonths.map(k => monthMap[k].total),
                            backgroundColor: 'rgba(52, 152, 219, 0.6)',
                            borderColor: '#3498db',
                            borderWidth: 1,
                            borderRadius: 4
                        },
                        {
                            label: 'Alta toxicidad (Clase I/II)',
                            data: sortedMonths.map(k => monthMap[k].highTox),
                            backgroundColor: 'rgba(231, 76, 60, 0.6)',
                            borderColor: '#e74c3c',
                            borderWidth: 1,
                            borderRadius: 4
                        }
                    ]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: { legend: { position: 'top', labels: { usePointStyle: true, padding: 16 } } },
                    scales: { y: { beginAtZero: true, ticks: { stepSize: 1, precision: 0 } } }
                }
            });
        }

        // Tox doughnut
        const toxGroups = {};
        apps.forEach(app => {
            const cls = app.maxToxClass || 'Sin clasificar';
            toxGroups[cls] = (toxGroups[cls] || 0) + 1;
        });

        const toxLabels = Object.keys(toxGroups);
        const toxData = Object.values(toxGroups);
        const toxColors = toxLabels.map(l => {
            if (l.includes('Ia')) return '#8e44ad';
            if (l.includes('Ib')) return '#e74c3c';
            if (l.includes('II') && !l.includes('III')) return '#f39c12';
            if (l.includes('III') && !l.includes('IV')) return '#3498db';
            if (l.includes('IV')) return '#2ecc71';
            return '#95a5a6';
        });

        const toxEl = document.getElementById('toxChart');
        if (toxEl) {
            toxChart = new Chart(toxEl, {
                type: 'doughnut',
                data: {
                    labels: toxLabels,
                    datasets: [{ data: toxData, backgroundColor: toxColors, borderWidth: 2, borderColor: '#fff' }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: { legend: { position: 'bottom', labels: { padding: 14, usePointStyle: true } } }
                }
            });
        }

        // Crop bar
        const cropGroups = {};
        apps.forEach(app => {
            const c = (app.crop || 'Sin especificar').trim();
            cropGroups[c] = (cropGroups[c] || 0) + 1;
        });

        const cropLabels = Object.keys(cropGroups).sort((a, b) => cropGroups[b] - cropGroups[a]).slice(0, 8);
        const cropData = cropLabels.map(l => cropGroups[l]);
        const cropColors = ['#3498db', '#2ecc71', '#f39c12', '#e74c3c', '#9b59b6', '#1abc9c', '#e67e22', '#34495e'];

        const cropEl = document.getElementById('cropChart');
        if (cropEl && cropLabels.length) {
            cropChart = new Chart(cropEl, {
                type: 'bar',
                data: {
                    labels: cropLabels,
                    datasets: [{
                        label: 'Aplicaciones',
                        data: cropData,
                        backgroundColor: cropColors.slice(0, cropLabels.length),
                        borderRadius: 6,
                        borderSkipped: false
                    }]
                },
                options: {
                    indexAxis: 'y',
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: { legend: { display: false } },
                    scales: { x: { beginAtZero: true, ticks: { stepSize: 1, precision: 0 } } }
                }
            });
        }
    }

    // ===== APPLY DATA =====

    function applyGeoData(data, filters, preserveMapView) {
        if (!data) return;

        updateKpis(data);

        if (data.availableFilters) {
            const f = data.availableFilters;
            populateSelect('filterCrop', f.crops, filters?.crop || '');
            populateSelect('filterToxClass', f.toxClasses, filters?.toxClass || '');
            populateSelect('filterProduct', f.products, filters?.productName || '');
            populateSelect('filterAdvisor', f.advisors, filters?.advisorName || '');
        }

        redrawMap(data, preserveMapView);
        renderCharts(data);
    }

    // ===== AJAX LOAD (Admin only) =====

    async function loadGeoInsights(filters, preserveMapView) {
        if (isLoading || !config.geoDataUrlBase) return;
        isLoading = true;
        hideError();

        const qs = buildQueryString(filters);
        const url = qs ? config.geoDataUrlBase + '?' + qs : config.geoDataUrlBase;

        try {
            const resp = await fetch(url, { method: 'GET', headers: { 'Accept': 'application/json' } });

            if (!resp.ok) {
                let msg = 'No se pudieron obtener datos geoespaciales. HTTP ' + resp.status;
                try {
                    const err = await resp.json();
                    if (err?.message) msg = err.message;
                    if (err?.error) msg = err.error;
                } catch (e) { /* ignore */ }
                showError(msg);
                return;
            }

            const data = await resp.json();
            applyGeoData(data, filters, preserveMapView);

            try {
                history.replaceState({ geoFilters: filters }, '', window.location.pathname);
            } catch (e) { /* ignore */ }
        } catch (e) {
            showError('No se pudieron obtener datos geoespaciales. Error de red.');
        } finally {
            isLoading = false;
        }
    }

    // ===== TOGGLES =====

    function setupToggles() {
        function setupToggle(desktopId, mobileId, layerRef, isHeat) {
            const dEl = document.getElementById(desktopId);
            const mEl = document.getElementById(mobileId);

            function toggle(checked) {
                if (isHeat && heatLayer) {
                    checked ? map.addLayer(heatLayer) : map.removeLayer(heatLayer);
                } else if (!isHeat) {
                    checked ? map.addLayer(layerRef) : map.removeLayer(layerRef);
                }
                if (dEl) dEl.checked = checked;
                if (mEl) mEl.checked = checked;
            }

            if (dEl) dEl.addEventListener('change', () => toggle(dEl.checked));
            if (mEl) mEl.addEventListener('change', () => toggle(mEl.checked));
        }

        // Support both naming conventions (Admin: toggleHeat, Municipio: toggleHeatmap)
        const heatDesktop = document.getElementById('toggleHeat') || document.getElementById('toggleHeatmap');
        const heatMobile = document.getElementById('toggleHeatMobile') || document.getElementById('toggleHeatmapMobile');
        const heatDesktopId = heatDesktop?.id;
        const heatMobileId = heatMobile?.id;

        if (heatDesktopId || heatMobileId) {
            setupToggle(heatDesktopId, heatMobileId, null, true);
        }

        setupToggle('togglePolygons', 'togglePolygonsMobile', polygonLayers, false);
        setupToggle('toggleSensitive', 'toggleSensitiveMobile', sensitiveLayer, false);
    }

    function setupLegendToggle() {
        const legendEl = document.getElementById('mapLegend');
        const legendBtn = document.getElementById('legendToggleBtn');
        if (legendEl && legendBtn) {
            legendBtn.addEventListener('click', function (e) {
                e.stopPropagation();
                legendEl.classList.toggle('collapsed');
            });
        }
    }

    function setupMapEventBlocking() {
        const controlsEl = document.querySelector('.geo-controls-desktop');
        if (controlsEl) L.DomEvent.disableClickPropagation(controlsEl);

        const mobileCtrl = document.getElementById('mobileControls');
        if (mobileCtrl) {
            L.DomEvent.disableClickPropagation(mobileCtrl);
            L.DomEvent.disableScrollPropagation(mobileCtrl);
        }
    }

    // ===== INIT =====

    function init(opts) {
        config = opts || {};

        if (typeof L === 'undefined') {
            showError('No se pudo cargar el mapa (Leaflet no disponible).');
            return;
        }

        if (config.serverError) showError(config.serverError);

        initMap();
        setupMapEventBlocking();
        setupLegendToggle();
        setupToggles();

        // Determine initial filters
        const stateFilters = history.state?.geoFilters;
        const queryFilters = getInitialFiltersFromQuery();
        const filters = stateFilters || queryFilters || {};

        setFormFromFilters(filters);

        // Form handling
        const form = document.getElementById('filtersForm');
        if (form && config.useAjaxFilters) {
            form.addEventListener('submit', async (ev) => {
                ev.preventDefault();
                await loadGeoInsights(getFiltersFromForm(), true);
            });
        }

        // Clear filters button
        const btnClear = document.getElementById('btnClearFilters');
        if (btnClear) {
            btnClear.addEventListener('click', async () => {
                if (form) form.reset();
                if (config.useAjaxFilters) {
                    const emptyFilters = getFiltersFromForm();
                    await loadGeoInsights(emptyFilters, true);
                } else {
                    window.location.href = window.location.pathname;
                }
            });
        }

        // Load initial data
        if (config.geoData) {
            applyGeoData(config.geoData, filters, false);
            if (config.useAjaxFilters) {
                try { history.replaceState({ geoFilters: filters }, '', window.location.pathname); } catch (e) { /* */ }
            }
        } else if (config.useAjaxFilters && config.geoDataUrlBase) {
            loadGeoInsights(filters, false);
        }
    }

    return { init };
})();
