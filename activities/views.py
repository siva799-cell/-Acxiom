from django.contrib import messages
from django.core.exceptions import ValidationError
from django.core.paginator import Paginator
from django.shortcuts import get_object_or_404, redirect, render
from django.views.decorators.http import require_POST

from activities.forms import ActivityFilterForm, ActivityForm
from activities.models import Activity
from activities.services import save_activity, transition_activity
from crm_core.authorization import assignable_users, visible_activities
from crm_core.decorators import crm_role_required


@crm_role_required
def activity_list(request):
    form = ActivityFilterForm(request.GET or None, user=request.user)
    queryset = visible_activities(request.user).select_related("customer", "lead", "assigned_to", "created_by")
    if form.is_valid():
        for key in ("activity_type", "status"):
            if form.cleaned_data[key]:
                queryset = queryset.filter(**{key: form.cleaned_data[key]})
        for key in ("assigned_to", "customer", "lead"):
            if form.cleaned_data[key]:
                queryset = queryset.filter(**{f"{key}_id": form.cleaned_data[key].pk})
        if form.cleaned_data["date_from"]:
            queryset = queryset.filter(activity_date__gte=form.cleaned_data["date_from"])
        if form.cleaned_data["date_to"]:
            queryset = queryset.filter(activity_date__lte=form.cleaned_data["date_to"])
    page_obj = Paginator(queryset, 15).get_page(request.GET.get("page"))
    return render(request, "activities/list.html", {"page_obj": page_obj, "filter_form": form, "assignees": assignable_users(request.user)})


@crm_role_required
def activity_create(request):
    if request.method == "POST":
        form = ActivityForm(request.POST, user=request.user)
        if form.is_valid():
            activity = save_activity(form=form, actor=request.user)
            messages.success(request, "Activity created.")
            return redirect("activities:detail", pk=activity.pk)
    else:
        initial = {}
        for related_kind in ("customer", "lead"):
            value = request.GET.get(related_kind, "")
            if value.isdigit():
                initial[related_kind] = int(value)
        form = ActivityForm(initial=initial, user=request.user)
    return render(request, "activities/form.html", {"form": form, "is_create": True})


@crm_role_required
def activity_detail(request, pk):
    activity = get_object_or_404(visible_activities(request.user).select_related("customer", "lead", "assigned_to", "created_by"), pk=pk)
    return render(request, "activities/detail.html", {
        "activity": activity,
        "activity_actions": ((Activity.Status.COMPLETED, "Completed", "success"), (Activity.Status.CANCELLED, "Cancelled", "secondary")),
    })


@crm_role_required
def activity_edit(request, pk):
    activity = get_object_or_404(visible_activities(request.user).select_related("customer", "lead"), pk=pk)
    if activity.status != Activity.Status.PLANNED:
        messages.error(request, "Only planned activities can be edited.")
        return redirect("activities:detail", pk=activity.pk)
    if request.method == "POST":
        form = ActivityForm(request.POST, instance=activity, user=request.user)
        if form.is_valid():
            save_activity(form=form, actor=request.user, instance=activity)
            messages.success(request, "Activity updated.")
            return redirect("activities:detail", pk=activity.pk)
    else:
        form = ActivityForm(instance=activity, user=request.user)
    return render(request, "activities/form.html", {"form": form, "activity": activity})


@crm_role_required
@require_POST
def activity_status(request, pk, status):
    activity = get_object_or_404(visible_activities(request.user), pk=pk)
    try:
        transition_activity(activity=activity, actor=request.user, status=status)
    except ValidationError as error:
        messages.error(request, error.messages[0])
    else:
        messages.success(request, f"Activity marked {status.lower()}.")
    return redirect("activities:detail", pk=activity.pk)