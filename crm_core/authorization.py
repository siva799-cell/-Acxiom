from django.db.models import Q

from accounts.roles import ADMIN, MANAGER, get_user_role


def is_sales_user(user):
    return get_user_role(user) == "SalesExecutive"


def assignable_users(user):
    from django.contrib.auth import get_user_model
    from django.contrib.auth.models import Group

    user_model = get_user_model()
    role = get_user_role(user)
    if role == ADMIN:
        return user_model.objects.filter(is_active=True, groups__name="SalesExecutive").distinct().order_by("username")
    if role == MANAGER:
        return user_model.objects.filter(
            is_active=True,
            groups__name="SalesExecutive",
        ).distinct().order_by("username")
    return user_model.objects.filter(pk=user.pk, is_active=True)


def visible_customers(user):
    from customers.models import Customer

    role = get_user_role(user)
    queryset = Customer.objects.all()
    if role in (ADMIN, MANAGER):
        return queryset
    if is_sales_user(user):
        return queryset.filter(created_by=user)
    return queryset.none()


def visible_leads(user):
    from leads.models import Lead

    role = get_user_role(user)
    queryset = Lead.objects.all()
    if role in (ADMIN, MANAGER):
        return queryset
    if is_sales_user(user):
        return queryset.filter(Q(assigned_to=user) | Q(created_by=user)).distinct()
    return queryset.none()


def visible_followups(user):
    from followups.models import FollowUp

    role = get_user_role(user)
    queryset = FollowUp.objects.all()
    if role in (ADMIN, MANAGER):
        return queryset
    if is_sales_user(user):
        return queryset.filter(
            assigned_to=user,
        ).filter(
            Q(customer__isnull=True) | Q(customer__created_by=user),
            Q(lead__isnull=True) | Q(lead__assigned_to=user) | Q(lead__created_by=user),
        ).distinct()
    return queryset.none()


def visible_activities(user):
    from activities.models import Activity

    role = get_user_role(user)
    queryset = Activity.objects.all()
    if role in (ADMIN, MANAGER):
        return queryset
    if is_sales_user(user):
        return queryset.filter(
            assigned_to=user,
        ).filter(
            Q(customer__isnull=True) | Q(customer__created_by=user),
            Q(lead__isnull=True) | Q(lead__assigned_to=user) | Q(lead__created_by=user),
        ).distinct()
    return queryset.none()