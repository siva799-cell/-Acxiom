from datetime import timedelta

from django.contrib import messages
from django.core.exceptions import ValidationError
from django.core.paginator import Paginator
from django.shortcuts import get_object_or_404, redirect, render
from django.utils import timezone
from django.views.decorators.http import require_POST

from crm_core.authorization import assignable_users, visible_followups
from crm_core.decorators import crm_role_required
from followups.forms import FollowUpFilterForm, FollowUpForm
from followups.models import FollowUp
from followups.services import transition_followup, save_followup


@crm_role_required
def followup_list(request):
    form = FollowUpFilterForm(request.GET or None, user=request.user)
    queryset = visible_followups(request.user).select_related("customer", "lead", "assigned_to", "created_by")
    if form.is_valid():
        data = form.cleaned_data
        today = timezone.localdate()
        if data["period"] == "today":
            queryset = queryset.filter(follow_up_date=today)
        elif data["period"] == "week":
            start = today - timedelta(days=today.weekday())
            queryset = queryset.filter(follow_up_date__range=(start, start + timedelta(days=6)))
        elif data["period"] == "month":
            queryset = queryset.filter(follow_up_date__year=today.year, follow_up_date__month=today.month)
        elif data["period"] == "custom":
            if data["date_from"]:
                queryset = queryset.filter(follow_up_date__gte=data["date_from"])
            if data["date_to"]:
                queryset = queryset.filter(follow_up_date__lte=data["date_to"])
        for key in ("status", "follow_up_type"):
            if data[key]:
                queryset = queryset.filter(**{key: data[key]})
        for key in ("assigned_to", "customer", "lead"):
            if data[key]:
                queryset = queryset.filter(**{f"{key}_id": data[key].pk})
    page_obj = Paginator(queryset, 15).get_page(request.GET.get("page"))
    return render(request, "followups/list.html", {
        "page_obj": page_obj,
        "filter_form": form,
        "assignees": assignable_users(request.user),
    })


@crm_role_required
def followup_create(request):
    if request.method == "POST":
        form = FollowUpForm(request.POST, user=request.user)
        if form.is_valid():
            followup = save_followup(form=form, actor=request.user)
            messages.success(request, "Follow-up created.")
            return redirect("followups:detail", pk=followup.pk)
    else:
        initial = {}
        for related_kind in ("customer", "lead"):
            value = request.GET.get(related_kind, "")
            if value.isdigit():
                initial[related_kind] = int(value)
        form = FollowUpForm(initial=initial, user=request.user)
    return render(request, "followups/form.html", {"form": form, "is_create": True})


@crm_role_required
def followup_detail(request, pk):
    followup = get_object_or_404(visible_followups(request.user).select_related("customer", "lead", "assigned_to", "created_by"), pk=pk)
    return render(request, "followups/detail.html", {
        "followup": followup,
        "followup_actions": (
            (FollowUp.Status.COMPLETED, "Completed", "success"),
            (FollowUp.Status.MISSED, "Missed", "danger"),
            (FollowUp.Status.CANCELLED, "Cancelled", "secondary"),
        ),
    })


@crm_role_required
def followup_edit(request, pk):
    followup = get_object_or_404(visible_followups(request.user).select_related("customer", "lead"), pk=pk)
    if followup.status != FollowUp.Status.PLANNED:
        messages.error(request, "Only planned follow-ups can be edited.")
        return redirect("followups:detail", pk=followup.pk)
    if request.method == "POST":
        form = FollowUpForm(request.POST, instance=followup, user=request.user)
        if form.is_valid():
            save_followup(form=form, actor=request.user, instance=followup)
            messages.success(request, "Follow-up updated.")
            return redirect("followups:detail", pk=followup.pk)
    else:
        form = FollowUpForm(instance=followup, user=request.user)
    return render(request, "followups/form.html", {"form": form, "followup": followup})


@crm_role_required
@require_POST
def followup_status(request, pk, status):
    followup = get_object_or_404(visible_followups(request.user), pk=pk)
    try:
        transition_followup(followup=followup, actor=request.user, status=status)
    except ValidationError as error:
        messages.error(request, error.messages[0])
    else:
        messages.success(request, f"Follow-up marked {status.lower()}.")
    return redirect("followups:detail", pk=followup.pk)