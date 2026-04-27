// ═══════════════════════════════════════════════════════════════
// Kanrich HRMS — Notification Panel
// Include this script in all pages that have a bell icon
// ═══════════════════════════════════════════════════════════════

(function () {

    // ── Inject notification panel HTML into page ───────────────────
    document.addEventListener('DOMContentLoaded', function () {

        // Inject panel
        document.body.insertAdjacentHTML('beforeend', `
            <div id="notifOverlay" onclick="closeNotifications()" 
                 style="display:none;position:fixed;inset:0;z-index:40;background:transparent"></div>

            <div id="notifPanel"
                 style="display:none;position:fixed;top:68px;right:20px;width:380px;max-height:520px;
                        background:#fff;border:1px solid #e2e5dc;border-radius:16px;
                        box-shadow:0 8px 32px rgba(0,0,0,0.12);z-index:41;
                        font-family:'Manrope',sans-serif;overflow:hidden;flex-direction:column;">

                <!-- Panel Header -->
                <div style="display:flex;align-items:center;justify-content:space-between;
                            padding:16px 20px;border-bottom:1px solid #e2e5dc;background:#fff;">
                    <div style="display:flex;align-items:center;gap:8px;">
                        <span style="font-family:'Material Symbols Outlined';font-size:20px;
                                     color:#10823c;font-variation-settings:'FILL' 1,'wght' 400,'GRAD' 0,'opsz' 24">
                            notifications
                        </span>
                        <span style="font-size:14px;font-weight:800;color:#151811">Notifications</span>
                        <span id="notifBadgePanel" style="display:none;background:#10823c;color:#fff;
                              font-size:10px;font-weight:700;padding:2px 7px;border-radius:999px"></span>
                    </div>
                    <button onclick="closeNotifications()"
                            style="background:none;border:none;cursor:pointer;padding:4px;
                                   color:#7a8863;font-family:'Material Symbols Outlined';font-size:18px;
                                   font-variation-settings:'FILL' 0,'wght' 400,'GRAD' 0,'opsz' 24">
                        close
                    </button>
                </div>

                <!-- Panel Body -->
                <div id="notifList" style="overflow-y:auto;flex:1;max-height:420px;padding:8px 0;">
                    <div style="display:flex;flex-direction:column;align-items:center;
                                justify-content:center;padding:40px 20px;color:#7a8863;">
                        <span style="font-family:'Material Symbols Outlined';font-size:40px;
                                     font-variation-settings:'FILL' 0,'wght' 400,'GRAD' 0,'opsz' 24;margin-bottom:8px">
                            hourglass_top
                        </span>
                        <p style="font-size:13px;font-weight:600">Loading notifications...</p>
                    </div>
                </div>

                <!-- Panel Footer -->
                <div style="padding:12px 20px;border-top:1px solid #e2e5dc;background:#f6f8f7;text-align:center">
                    <span style="font-size:11px;color:#7a8863">Showing latest 10 notifications</span>
                </div>
            </div>
        `);

        // Load notification count for badge
        loadNotificationCount();
    });

    // ── Load count for bell badge ──────────────────────────────────
    async function loadNotificationCount() {
        try {
            const res = await fetch('/api/notifications');
            if (!res.ok) return;
            const data = await res.json();

            const count = data.filter(n => n.type === 'warning' || n.type === 'error').length;

            // Update all bell badges on the page
            document.querySelectorAll('.notif-badge').forEach(badge => {
                if (count > 0) {
                    badge.style.display = 'flex';
                    badge.textContent = count > 9 ? '9+' : count;
                } else {
                    badge.style.display = 'none';
                }
            });

            // Also update the red dot on bell buttons
            document.querySelectorAll('.notif-dot').forEach(dot => {
                dot.style.display = count > 0 ? 'block' : 'none';
            });

        } catch (e) {
            console.log('Notification count load failed:', e);
        }
    }

    // ── Open notification panel ────────────────────────────────────
    window.openNotifications = async function () {
        const panel = document.getElementById('notifPanel');
        const overlay = document.getElementById('notifOverlay');

        if (panel.style.display === 'flex') {
            closeNotifications();
            return;
        }

        panel.style.display = 'flex';
        overlay.style.display = 'block';

        // Fetch notifications
        try {
            const res = await fetch('/api/notifications');
            if (!res.ok) {
                showError();
                return;
            }
            const notifications = await res.json();
            renderNotifications(notifications);
        } catch (e) {
            showError();
        }
    };

    // ── Close panel ────────────────────────────────────────────────
    window.closeNotifications = function () {
        document.getElementById('notifPanel').style.display = 'none';
        document.getElementById('notifOverlay').style.display = 'none';
    };

    // ── Render notifications ───────────────────────────────────────
    function renderNotifications(notifications) {
        const list = document.getElementById('notifList');

        if (!notifications || notifications.length === 0) {
            list.innerHTML = `
                <div style="display:flex;flex-direction:column;align-items:center;
                            justify-content:center;padding:40px 20px;color:#7a8863;">
                    <span style="font-family:'Material Symbols Outlined';font-size:40px;
                                 font-variation-settings:'FILL' 0,'wght' 400,'GRAD' 0,'opsz' 24;margin-bottom:8px">
                        notifications_off
                    </span>
                    <p style="font-size:13px;font-weight:600">No notifications</p>
                    <p style="font-size:11px;margin-top:4px">You are all caught up!</p>
                </div>`;

            // Update badge
            const badge = document.getElementById('notifBadgePanel');
            badge.style.display = 'none';
            return;
        }

        // Update panel badge
        const actionCount = notifications.filter(n => n.type === 'warning' || n.type === 'error').length;
        const badge = document.getElementById('notifBadgePanel');
        if (actionCount > 0) {
            badge.style.display = 'inline-block';
            badge.textContent = actionCount;
        } else {
            badge.style.display = 'none';
        }

        const colors = {
            success: { bg: '#f0fdf4', border: '#bbf7d0', icon: '#10823c', dot: '#10823c' },
            error: { bg: '#fef2f2', border: '#fecaca', icon: '#dc2626', dot: '#dc2626' },
            warning: { bg: '#fffbeb', border: '#fde68a', icon: '#d97706', dot: '#d97706' },
            info: { bg: '#eff6ff', border: '#bfdbfe', icon: '#2563eb', dot: '#2563eb' },
        };

        list.innerHTML = notifications.map(n => {
            const c = colors[n.type] || colors.info;
            return `
                <a href="${n.link}" onclick="closeNotifications()"
                   style="display:flex;align-items:flex-start;gap:12px;padding:12px 20px;
                          text-decoration:none;transition:background 0.15s;border-bottom:1px solid #f3f4f6;"
                   onmouseover="this.style.background='#f6f8f7'"
                   onmouseout="this.style.background='transparent'">
                    <div style="width:36px;height:36px;border-radius:10px;background:${c.bg};
                                border:1px solid ${c.border};display:flex;align-items:center;
                                justify-content:center;flex-shrink:0;margin-top:2px;">
                        <span style="font-family:'Material Symbols Outlined';font-size:18px;color:${c.icon};
                                     font-variation-settings:'FILL' 1,'wght' 400,'GRAD' 0,'opsz' 24">
                            ${n.icon}
                        </span>
                    </div>
                    <div style="flex:1;min-width:0;">
                        <div style="display:flex;align-items:center;justify-content:space-between;gap:8px;margin-bottom:3px;">
                            <p style="font-size:12px;font-weight:700;color:#151811;margin:0">${n.title}</p>
                            <span style="font-size:10px;color:#7a8863;white-space:nowrap;flex-shrink:0">${n.timeAgo}</span>
                        </div>
                        <p style="font-size:11px;color:#7a8863;margin:0;line-height:1.5">${n.message}</p>
                    </div>
                </a>`;
        }).join('');
    }

    // ── Show error state ───────────────────────────────────────────
    function showError() {
        document.getElementById('notifList').innerHTML = `
            <div style="display:flex;flex-direction:column;align-items:center;
                        justify-content:center;padding:40px 20px;color:#7a8863;">
                <span style="font-family:'Material Symbols Outlined';font-size:40px;
                             font-variation-settings:'FILL' 0,'wght' 400,'GRAD' 0,'opsz' 24;margin-bottom:8px;color:#ef4444">
                    error
                </span>
                <p style="font-size:13px;font-weight:600;color:#ef4444">Failed to load</p>
                <p style="font-size:11px;margin-top:4px">Please try again.</p>
            </div>`;
    }

})();
