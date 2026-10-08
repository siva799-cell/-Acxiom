from accounts.roles import CRM_ROLES, role_required


crm_role_required = role_required(*CRM_ROLES)