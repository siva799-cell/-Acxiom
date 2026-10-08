from datetime import timedelta

from django.core.exceptions import ValidationError
from django.db import transaction
from django.utils import timezone

from crm_core.services import record_event
from followups.models import FollowUp


ALLOWED_TRANSITIONS = {
    FollowUp.Status.PLANNED: {FollowUp.Status.COMPLETED, FollowUp.Status.MISSED, FollowUp.Status.CANCELLED},
    FollowUp.Status.COMPLETED: set(),
    FollowUp.Status.MISSED: set(),
    FollowUp.Status.CANCELLED: set(),
}


@transaction.atomic
def save_followup(*, form, actor, instance=None):
    previous_date = form.original_follow_up_date if instance else None
    previous_assignee = form.original_assigned_to_id if instance else None
    followup = form.save(commit=False)
    if instance is None:
        followup.created_by = actor
        event_types = ("created",)
    else:
        event_types = ["updated"]
        if previous_date != followup.follow_up_date:
            event_types.append("rescheduled")
        if previous_assignee != followup.assigned_to_id:
            event_types.append("assigned")
    followup.full_clean()
    followup.save()
    for event_type in event_types:
        record_event(actor=actor, event_type=event_type, instance=followup)
    return followup


@transaction.atomic
def transition_followup(*, followup, actor, status):
    if status not in ALLOWED_TRANSITIONS.get(followup.status, set()):
        raise ValidationError("That follow-up status transition is not allowed.")
    if status == FollowUp.Status.MISSED and followup.follow_up_date >= timezone.localdate():
        raise ValidationError("A follow-up can only be marked missed after its scheduled date.")
    followup.status = status
    update_fields = ["status"]
    if status == FollowUp.Status.COMPLETED:
        followup.completed_at = timezone.now()
        update_fields.append("completed_at")
    followup.save(update_fields=update_fields)
    record_event(actor=actor, event_type=status.lower(), instance=followup)
    return followup