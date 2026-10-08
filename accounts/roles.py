from functools import wraps

from django.contrib.auth.decorators import login_required
from django.core.exceptions import PermissionDenied


ADMIN = "Admin"
MANAGER = "Manager"
SALES_EXECUTIVE = "SalesExecutive"
CRM_ROLES = (ADMIN, MANAGER, SALES_EXECUTIVE)


def get_user_role(user):
    if not getattr(user, "is_authenticated", False):
        return None
    if user.is_superuser or user.groups.filter(name=ADMIN).exists():
        return ADMIN
    if user.groups.filter(name=MANAGER).exists():
        return MANAGER
    if user.groups.filter(name=SALES_EXECUTIVE).exists():
        return SALES_EXECUTIVE
    return None


def is_admin(user):
    return get_user_role(user) == ADMIN


def is_manager(user):
    return get_user_role(user) == MANAGER


def is_sales_executive(user):
    return get_user_role(user) == SALES_EXECUTIVE


def has_any_role(user, *roles):
    return get_user_role(user) in roles


def role_required(*roles):
    def decorate(view_func):
        @wraps(view_func)
        def check_role(request, *args, **kwargs):
            if not has_any_role(request.user, *roles):
                raise PermissionDenied
            return view_func(request, *args, **kwargs)

        return login_required(check_role)

    return decorate