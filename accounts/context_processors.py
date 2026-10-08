from accounts.roles import ADMIN, CRM_ROLES, get_user_role


CRM_ITEMS = (
    ("Customers", "bi-people", "customers:list"),
    ("Leads", "bi-person-lines-fill", "leads:list"),
    ("Follow-Ups", "bi-calendar2-check", "followups:list"),
    ("Opportunities", "bi-briefcase", None),
    ("Activities", "bi-list-check", "activities:list"),
    ("Reports", "bi-bar-chart-line", None),
)
ADMIN_ITEMS = (
    ("User Management", "bi-person-gear"),
    ("Role Management", "bi-shield-lock"),
    ("Audit Logs", "bi-journal-text"),
    ("Settings", "bi-gear"),
)


def crm_identity(request):
    role = get_user_role(request.user)
    return {
        "crm_role": role,
        "crm_role_label": role or "Unassigned",
        "crm_navigation": {
            "crm_items": CRM_ITEMS if role in CRM_ROLES else (),
            "admin_items": ADMIN_ITEMS if role == ADMIN else (),
            "show_admin": role == ADMIN,
        },
    }