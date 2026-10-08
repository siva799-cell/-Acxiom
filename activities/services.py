from django.core.exceptions import ValidationError
from django.db import transaction

from activities.models import Activity
from crm_core.services import record_event


ALLOWED_TRANSITIONS = {
    Activity.Status.PLANNED: {Activity.Status.COMPLETED, Activity.Status.CANCELLED},
    Activity.Status.COMPLETED: set(),
    Activity.Status.CANCELLED: set(),
}


@transaction.atomic
def save_activity(*, form, actor, instance=None):
    previous_assignee = form.original_assigned_to_id if instance else None
    activity = form.save(commit=False)
    if instance is None:
        activity.created_by = actor
        event_types = ("created",)
    else:
        event_types = ["updated"]
        if previous_assignee != activity.assigned_to_id:
            event_types.append("assigned")
    activity.full_clean()
    activity.save()
    for event_type in event_types:
        record_event(actor=actor, event_type=event_type, instance=activity)
    return activity


@transaction.atomic
def transition_activity(*, activity, actor, status):
    if status not in ALLOWED_TRANSITIONS.get(activity.status, set()):
        raise ValidationError("That activity status transition is not allowed.")
    activity.status = status
    activity.save(update_fields=("status",))
    record_event(actor=actor, event_type=status.lower(), instance=activity)
    return activity