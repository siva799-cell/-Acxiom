from django.contrib.auth.decorators import login_required
from django.shortcuts import render

from accounts.roles import ADMIN, MANAGER, SALES_EXECUTIVE, get_user_role
from crm_core.authorization import visible_followups
from followups.models import FollowUp
from django.utils import timezone


ROLE_MESSAGES = {
    ADMIN: "You have full administrative access to AcxiomCRM.",
    MANAGER: "You have access to team CRM management features.",
    SALES_EXECUTIVE: "You have access to your assigned CRM activities.",
}


@login_required
def home(request):
    role = get_user_role(request.user)
    context = {
        "role": role,
        "role_message": ROLE_MESSAGES.get(role, "Your AcxiomCRM account is ready."),
        "todays_followups": visible_followups(request.user).filter(
            status=FollowUp.Status.PLANNED,
            follow_up_date=timezone.localdate(),
        ).count(),
        "overdue_followups": visible_followups(request.user).filter(
            status=FollowUp.Status.PLANNED,
            follow_up_date__lt=timezone.localdate(),
        ).count(),
        "completed_followups": visible_followups(request.user).filter(status=FollowUp.Status.COMPLETED).count(),
    }
    return render(request, "dashboard/dashboard.html", context)