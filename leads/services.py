from django.core.exceptions import PermissionDenied, ValidationError
from django.db import transaction
from django.db.models import Q

from accounts.roles import is_admin, is_manager
from crm_core.authorization import visible_customers
from crm_core.services import record_event
from customers.models import Customer
from leads.models import Lead
from leads.rules import ALLOWED_TRANSITIONS


@transaction.atomic
def save_lead(*, form, actor, instance=None):
    previous_assignee = form.original_assigned_to_id if instance else None
    previous_status = form.original_status if instance else None
    lead = form.save(commit=False)
    if instance is None:
        lead.created_by = actor
        event_type = "created"
    else:
        event_type = "updated"
    lead.save()
    if instance and previous_assignee != lead.assigned_to_id:
        record_event(actor=actor, event_type="assigned", instance=lead)
    if instance and previous_status != lead.status:
        record_event(actor=actor, event_type="status_changed", instance=lead)
    record_event(actor=actor, event_type=event_type, instance=lead)
    return lead


@transaction.atomic
def transition_lead(*, lead, actor, status):
    if status not in ALLOWED_TRANSITIONS.get(lead.status, set()):
        raise ValidationError("That lead status transition is not allowed.")
    lead.status = status
    lead.save(update_fields=("status", "modified_at"))
    record_event(actor=actor, event_type="lost" if status == Lead.Status.LOST else "status_changed", instance=lead)
    return lead


def _matching_customer(lead):
    matches = Customer.objects.none()
    if lead.email:
        matches = Customer.objects.filter(email__iexact=lead.email)
    if lead.phone:
        phone_matches = Customer.objects.filter(phone=lead.phone)
        matches = (matches | phone_matches).distinct()
    return matches.order_by("pk").first()


@transaction.atomic
def convert_lead(*, lead, actor):
    lead = Lead.objects.select_for_update().get(pk=lead.pk)
    if lead.status != Lead.Status.QUALIFIED:
        raise ValidationError("Only qualified leads can be converted.")
    if not (is_admin(actor) or is_manager(actor) or lead.assigned_to_id == actor.pk or lead.created_by_id == actor.pk):
        raise PermissionDenied

    customer = _matching_customer(lead)
    if customer and not visible_customers(actor).filter(pk=customer.pk).exists():
        raise PermissionDenied
    if customer is None:
        customer = Customer.objects.create(
            customer_name=lead.lead_name,
            email=lead.email,
            phone=lead.phone,
            company_name=lead.company_name,
            created_by=lead.created_by,
        )
        record_event(actor=actor, event_type="created", instance=customer)
    lead.customer = customer
    lead.status = Lead.Status.CONVERTED
    lead.save(update_fields=("customer", "status", "modified_at"))
    record_event(actor=actor, event_type="converted", instance=lead)
    return customer