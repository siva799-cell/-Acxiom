from django.contrib import messages
from django.core.exceptions import PermissionDenied, ValidationError
from django.core.paginator import Paginator
from django.db.models import Q
from django.shortcuts import get_object_or_404, redirect, render
from django.views.decorators.http import require_POST

from crm_core.authorization import assignable_users, visible_leads
from crm_core.authorization import visible_activities, visible_followups
from crm_core.decorators import crm_role_required
from leads.forms import LeadForm
from leads.models import Lead
from leads.rules import ALLOWED_TRANSITIONS
from leads.services import convert_lead, save_lead


@crm_role_required
def lead_list(request):
    leads = visible_leads(request.user).select_related("assigned_to", "created_by", "customer")
    query = request.GET.get("q", "").strip()
    filters = {
        "status": request.GET.get("status", ""),
        "source": request.GET.get("source", ""),
        "priority": request.GET.get("priority", ""),
        "assigned_to": request.GET.get("assigned_to", ""),
    }
    if query:
        leads = leads.filter(
            Q(lead_code__icontains=query)
            | Q(lead_name__icontains=query)
            | Q(company_name__icontains=query)
            | Q(email__icontains=query)
            | Q(phone__icontains=query)
        )
    for key, choices in (("status", Lead.Status), ("source", Lead.Source), ("priority", Lead.Priority)):
        if filters[key] in choices.values:
            leads = leads.filter(**{key: filters[key]})
        else:
            filters[key] = ""
    if filters["assigned_to"].isdigit():
        leads = leads.filter(assigned_to_id=filters["assigned_to"])
    else:
        filters["assigned_to"] = ""
    page_obj = Paginator(leads, 15).get_page(request.GET.get("page"))
    return render(request, "leads/list.html", {
        "page_obj": page_obj,
        "q": query,
        "filters": filters,
        "status_choices": Lead.Status.choices,
        "source_choices": Lead.Source.choices,
        "priority_choices": Lead.Priority.choices,
        "assignees": assignable_users(request.user),
    })


@crm_role_required
def lead_create(request):
    if request.method == "POST":
        form = LeadForm(request.POST, user=request.user)
        if form.is_valid():
            lead = save_lead(form=form, actor=request.user)
            messages.success(request, "Lead created.")
            return redirect("leads:detail", pk=lead.pk)
    else:
        form = LeadForm(user=request.user)
    return render(request, "leads/form.html", {"form": form, "is_create": True})


@crm_role_required
def lead_detail(request, pk):
    lead = get_object_or_404(visible_leads(request.user).select_related("assigned_to", "created_by", "customer"), pk=pk)
    next_statuses = ALLOWED_TRANSITIONS.get(lead.status, set())
    return render(request, "leads/detail.html", {
        "lead": lead,
        "lead_next_statuses": next_statuses,
        "lead_status_choices": Lead.Status.choices,
        "related_followups": visible_followups(request.user).filter(lead=lead).select_related("assigned_to")[:5],
        "related_activities": visible_activities(request.user).filter(lead=lead).select_related("assigned_to")[:5],
    })


@crm_role_required
def lead_edit(request, pk):
    lead = get_object_or_404(visible_leads(request.user), pk=pk)
    if lead.status in (Lead.Status.CONVERTED, Lead.Status.LOST, Lead.Status.UNQUALIFIED):
        messages.error(request, "This lead is closed and cannot be edited.")
        return redirect("leads:detail", pk=lead.pk)
    if request.method == "POST":
        form = LeadForm(request.POST, instance=lead, user=request.user)
        if form.is_valid():
            save_lead(form=form, actor=request.user, instance=lead)
            messages.success(request, "Lead updated.")
            return redirect("leads:detail", pk=lead.pk)
    else:
        form = LeadForm(instance=lead, user=request.user)
    return render(request, "leads/form.html", {"form": form, "lead": lead})


@crm_role_required
def lead_convert(request, pk):
    lead = get_object_or_404(visible_leads(request.user).select_related("customer"), pk=pk)
    if request.method == "POST":
        try:
            customer = convert_lead(lead=lead, actor=request.user)
        except ValidationError as error:
            messages.error(request, error.messages[0])
            return redirect("leads:detail", pk=lead.pk)
        except PermissionDenied:
            raise
        messages.success(request, "Lead converted and linked to a customer.")
        return redirect("customers:detail", pk=customer.pk)
    if lead.status != Lead.Status.QUALIFIED:
        messages.error(request, "Only qualified leads can be converted.")
        return redirect("leads:detail", pk=lead.pk)
    return render(request, "leads/convert.html", {"lead": lead})


@crm_role_required
@require_POST
def lead_status(request, pk):
    lead = get_object_or_404(visible_leads(request.user), pk=pk)
    form = LeadForm({**request.POST, "assigned_to": lead.assigned_to_id}, instance=lead, user=request.user)
    if form.is_valid():
        save_lead(form=form, actor=request.user, instance=lead)
        messages.success(request, "Lead status updated.")
    else:
        messages.error(request, "That lead status change is not valid.")
    return redirect("leads:detail", pk=lead.pk)