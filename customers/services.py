from django.db import transaction

from crm_core.services import record_event
from customers.models import Customer


@transaction.atomic
def save_customer(*, form, actor, instance=None):
    customer = form.save(commit=False)
    if instance is None:
        customer.created_by = actor
        event_type = "created"
    else:
        event_type = "updated"
    customer.save()
    form.save_m2m()
    record_event(actor=actor, event_type=event_type, instance=customer)
    return customer


@transaction.atomic
def set_customer_status(*, customer, actor, status):
    if status not in Customer.Status.values:
        raise ValueError("Invalid customer status.")
    customer.status = status
    customer.save(update_fields=("status", "modified_at"))
    record_event(
        actor=actor,
        event_type="activated" if status == Customer.Status.ACTIVE else "deactivated",
        instance=customer,
    )
    return customer